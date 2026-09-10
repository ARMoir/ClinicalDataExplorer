using System.Net;
using System.Xml.Linq;
using Xunit;

namespace ClinicalDataExplorer.Tests;

// Protocol-level regression tests. These model server response shapes; they do
// not substitute for running the live suite against the deployed Microsoft server.
public sealed class FhirCompatibilityTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Encounter_observations_disambiguate_reference_type_for_strict_servers(bool report)
    {
        using var handler = new ScriptedHandler(request =>
        {
            if (!request.RequestUri!.Query.Contains("encounter:Encounter=e1", StringComparison.Ordinal))
                return ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.BadRequest);
            return ScriptedHandler.Xml(Bundle(Entry("<Observation><id value='o1'/><status value='final'/><code><text value='Pulse'/></code></Observation>")));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        if (report)
        {
            var document = XDocument.Parse(await context.Service.GetEncounterObservationBundleXmlAsync("e1"));
            Assert.Equal("o1", Assert.Single(document.Descendants(Fhir + "Observation")).Element(Fhir + "id")?.Attribute("value")?.Value);
        }
        else Assert.Equal("o1", Assert.Single(await context.Service.GetEncounterObservationsAsync("e1")).Id);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("?_getpages=opaque%2Btoken%3D&_getpagesoffset=100&_format=xml")]
    [InlineData("Observation?ct=opaque%2Btoken%3D&_count=100")]
    public async Task Observations_follow_opaque_Hapi_and_Microsoft_continuation_links(string continuation)
    {
        var next = new Uri(new Uri(BaseUrl), continuation).AbsoluteUri;
        var first = Bundle("", next); // Empty pages can still have a continuation.
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(first),
            request =>
            {
                Assert.Equal(next, request.RequestUri!.AbsoluteUri);
                return ScriptedHandler.Xml(Bundle(Entry("<Observation><id value='o1'/><status value='final'/><code><text value='Pulse'/></code><valueQuantity><value value='72'/><unit value='/min'/></valueQuantity></Observation>")));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var observations = await context.Service.GetEncounterObservationsAsync("e1");
        var observation = Assert.Single(observations);
        Assert.Equal("o1", observation.Id);
        Assert.Equal("Pulse", observation.Name);
        Assert.Equal("72 /min", observation.Value);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("https://fhir.example.test/r4?_getpages=opaque%2Btoken%3D&_getpagesoffset=100&_format=xml")]
    [InlineData("/r4?_getpages=opaque%2Btoken%3D&_getpagesoffset=100&_format=xml")]
    public async Task Paging_accepts_the_exact_base_endpoint_without_a_trailing_slash(string continuation)
    {
        var next = new Uri(new Uri(BaseUrl), continuation).AbsoluteUri;
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle("", continuation)),
            request =>
            {
                Assert.Equal(next, request.RequestUri!.AbsoluteUri);
                return ScriptedHandler.Xml(Bundle(Entry("<Observation><id value='page2'/></Observation>")));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var observation = Assert.Single(await context.Service.GetEncounterObservationsAsync("e1"));
        Assert.Equal("page2", observation.Id);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData("https://other.example.test/r4/Patient?ct=secret")]
    [InlineData("https://other.example.test/r4?ct=secret")]
    [InlineData("https://fhir.example.test:8443/r4?ct=secret")]
    [InlineData("https://fhir.example.test/r4-other?ct=secret")]
    [InlineData("https://fhir.example.test/?ct=secret")]
    [InlineData("http://fhir.example.test/r4?ct=secret")]
    [InlineData("https://fhir.example.test/r4-other/Patient?ct=secret")]
    [InlineData("http://fhir.example.test/r4/Patient?ct=secret")]
    public async Task Paging_cannot_leave_the_configured_server(string next)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle("", next)));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.SearchPatientBundleXmlAsync("test"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Discovery_reads_patient_when_include_is_omitted_or_truncated()
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(Entry("<Encounter><id value='e1'/><subject><reference value='https://fhir.example.test/r4/Patient/p1/_history/2'/></subject><period><start value='2025-01-02'/></period></Encounter>"))),
            request =>
            {
                Assert.Equal("/r4/Patient/p1", request.RequestUri!.AbsolutePath);
                return ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='p1'/><name><given value='Test'/><family value='Patient'/></name></Patient>");
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var patient = Assert.Single(await context.Service.GetPatientsWithRecentEncountersAsync());
        Assert.Equal("p1", patient.Id);
        Assert.Equal("Patient, Test", patient.DisplayName);
        Assert.Equal("PT", patient.Initials);
        Assert.NotNull(patient.MostRecentEncounter);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Discovery_preserves_case_sensitive_ids_and_tolerates_repeated_includes()
    {
        var encounters = Entry("<Encounter><id value='e1'/><subject><reference value='Patient/ABC'/></subject></Encounter>") +
            Entry("<Encounter><id value='e2'/><subject><reference value='Patient/abc'/></subject></Encounter>");
        var upper = Entry("<Patient><id value='ABC'/></Patient>", "include");
        var lower = Entry("<Patient><id value='abc'/></Patient>", "include");
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(encounters + upper + upper + lower)));
        using var context = new FhirTestContext(BaseUrl, handler);
        var patients = await context.Service.GetPatientsWithRecentEncountersAsync();
        Assert.Equal(new[] { "ABC", "abc" }, patients.Select(patient => patient.Id));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Encounter_count_uses_an_exact_summary_query()
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(Entry("<Encounter><id value='e1'/></Encounter>"))),
            request =>
            {
                Assert.Contains("encounter:Encounter=e1", request.RequestUri!.Query);
                Assert.Contains("_summary=count", request.RequestUri.Query);
                Assert.Contains("_total=accurate", request.RequestUri.Query);
                return ScriptedHandler.Xml(Bundle("<total value='12'/>"));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var encounter = Assert.Single(await context.Service.GetPatientEncountersAsync("p1"));
        Assert.Equal(12, encounter.ObservationCount);
    }

    [Fact]
    public async Task Diagnostic_reports_merge_pages_and_deduplicate_includes()
    {
        var report = Entry("<DiagnosticReport><id value='r1'/></DiagnosticReport>", "match");
        var observation = Entry("<Observation><id value='o1'/></Observation>", "include");
        var practitioner = Entry("<Practitioner><id value='pr1'/></Practitioner>", "include");
        var next = BaseUrl + "DiagnosticReport?ct=opaque%2Btoken%3D";
        using var handler = new ScriptedHandler(
            request =>
            {
                var query = Uri.UnescapeDataString(request.RequestUri!.Query);
                Assert.Contains("_include=DiagnosticReport:result", query);
                Assert.Contains("_include:iterate=Observation:performer", query);
                Assert.Contains("_include:iterate=PractitionerRole:practitioner", query);
                return ScriptedHandler.Xml(Bundle(report + observation, next));
            },
            request =>
            {
                Assert.Equal(next, request.RequestUri!.AbsoluteUri);
                return ScriptedHandler.Xml(Bundle(observation + practitioner));
            });
        using var context = new FhirTestContext(BaseUrl, handler);
        var result = await context.Service.GetDiagnosticReportBundleXmlAsync("system|report");
        var document = XDocument.Parse(result.Xml);
        Assert.Equal("1", document.Root!.Element(Fhir + "total")!.Attribute("value")!.Value);
        Assert.Equal(3, document.Root.Elements(Fhir + "entry").Count());
        Assert.Single(document.Descendants(Fhir + "Practitioner"));
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(400)] // Includes or sort unsupported with strict handling.
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Server_failures_are_not_misreported_as_empty_results(int status)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(
            "<OperationOutcome xmlns='http://hl7.org/fhir'/>", (HttpStatusCode)status));
        using var context = new FhirTestContext(BaseUrl, handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => context.Service.SearchPatientsByIdentifierAsync("test"));
        Assert.Equal((HttpStatusCode)status, error.StatusCode);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("-1")]
    public async Task Missing_or_invalid_count_is_not_displayed_as_zero(string? total)
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(Entry("<Encounter><id value='e1'/></Encounter>"))),
            _ => ScriptedHandler.Xml(Bundle(total is null ? "" : $"<total value='{total}'/>")));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetPatientEncountersAsync("p1"));
    }

    [Fact]
    public async Task Json_only_endpoint_produces_an_actionable_error()
    {
        using var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"resourceType\":\"Bundle\"}", System.Text.Encoding.UTF8, "application/fhir+json")
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.SearchPatientsByIdentifierAsync("test"));
        Assert.Contains("XML-enabled FHIR R4", error.Message);
    }

    internal static string Bundle(string entries, string? next = null) =>
        "<Bundle xmlns='http://hl7.org/fhir'><type value='searchset'/>" + entries +
        (next is null ? "" : new XElement(Fhir + "link", new XElement(Fhir + "relation", new XAttribute("value", "next")),
            new XElement(Fhir + "url", new XAttribute("value", next))).ToString()) + "</Bundle>";

    private static string Entry(string resource, string mode = "match") =>
        $"<entry><resource>{resource}</resource><search><mode value='{mode}'/></search></entry>";
}
