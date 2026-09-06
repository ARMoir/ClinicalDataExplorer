using System.Net;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class RecentReportTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Entry(string resource) => $"<entry><resource>{resource}</resource></entry>";
    private static string Report(string id) => $"<DiagnosticReport><id value='{id}'/><code><text value='Lab report'/></code><subject><reference value='Patient/p1'/></subject><performer><reference value='Practitioner/doctor'/></performer></DiagnosticReport>";
    private const string Doctor = "<Practitioner><id value='doctor'/><name><text value='Dr Example'/></name><identifier><system value='urn:staff'/><value value='staff1'/></identifier></Practitioner>";

    [Fact]
    public async Task Recent_diagnostics_follow_pages_deduplicate_and_resolve_provider_identifiers()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Contains("_sort=-date", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle(Entry(Report("r1")), "DiagnosticReport?cursor=2"));
        }, request =>
        {
            Assert.Contains("_id=r1", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle(Entry(Report("r1")) + Entry(Doctor)));
        }, _ => ScriptedHandler.Xml(Bundle(Entry(Report("r1")) + Entry(Report("r2")))), request =>
        {
            Assert.Contains("_id=r2", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle(Entry(Report("r2")) + Entry(Doctor)));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var items = new List<RecentReportItem>();
        await foreach (var item in context.Service.StreamRecentReportsAsync(true)) items.Add(item);
        Assert.Equal(new[] { "r1", "r2" }, items.Select(i => i.Id));
        Assert.All(items, item =>
        {
            Assert.Equal(PractitionerAssociationStatus.Complete, item.ProviderStatus);
            var provider = Assert.Single(item.Providers);
            Assert.Equal("staff1", Assert.Single(provider.Identifiers).Value);
            Assert.Equal("urn:staff", provider.Identifiers[0].System);
            Assert.NotEmpty(provider.Sources);
            Assert.Equal("/reports?reportId=" + item.Id, item.Url);
        });
    }

    [Fact]
    public async Task Recent_patient_reports_have_full_patient_provider_associations()
    {
        var patient = "<Patient><id value='p1'/><name><family value='Example'/><given value='Test'/></name><generalPractitioner><reference value='Practitioner/doctor'/></generalPractitioner></Patient>";
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry(patient) + Entry("<Encounter><id value='e1'/><subject><reference value='Patient/p1'/></subject></Encounter>"))),
            request =>
            {
                Assert.Equal("/r4/Patient/p1/$everything", request.RequestUri!.AbsolutePath);
                return ScriptedHandler.Xml(Bundle(Entry(patient) + Entry(Doctor)));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var items = new List<RecentReportItem>();
        await foreach (var item in context.Service.StreamRecentReportsAsync(false)) items.Add(item);
        var result = Assert.Single(items);
        Assert.Equal("Example, Test", result.Title);
        Assert.Equal("/patient/p1", result.Url);
        Assert.Equal("staff1", Assert.Single(Assert.Single(result.Providers).Identifiers).Value);
    }

    [Fact]
    public async Task Failed_association_lookup_keeps_recent_report_and_explicit_unavailable_status()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry(Report("r1")))),
            _ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.Forbidden));
        using var context = new FhirTestContext(BaseUrl, handler);
        var items = new List<RecentReportItem>();
        await foreach (var item in context.Service.StreamRecentReportsAsync(true)) items.Add(item);
        var result = Assert.Single(items);
        Assert.Equal(PractitionerAssociationStatus.Unavailable, result.ProviderStatus);
        Assert.NotNull(result.ProviderError);
        Assert.Equal("r1", result.Id);
    }

    [Fact]
    public async Task Diagnostic_report_without_identifier_can_be_opened_by_id()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Contains("_id=r1", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle(Entry(Report("r1")) + Entry(Doctor)));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        Assert.Contains("Dr Example", (await context.Service.GetDiagnosticReportByIdAsync("r1")).Xml);
    }
}
