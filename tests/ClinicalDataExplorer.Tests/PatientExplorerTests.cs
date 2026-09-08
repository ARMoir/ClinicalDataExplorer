using System.Net;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientExplorerTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static string Entry(string resource) => $"<entry><resource>{resource}</resource></entry>";
    private static string Patient(int i) => $"<Patient><id value='p{i}'/><name><family value='Example'/><given value='Test{i}'/></name></Patient>";

    [Fact]
    public async Task Recent_patients_continue_beyond_ten_and_deduplicate_across_pages()
    {
        static string Enc(int i) => Entry($"<Encounter><id value='e{i}'/><subject><reference value='Patient/p{i}'/></subject></Encounter>");
        var first = string.Concat(Enumerable.Range(1, 10).Select(i => Enc(i) + Entry(Patient(i))));
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(first, "Encounter?cursor=2")),
            _ => ScriptedHandler.Xml(Bundle(Enc(1) + Enc(11) + Entry(Patient(1)) + Entry(Patient(11)))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var ids = new List<string>();
        await foreach (var patient in context.Service.StreamRecentPatientsAsync()) ids.Add(patient.Id);
        Assert.Equal(Enumerable.Range(1, 11).Select(i => $"p{i}"), ids);
        Assert.Equal(2, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Recent_patients_skip_missing_records_on_second_page(HttpStatusCode status)
    {
        static string Enc(string id) => Entry($"<Encounter><subject><reference value='Patient/{id}'/></subject></Encounter>");
        var first = string.Concat(Enumerable.Range(1, 10).Select(i => Enc($"p{i}") + Entry(Patient(i))));
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(first, "Encounter?cursor=2")),
            request =>
            {
                Assert.Equal("/r4/Encounter", request.RequestUri!.AbsolutePath);
                Assert.Equal("?cursor=2", request.RequestUri.Query);
                return ScriptedHandler.Xml(Bundle(Enc("missing") + Enc("p11") + Entry(Patient(11)), "Encounter?cursor=3"));
            },
            request =>
            {
                Assert.Equal("/r4/Patient/missing", request.RequestUri!.AbsolutePath);
                return ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", status);
            },
            _ => ScriptedHandler.Xml(Bundle(Enc("missing") + Enc("p12") + Entry(Patient(12)))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var ids = new List<string>();
        await foreach (var patient in context.Service.StreamRecentPatientsAsync()) ids.Add(patient.Id);
        Assert.Equal(Enumerable.Range(1, 12).Select(i => $"p{i}"), ids);
        Assert.Equal(4, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Recent_patients_do_not_hide_other_patient_lookup_errors(HttpStatusCode status)
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(Entry("<Encounter><subject><reference value='Patient/p1'/></subject></Encounter>"))),
            _ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", status));
        using var context = new FhirTestContext(BaseUrl, handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var patient in context.Service.StreamRecentPatientsAsync()) { }
        });
        Assert.Equal(status, error.StatusCode);
    }

    [Fact]
    public async Task Recent_patients_do_not_hide_a_missing_next_page()
    {
        using var handler = new ScriptedHandler(
            _ => ScriptedHandler.Xml(Bundle(Entry(Patient(1)), "Encounter?cursor=2")),
            _ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.NotFound));
        using var context = new FhirTestContext(BaseUrl, handler);
        var error = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var patient in context.Service.StreamRecentPatientsAsync()) { }
        });
        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
    }

    [Theory]
    [InlineData(null, "O'Neil, Jr", "Alex", "family=O'Neil\\, Jr&given=Alex")]
    [InlineData("MRN|123", null, null, "identifier=MRN\\|123")]
    [InlineData(null, null, "Alex", "given=Alex")]
    public async Task Search_encodes_literal_input_and_follows_next_pages(string? identifier, string? family, string? given, string expected)
    {
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Contains(expected, Uri.UnescapeDataString(request.RequestUri!.Query));
            return ScriptedHandler.Xml(Bundle(Entry(Patient(1)), "Patient?cursor=next"));
        }, request =>
        {
            Assert.Equal("?cursor=next", request.RequestUri!.Query);
            return ScriptedHandler.Xml(Bundle(Entry(Patient(1)) + Entry(Patient(2))));
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var ids = new List<string>();
        await foreach (var patient in context.Service.StreamPatientSearchAsync(identifier, family, given)) ids.Add(patient.Id);
        Assert.Equal(new[] { "p1", "p2" }, ids);
    }

    [Fact]
    public async Task Everything_counts_all_pages_and_includes_supporting_and_unusual_types()
    {
        var observation = Entry("<Observation><id value='o1'/><component><valueString value='nested result'/></component></Observation>");
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Equal("/r4/Patient/p1/$everything", request.RequestUri!.AbsolutePath);
            return ScriptedHandler.Xml(Bundle(Entry(Patient(1)) + observation, "Patient/p1/$everything?cursor=2"));
        }, _ => ScriptedHandler.Xml(Bundle(observation + Entry("<Practitioner><id value='pr1'/></Practitioner>") + Entry("<Task><id value='t1'/></Task>"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var sections = await context.Service.GetPatientResourceSectionsAsync("p1");
        Assert.Equal(new[] { "Observation", "Patient", "Practitioner", "Task" }, sections.Select(s => s.ResourceType));
        Assert.All(sections, s => Assert.Equal(1, s.Count));
        Assert.Contains("nested result", sections[0].PageXml());
    }

    [Fact]
    public async Task Unsupported_everything_is_an_error_not_an_empty_record()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<OperationOutcome xmlns='http://hl7.org/fhir'/>", HttpStatusCode.NotImplemented));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => context.Service.GetPatientResourceSectionsAsync("p1"));
    }

    [Theory]
    [InlineData(@"\n")]
    [InlineData("&#10;")]
    public async Task Multiline_observations_are_formatted_in_diagnostic_reports(string separator)
    {
        var text = $"FINDINGS{separator}Result: Clear{separator}{separator}-----{separator}&lt;script&gt;";
        var report = Entry($"<Observation><id value='report'/><valueString value='{text}'/></Observation>");
        var scalar = Entry("<Observation><id value='scalar'/><valueString value='Clear'/></Observation>");
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle(report + scalar,
            "Patient/p1/$everything?cursor=2")), _ => ScriptedHandler.Xml(Bundle(report +
                Entry("<DiagnosticReport><id value='diagnostic'/></DiagnosticReport>"))));
        using var context = new FhirTestContext(BaseUrl, handler);
        var sections = await context.Service.GetPatientResourceSectionsAsync("p1");
        var reports = Assert.Single(sections, s => s.ResourceType == "DiagnosticReport");
        Assert.Equal(2, reports.Count);
        var observations = Assert.Single(sections, s => s.ResourceType == "Observation");
        Assert.Equal("scalar", Assert.Single(observations.Resources).Element(XName.Get("id", "http://hl7.org/fhir"))!.Attribute("value")!.Value);
        var html = Transform("PatientResources", reports.PageXml());
        var body = html.Split("<div class=\"clinical-document\">", 2)[1].Split("<details", 2)[0];
        Assert.Contains("class=\"document-heading\">FINDINGS</div>", body);
        Assert.Contains("<strong class=\"document-label\">Result:</strong>", body);
        Assert.Contains("class=\"document-blank\"", body);
        Assert.Contains("class=\"document-rule\"", body);
        Assert.Contains("&lt;script&gt;", body);
        Assert.DoesNotContain(@"\n", body);
        Assert.DoesNotContain("<script>", body);
        Assert.DoesNotContain("class=\"observation-value\"", html);
    }

    [Theory]
    [InlineData("<OperationOutcome xmlns='http://hl7.org/fhir'/>")]
    [InlineData("<Bundle xmlns='http://hl7.org/fhir'><entry><resource><OperationOutcome><issue><severity value='error'/></issue></OperationOutcome></resource></entry></Bundle>")]
    public async Task Outcome_does_not_become_zero_counts(string xml)
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(xml));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetPatientResourceSectionsAsync("p1"));
    }

    [Fact]
    public async Task Everything_rejects_external_continuation()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml(Bundle("", "https://other.example/Patient/p1/$everything")));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetPatientResourceSectionsAsync("p1"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void Resource_pages_preserve_full_count_and_nested_fields_and_escape_html()
    {
        XNamespace f = "http://hl7.org/fhir";
        var records = Enumerable.Range(1, 11).Select(i => new XElement(f + "Observation", new XElement(f + "id", new XAttribute("value", $"o{i}")),
            new XElement(f + "component", new XElement(f + "valueString", new XAttribute("value", "<script>alert(1)</script>"))))).ToList();
        var section = new PatientResourceSection("Observation", records);
        Assert.Equal(10, XDocument.Parse(section.PageXml()).Descendants(f + "Observation").Count());
        section.Page = 1;
        Assert.Equal("o11", XDocument.Parse(section.PageXml()).Descendants(f + "id").Single().Attribute("value")!.Value);
        Assert.Equal(11, section.Count);
        var html = Transform("PatientResources", section.PageXml());
        Assert.Contains("component", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Theory]
    [InlineData("", "active")]
    [InlineData("<active value='true'/>", "active")]
    [InlineData("<active value='false'/>", "inactive")]
    public void Demographics_render_branding_and_default_to_active(string active, string expectedStatus)
    {
        var html = Transform("PatientDetails", $"<Patient xmlns='http://hl7.org/fhir'><id value='p1'/>{active}</Patient>");
        Assert.Contains("/uploads/test.png", html);
        Assert.Contains("Test facility", html);
        Assert.Contains("#113B75", html);
        Assert.Contains("Not recorded", html);
        Assert.Contains($"<span class=\"status-chip\">{expectedStatus}</span>", html);
    }

    [Theory]
    [InlineData("Coverage", "<payor><reference value='Organization/1'/><display value='Example Health'/></payor><period><start value='2026-01-01'/><end value='2026-12-31'/></period>", "Payer", "Example Health", "2026-12-31")]
    [InlineData("Appointment", "<start value='2026-09-06T09:00:00Z'/><participant><actor><display value='Dr Example'/></actor><status value='accepted'/></participant>", "Start", "2026-09-06T09:00:00Z", "Dr Example")]
    [InlineData("MedicationRequest", "<medicationCodeableConcept><text value='Example medication'/></medicationCodeableConcept><dosageInstruction><text value='Take with food'/></dosageInstruction>", "Medication", "Example medication", "Take with food")]
    [InlineData("Observation", "<interpretation><coding><code value='H'/></coding></interpretation><referenceRange><low><value value='3'/><unit value='mg/L'/></low><high><value value='10'/><unit value='mg/L'/></high></referenceRange>", "Interpretation", "H", "10 mg/L")]
    [InlineData("PractitionerRole", "<practitioner><reference value='Practitioner/123'/></practitioner><specialty><coding><display value='Family medicine'/></coding></specialty>", "Provider", "Practitioner/123", "Family medicine")]
    [InlineData("Condition", "<clinicalStatus><coding><display value='Active'/></coding></clinicalStatus><note><text value='&lt;script&gt;example&lt;/script&gt;'/></note>", "Clinical status", "Active", "&lt;script&gt;example&lt;/script&gt;")]
    public void Pertinent_fields_are_visible_before_full_details(string type, string fields, string label, string first, string second)
    {
        var html = Transform("PatientResources", Bundle(Entry($"<{type}><id value='test'/>{fields}</{type}>")));
        var visible = html.Split("<details", 2)[0];
        if (type == "Observation") Assert.Contains("<th scope=\"col\">Flag</th>", visible);
        else Assert.Contains($"<dt><strong>{label}:</strong></dt>", visible);
        Assert.Contains(first, visible);
        Assert.Contains(second, visible);
        Assert.DoesNotContain("<script>", visible);
        Assert.Contains("All record details", html);
    }

    internal static string Transform(string template, string xml)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "XSLT"))) root = root.Parent;
        Assert.NotNull(root);
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(root.FullName, "XSLT", template + ".xslt"), new XsltSettings(false, false), null);
        var args = new XsltArgumentList();
        args.AddParam("facilityName", "", "Test facility"); args.AddParam("logoPath", "", "/uploads/test.png");
        args.AddParam("primaryColor", "", "#113B75"); args.AddParam("secondaryColor", "", "#006443");
        using var reader = XmlReader.Create(new StringReader(xml));
        using var output = new StringWriter();
        transform.Transform(reader, args, output);
        return output.ToString();
    }
}
