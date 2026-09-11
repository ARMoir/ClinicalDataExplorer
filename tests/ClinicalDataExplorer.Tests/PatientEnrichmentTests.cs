using System.Net;
using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientEnrichmentTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Entry(string type, string id) => $"<entry><resource><{type}><id value='{id}'/></{type}></resource></entry>";

    [Fact]
    public async Task Enrichment_pages_sequentially_and_waits_for_foreground_before_each_page()
    {
        var waits = 0;
        var snapshots = new List<IReadOnlyList<PatientResourceSection>>();
        using var slots = new SemaphoreSlim(3);
        using var handler = new ScriptedHandler(
            r => { Assert.EndsWith("/$everything", r.RequestUri!.AbsolutePath); Assert.Equal(1, waits); Assert.Equal(2, slots.CurrentCount); return ScriptedHandler.Xml(Bundle(Entry("Medication", "m1"), "Patient/p1/$everything?cursor=2")); },
            r => { Assert.Equal(2, waits); Assert.Single(snapshots); return ScriptedHandler.Xml(Bundle(Entry("Medication", "m1") + Entry("Practitioner", "pr1"))); });
        using var context = new FhirTestContext(BaseUrl, handler);
        var complete = await context.Service.EnrichPatientResourcesAsync("p1", sections =>
        {
            Assert.Equal(3, slots.CurrentCount);
            snapshots.Add(sections);
            return Task.CompletedTask;
        }, waitForForeground: _ => { waits++; return Task.CompletedTask; }, requestSlots: slots);
        Assert.True(complete);
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(2, snapshots[^1].Sum(s => s.Count));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Timeout_retains_completed_pages_and_releases_request_slot()
    {
        var snapshots = new List<IReadOnlyList<PatientResourceSection>>();
        var calls = 0;
        using var slots = new SemaphoreSlim(3);
        using var handler = new AsyncHandler(async (_, token) =>
        {
            if (++calls == 1) return ScriptedHandler.Xml(Bundle(Entry("Medication", "m1"), "Patient/p1/$everything?cursor=2"));
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var complete = await context.Service.EnrichPatientResourcesAsync("p1", sections =>
        {
            snapshots.Add(sections); return Task.CompletedTask;
        }, requestSlots: slots, timeBudget: TimeSpan.FromMilliseconds(250));
        Assert.False(complete);
        Assert.Single(snapshots);
        Assert.Equal("Medication", Assert.Single(snapshots[0]).ResourceType);
        Assert.Equal(3, slots.CurrentCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NotImplemented)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Unsupported_or_failed_enrichment_is_incomplete(HttpStatusCode status)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("unavailable", status));
        using var context = new FhirTestContext(BaseUrl, handler);
        var complete = await context.Service.EnrichPatientResourcesAsync("p1", _ => throw new Exception("No page should be published"));
        Assert.False(complete);
    }

    [Fact]
    public async Task Navigation_cancellation_propagates_and_does_not_publish_stale_records()
    {
        using var cancellation = new CancellationTokenSource();
        using var handler = new AsyncHandler(async (_, token) =>
        {
            cancellation.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Unreachable");
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.Service.EnrichPatientResourcesAsync("p1",
            _ => throw new Exception("No stale snapshot"), cancellation.Token));
    }

    [Fact]
    public async Task Budget_includes_waiting_for_foreground_work_without_sending_a_request()
    {
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        var complete = await context.Service.EnrichPatientResourcesAsync("p1", _ => Task.CompletedTask,
            waitForForeground: token => Task.Delay(Timeout.Infinite, token), timeBudget: TimeSpan.FromMilliseconds(50));
        Assert.False(complete);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Merge_preserves_foreground_records_and_adds_missing_types_without_duplicates()
    {
        XNamespace f = "http://hl7.org/fhir";
        XElement Record(string type, string id, string value) => new(f + type, new XElement(f + "id", new XAttribute("value", id)), new XElement(f + "valueString", new XAttribute("value", value)));
        var primary = Record("Observation", "same", "current");
        var sections = PatientSectionGrouping.Merge([primary], [Record("Observation", "same", "older"), Record("Medication", "same", "drug"), Record("Observation", "report", "line1\nline2")]);
        Assert.Equal(3, sections.Sum(s => s.Count));
        Assert.Equal("current", Assert.Single(sections.Single(s => s.ResourceType == "Observation").Resources).Element(f + "valueString")!.Attribute("value")!.Value);
        Assert.Single(sections, s => s.ResourceType == "Medication");
        Assert.Single(sections, s => s.ResourceType == "DocumentReference");
        Assert.Null(primary.Parent);
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token);
    }
}
