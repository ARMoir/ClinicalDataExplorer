using System.Net;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientResourceSearchTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Entry(string type, string id) => $"<entry><resource><{type}><id value='{id}'/></{type}></resource></entry>";

    [Fact]
    public async Task Stalled_observations_do_not_block_conditions()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new AsyncHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/Observation"))
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, token);
            }
            return ScriptedHandler.Xml(Bundle(Entry("Condition", "c1")));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var observations = new PatientResourceLoad("Observation");
        var pending = context.Service.LoadPatientResourcePageAsync("p1", observations, cancellation.Token);
        await started.Task.WaitAsync(cancellation.Token);
        var conditions = new PatientResourceLoad("Condition");
        await context.Service.LoadPatientResourcePageAsync("p1", conditions, cancellation.Token);
        Assert.Single(conditions.Resources);
        Assert.False(pending.IsCompleted);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(observations.Loaded);
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token);
    }

    [Fact]
    public async Task Separate_cursors_retry_failed_page_without_losing_or_duplicating_records()
    {
        using var handler = new ScriptedHandler(
            r => { Assert.Equal("/r4/Patient/p1/Observation", r.RequestUri!.AbsolutePath); Assert.Contains("_count=50", r.RequestUri.Query); return ScriptedHandler.Xml(Bundle(Entry("Observation", "o1"), "Patient/p1/Observation?cursor=2")); },
            r => { Assert.Equal("/r4/Patient/p1/Condition", r.RequestUri!.AbsolutePath); return ScriptedHandler.Xml(Bundle(Entry("Condition", "c1"))); },
            r => ScriptedHandler.Xml("unavailable", HttpStatusCode.ServiceUnavailable),
            r => { Assert.Contains("cursor=2", r.RequestUri!.Query); return ScriptedHandler.Xml(Bundle(Entry("Observation", "o1") + Entry("Observation", "o2"))); });
        using var context = new FhirTestContext(BaseUrl, handler);
        var observations = new PatientResourceLoad("Observation");
        var conditions = new PatientResourceLoad("Condition");
        await context.Service.LoadPatientResourcePageAsync("p1", observations);
        await context.Service.LoadPatientResourcePageAsync("p1", conditions);
        Assert.False(conditions.HasMore);
        Assert.True(observations.HasMore);
        await Assert.ThrowsAsync<HttpRequestException>(() => context.Service.LoadPatientResourcePageAsync("p1", observations));
        Assert.Single(observations.Resources);
        Assert.True(observations.HasMore);
        await context.Service.LoadPatientResourcePageAsync("p1", observations);
        Assert.Equal(2, observations.Resources.Count);
        Assert.False(observations.HasMore);
        await context.Service.LoadPatientResourcePageAsync("p1", observations);
        Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData("https://other.example/Observation?cursor=2")]
    [InlineData("Patient/p1/Observation?_count=50&_format=xml")]
    public async Task Unsafe_or_repeated_next_links_do_not_commit_a_page(string next)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry("Observation", "o1"), next)));
        using var context = new FhirTestContext(BaseUrl, handler);
        var source = new PatientResourceLoad("Observation");
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.LoadPatientResourcePageAsync("p1", source));
        Assert.False(source.Loaded);
        Assert.Empty(source.Resources);
    }

    [Fact]
    public async Task Empty_page_with_continuation_is_not_complete_and_other_patient_cannot_reuse_cursor()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle("", "Patient/p1/Condition?cursor=2")));
        using var context = new FhirTestContext(BaseUrl, handler);
        var source = new PatientResourceLoad("Condition");
        await context.Service.LoadPatientResourcePageAsync("p1", source);
        Assert.True(source.Loaded);
        Assert.True(source.HasMore);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.LoadPatientResourcePageAsync("p2", source));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Unsupported_category_is_an_error_not_zero_records()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("unsupported", HttpStatusCode.NotFound));
        using var context = new FhirTestContext(BaseUrl, handler);
        var source = new PatientResourceLoad("Condition");
        await Assert.ThrowsAsync<HttpRequestException>(() => context.Service.LoadPatientResourcePageAsync("p1", source));
        Assert.False(source.Loaded);
    }

    [Fact]
    public async Task Cancellation_does_not_commit_or_fetch_a_page()
    {
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = new PatientResourceLoad("Observation");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Service.LoadPatientResourcePageAsync("p1", source, cancellation.Token));
        Assert.False(source.Loaded);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Included_supporting_records_and_multiline_document_grouping_are_preserved()
    {
        var observation = "<entry><resource><Observation><id value='o1'/><valueString value='line1&#10;line2'/></Observation></resource></entry>";
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(observation + Entry("Practitioner", "pr1"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var source = new PatientResourceLoad("Observation");
        await context.Service.LoadPatientResourcePageAsync("p1", source);
        System.Xml.Linq.XNamespace f = "http://hl7.org/fhir";
        var bundle = new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement(f + "Bundle", source.Resources.Select(r => new System.Xml.Linq.XElement(f + "entry", new System.Xml.Linq.XElement(f + "resource", r)))));
        var sections = PatientSectionGrouping.Build(bundle);
        Assert.Single(sections, s => s.ResourceType == "DocumentReference");
        Assert.Single(sections, s => s.ResourceType == "Practitioner");
        Assert.Equal(1, handler.Calls);
    }
}
