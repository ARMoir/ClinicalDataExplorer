using System.Net;
using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class ReportContextTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Report(string patient = "Patient/p1") => Bundle($"<entry><resource><DiagnosticReport><id value='r1'/><subject><reference value='{patient}'/></subject><encounter><reference value='https://fhir.example.test/r4/Encounter/e1'/></encounter><result><reference value='Observation/o1'/></result></DiagnosticReport></resource></entry>");

    [Fact]
    public async Task Report_loads_patient_demographics_and_links_patient_and_encounter()
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Equal("/r4/Patient/p1", request.RequestUri!.AbsolutePath);
            return ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='p1'/><birthDate value='1980-01-02'/></Patient>");
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetReportContextAsync(Report());
        Assert.Contains("1980-01-02", result.PatientXml);
        Assert.Equal("/patient/p1", result.PatientUrl);
        Assert.Equal("/encounter/e1", result.EncounterUrl);
        Assert.Null(result.PatientError);
    }

    [Fact]
    public async Task Missing_patient_keeps_report_navigation_with_clear_error()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.NotFound));
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetReportContextAsync(Report());
        Assert.Null(result.PatientXml);
        Assert.NotNull(result.PatientError);
        Assert.Equal("/encounter/e1", result.EncounterUrl);
    }

    [Fact]
    public async Task External_patient_reference_is_not_fetched()
    {
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetReportContextAsync(Report("https://other.example/Patient/p1"));
        Assert.Null(result.PatientUrl);
        Assert.NotNull(result.PatientError);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Reports_render_clickable_patient_encounter_and_result_references()
    {
        var document = XDocument.Parse(Report());
        FhirReferenceLinks.Annotate(document, new Uri(BaseUrl), localTargets: false);
        var html = PatientExplorerTests.Transform("DiagnosticReportBundle", document.ToString());
        foreach (var reference in new[] { "Patient/p1", "Encounter/e1", "Observation/o1" })
            Assert.Contains("/record?reference=" + Uri.EscapeDataString(BaseUrl + reference), html);
    }
}
