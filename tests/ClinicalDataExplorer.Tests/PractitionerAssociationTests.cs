using System.Net;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class PractitionerAssociationTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static readonly PatientSummary Patient = new("p1", "Patient, Test", [], null, null, null, null, null);
    private static string Entry(string resource) => $"<entry><resource>{resource}</resource></entry>";
    private const string Provider = "<Practitioner xmlns='http://hl7.org/fhir'><id value='doctor'/><name><family value='Smith'/><given value='Jane'/></name><identifier><system value='urn:npi'/><value value='123'/></identifier><identifier><system value='urn:local'/><value value='123'/><type><text value='Staff ID'/></type></identifier></Practitioner>";

    [Fact]
    public async Task All_pages_and_roles_resolve_to_one_provider_with_all_identifiers_and_sources()
    {
        using var handler = new ScriptedHandler(
            request =>
            {
                Assert.Equal("/r4/Patient/p1/$everything", request.RequestUri!.AbsolutePath);
                return ScriptedHandler.Xml(Bundle(Entry("<Patient><id value='p1'/><generalPractitioner><reference value='Practitioner/doctor'/></generalPractitioner></Patient>"), "Patient/p1/$everything?cursor=2"));
            },
            _ => ScriptedHandler.Xml(Bundle(Entry("<Observation><id value='o1'/><performer><reference value='PractitionerRole/role'/></performer></Observation>") +
                Entry("<Encounter><id value='e1'/><participant><individual><reference value='https://fhir.example.test/r4/Practitioner/doctor/_history/2'/></individual></participant></Encounter>"))),
            request => { Assert.Equal("/r4/Practitioner/doctor", request.RequestUri!.AbsolutePath); return ScriptedHandler.Xml(Provider); },
            request => { Assert.Equal("/r4/PractitionerRole/role", request.RequestUri!.AbsolutePath); return ScriptedHandler.Xml("<PractitionerRole xmlns='http://hl7.org/fhir'><id value='role'/><practitioner><reference value='Practitioner/doctor'/></practitioner></PractitionerRole>"); });
        using var context = new FhirTestContext(BaseUrl, handler);
        var patient = await context.Service.AddPatientPractitionersAsync(Patient);
        var provider = Assert.Single(patient.Practitioners);
        Assert.Equal("Smith, Jane", provider.Name);
        Assert.Equal("Practitioner/doctor", provider.Reference);
        Assert.True(provider.IsResolved);
        Assert.Null(patient.PractitionerLookupError);
        Assert.Equal(PractitionerAssociationStatus.Complete, patient.PractitionerStatus);
        Assert.Equal(new[] { "urn:npi", "urn:local" }, provider.Identifiers.Select(i => i.System));
        Assert.Equal(3, provider.Sources.Count);
        Assert.Equal(4, handler.Calls);
    }

    [Fact]
    public async Task Included_provider_is_reused_and_unreferenced_provider_is_not_associated()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(
            Entry("<Patient><id value='p1'/><generalPractitioner><reference value='Practitioner/doctor'/></generalPractitioner></Patient>") +
            Entry(Provider) + Entry("<Practitioner><id value='unrelated'/></Practitioner>"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.AddPatientPractitionersAsync(Patient);
        Assert.Equal("Practitioner/doctor", Assert.Single(result.Practitioners).Reference);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<identifier><system value='urn:local'/></identifier>")]
    public async Task Provider_without_identifier_value_uses_server_scoped_resource_id(string identifier)
    {
        var bundle = Bundle(Entry("<Patient><id value='p1'/><generalPractitioner><reference value='Practitioner/138072247'/></generalPractitioner></Patient>") +
            Entry($"<Practitioner><id value='138072247'/><name><text value='Dr. Test Pediatri'/></name>{identifier}</Practitioner>"));
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(bundle), _ => ScriptedHandler.Xml(bundle));
        using var context = new FhirTestContext(BaseUrl, handler);
        var patient = await context.Service.AddPatientPractitionersAsync(Patient);
        var fallback = Assert.Single(Assert.Single(patient.Practitioners).Identifiers);
        Assert.Equal("138072247", fallback.Value);
        Assert.Equal("Provider ID", fallback.Label);
        Assert.Equal(BaseUrl + "Practitioner", fallback.System);
        Assert.True(fallback.IsResourceId);
        var sections = await context.Service.GetPatientResourceSectionsAsync("p1");
        var html = PatientExplorerTests.Transform("PatientResources", sections.Single(s => s.ResourceType == "Patient").PageXml());
        Assert.Contains("Provider ID: 138072247", html);
        Assert.DoesNotContain("No identifiers recorded", html);
    }

    [Theory]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(503)]
    public async Task Failed_reads_remain_visible_as_unresolved_associations(int status)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry(
            "<Patient><id value='p1'/><generalPractitioner><reference value='Practitioner/doctor'/></generalPractitioner></Patient>"))),
            _ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", (HttpStatusCode)status));
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.AddPatientPractitionersAsync(Patient);
        Assert.False(Assert.Single(result.Practitioners).IsResolved);
        Assert.NotNull(result.PractitionerLookupError);
        Assert.Equal(PractitionerAssociationStatus.Incomplete, result.PractitionerStatus);
    }

    [Fact]
    public async Task External_provider_references_are_not_fetched()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry(
            "<Patient><id value='p1'/><generalPractitioner><reference value='https://other.example/Practitioner/doctor'/></generalPractitioner></Patient>"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.AddPatientPractitionersAsync(Patient);
        Assert.False(Assert.Single(result.Practitioners).IsResolved);
        Assert.NotNull(result.PractitionerLookupError);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Unsupported_everything_is_not_reported_as_no_associations()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.NotImplemented));
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.AddPatientPractitionersAsync(Patient);
        Assert.NotNull(result.PractitionerLookupError);
        Assert.Equal(Patient.Id, result.Id);
        Assert.Equal(PractitionerAssociationStatus.Unavailable, result.PractitionerStatus);
    }

    [Fact]
    public async Task Patient_report_resolves_provider_details_without_replacing_source_fields()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(Entry(
            "<ServiceRequest><id value='order'/><requester><reference value='Practitioner/doctor'/><display value='Original display'/></requester></ServiceRequest>"))),
            _ => ScriptedHandler.Xml(Provider));
        using var context = new FhirTestContext(BaseUrl, handler);
        var section = Assert.Single(await context.Service.GetPatientResourceSectionsAsync("p1"));
        var html = PatientExplorerTests.Transform("PatientResources", section.PageXml());
        var visible = html.Split("<details", 2)[0];
        Assert.Contains("Smith, Jane", visible);
        Assert.Contains("123 [urn:npi]", visible);
        Assert.Contains("Staff ID: 123 [urn:local]", visible);
        Assert.Contains("Original display", section.PageXml());
    }

    [Fact]
    public async Task Encounter_details_resolve_names_and_identifiers()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(
            "<Encounter xmlns='http://hl7.org/fhir'><id value='e1'/><participant><individual><reference value='Practitioner/doctor'/></individual></participant></Encounter>"),
            _ => ScriptedHandler.Xml(Provider));
        using var context = new FhirTestContext(BaseUrl, handler);
        var html = PatientExplorerTests.Transform("EncounterDetails", await context.Service.GetEncounterXmlAsync("e1"));
        Assert.Contains("Smith, Jane", html);
        Assert.Contains("123 [urn:npi]", html);
    }

    [Fact]
    public async Task Diagnostic_report_and_observation_list_show_resolved_identifiers()
    {
        var bundle = Bundle(Entry("<DiagnosticReport><id value='r1'/></DiagnosticReport>") +
            Entry("<Observation><id value='o1'/><valueString value='Normal'/><performer><reference value='Practitioner/doctor'/><display value='Old display'/></performer></Observation>") + Entry(Provider));
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(bundle), _ => ScriptedHandler.Xml(bundle));
        using var context = new FhirTestContext(BaseUrl, handler);
        var report = await context.Service.GetDiagnosticReportBundleXmlAsync("r1");
        var observations = await context.Service.GetEncounterObservationBundleXmlAsync("e1");
        foreach (var html in new[] { PatientExplorerTests.Transform("DiagnosticReportBundle", report.Xml), PatientExplorerTests.Transform("ObservationList", observations) })
        {
            Assert.Contains("Smith, Jane", html);
            Assert.Contains("123 [urn:npi]", html);
            Assert.DoesNotContain("Old display", html);
        }
        Assert.Equal(2, handler.Calls);
    }
}
