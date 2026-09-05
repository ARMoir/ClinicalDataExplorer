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

    [Fact]
    public void Demographics_render_branding_and_unknown_status()
    {
        var html = Transform("PatientDetails", "<Patient xmlns='http://hl7.org/fhir'><id value='p1'/></Patient>");
        Assert.Contains("/uploads/test.png", html);
        Assert.Contains("Test facility", html);
        Assert.Contains("#113B75", html);
        Assert.Contains("Not recorded", html);
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
