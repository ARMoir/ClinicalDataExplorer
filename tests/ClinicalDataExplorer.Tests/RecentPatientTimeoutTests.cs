using System.Diagnostics;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class RecentPatientTimeoutTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private const string Patient = "<Patient><id value='p1'/></Patient>";
    private static string Entry(string resource) => $"<entry><resource>{resource}</resource></entry>";
    private static string Encounter(string id) => Entry($"<Encounter><subject><reference value='Patient/{id}'/></subject></Encounter>");

    [Fact]
    public async Task Recent_provider_timeout_keeps_patient_and_continues_to_next_patient()
    {
        using var handler = new AsyncHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("Encounter"))
                return ScriptedHandler.Xml(Bundle(Encounter("p1") + Entry(Patient) + Encounter("p2") + Entry("<Patient><id value='p2'/></Patient>")));
            if (request.RequestUri.AbsolutePath.Contains("/p1/")) await Task.Delay(Timeout.Infinite, token);
            return ScriptedHandler.Xml(Bundle(""));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var watch = Stopwatch.StartNew();
        var results = new List<RecentReportItem>();
        await foreach (var item in context.Service.StreamRecentReportsAsync(false)) results.Add(item);
        Assert.InRange(watch.Elapsed.TotalSeconds, 1.8, 4);
        Assert.Equal(new[] { "p1", "p2" }, results.Select(p => p.Id));
        Assert.Equal(PractitionerAssociationStatus.Unavailable, results[0].ProviderStatus);
        Assert.Contains("2-second", results[0].ProviderError);
        Assert.Equal(PractitionerAssociationStatus.Complete, results[1].ProviderStatus);
    }

    [Fact]
    public async Task Slow_patient_lookup_is_skipped()
    {
        using var handler = new AsyncHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("Encounter"))
                return ScriptedHandler.Xml(Bundle(Encounter("slow") + Encounter("p1") + Entry(Patient)));
            await Task.Delay(Timeout.Infinite, token);
            return ScriptedHandler.Xml(Patient);
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var results = new List<PatientSummary>();
        await foreach (var patient in context.Service.StreamRecentPatientsAsync()) results.Add(patient);
        Assert.Equal("p1", Assert.Single(results).Id);
    }

    [Fact]
    public async Task Lookup_and_enrichment_share_one_budget()
    {
        using var handler = new AsyncHandler(async (request, token) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("Encounter")) return ScriptedHandler.Xml(Bundle(Encounter("p1")));
            if (path.EndsWith("/p1"))
            {
                await Task.Delay(1200, token);
                return ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='p1'/></Patient>");
            }
            await Task.Delay(1200, token);
            return ScriptedHandler.Xml(Bundle(""));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var results = new List<RecentReportItem>();
        await foreach (var item in context.Service.StreamRecentReportsAsync(false)) results.Add(item);
        Assert.Equal(PractitionerAssociationStatus.Unavailable, Assert.Single(results).ProviderStatus);
    }

    [Fact]
    public async Task Specific_search_and_provider_loading_can_each_exceed_two_seconds()
    {
        using var handler = new AsyncHandler(async (_, token) =>
        {
            await Task.Delay(2200, token);
            return ScriptedHandler.Xml(Bundle(Entry(Patient)));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        await foreach (var patient in context.Service.StreamPatientSearchAsync("mrn", null, null))
        {
            var result = await context.Service.AddPatientPractitionersAsync(patient);
            Assert.Equal(PractitionerAssociationStatus.Complete, result.PractitionerStatus);
        }
    }

    [Fact]
    public async Task Caller_cancellation_is_not_treated_as_a_timeout()
    {
        using var handler = new AsyncHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return ScriptedHandler.Xml(Bundle(""));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var patient = new PatientSummary("p1", "Test", [], null, null, null, null, null);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            context.Service.AddRecentPatientPractitionersAsync(patient, cancellation.Token));
    }

    private sealed class AsyncHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
