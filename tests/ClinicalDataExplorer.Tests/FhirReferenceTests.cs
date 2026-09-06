using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class FhirReferenceTests
{
    private const string BaseUrl = "https://fhir.example.test/r4/";
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace P = "urn:clinical-data-explorer:presentation";

    [Theory]
    [InlineData("Practitioner/123")]
    [InlineData("https://fhir.example.test/r4/Practitioner/123")]
    [InlineData("Practitioner/123/_history/2")]
    public async Task References_link_to_and_load_the_exact_record(string reference)
    {
        var document = XDocument.Parse(Bundle($"<entry><resource><Patient><id value='p1'/><generalPractitioner><reference value='{reference}'/></generalPractitioner></Patient></resource></entry>"));
        FhirReferenceLinks.Annotate(document, new Uri(BaseUrl));
        var expected = new Uri(new Uri(BaseUrl), reference).AbsoluteUri;
        var link = (string?)document.Descendants(Fhir + "reference").Single().Attribute(P + "href");
        Assert.Equal("/record?reference=" + Uri.EscapeDataString(expected), link);
        var html = PatientExplorerTests.Transform("PatientResources", document.ToString());
        Assert.Contains("class=\"fhir-reference-link\"", html);
        Assert.Contains(link!, html);
        using var handler = new ScriptedHandler(request =>
        {
            Assert.Equal(expected + "?_format=xml", request.RequestUri!.AbsoluteUri);
            return ScriptedHandler.Xml("<Practitioner xmlns='http://hl7.org/fhir'><id value='123'/><name><text value='Dr Example'/></name></Practitioner>");
        });
        using var context = new FhirTestContext(BaseUrl, handler);
        var record = await context.Service.GetReferencedRecordAsync(reference);
        Assert.Equal("Practitioner", record.ResourceType);
        Assert.Contains("Dr Example", record.PageXml());
    }

    [Theory]
    [InlineData("https://other.example/r4/Patient/p1")]
    [InlineData("https://fhir.example.test/r4-other/Patient/p1")]
    [InlineData("Patient?name=Example")]
    [InlineData("Patient/p1/$everything")]
    [InlineData("javascript:alert(1)")]
    [InlineData("../Patient/p1")]
    [InlineData("urn:uuid:missing")]
    public async Task Unsupported_or_external_references_do_not_become_links_or_requests(string reference)
    {
        var document = new XDocument(new XElement(Fhir + "Bundle", new XElement(Fhir + "entry", new XElement(Fhir + "resource",
            new XElement(Fhir + "Observation", new XElement(Fhir + "subject", new XElement(Fhir + "reference", new XAttribute("value", reference), new XAttribute(P + "href", "javascript:alert(1)"))))))));
        FhirReferenceLinks.Annotate(document, new Uri(BaseUrl));
        Assert.Null(document.Descendants(Fhir + "reference").Single().Attribute(P + "href"));
        using var handler = new ScriptedHandler();
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetReferencedRecordAsync(reference));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Contained_and_bundle_full_url_references_have_matching_local_anchors()
    {
        var document = XDocument.Parse(Bundle("""
            <entry><resource><Observation><id value='o1'/>
              <contained><Practitioner><id value='local'/><name><text value='Local provider'/></name></Practitioner></contained>
              <performer><reference value='#local'/></performer><subject><reference value='urn:uuid:patient'/></subject>
            </Observation></resource></entry>
            <entry><fullUrl value='urn:uuid:patient'/><resource><Patient><id value='p1'/></Patient></resource></entry>
            """));
        FhirReferenceLinks.Annotate(document, new Uri(BaseUrl));
        var html = PatientExplorerTests.Transform("PatientResources", document.ToString());
        foreach (var reference in document.Descendants(Fhir + "reference"))
        {
            var link = (string)reference.Attribute(P + "href")!;
            Assert.StartsWith("#fhir-record-", link);
            Assert.Contains($"href=\"{link}\"", html);
            Assert.Contains($"id=\"{link[1..]}\"", html);
        }
    }

    [Fact]
    public async Task Viewer_rejects_a_different_record_returned_by_server()
    {
        using var handler = new ScriptedHandler(_ => ScriptedHandler.Xml("<Patient xmlns='http://hl7.org/fhir'><id value='wrong'/></Patient>"));
        using var context = new FhirTestContext(BaseUrl, handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Service.GetReferencedRecordAsync("Patient/p1"));
    }

    [Fact]
    public void Contained_anchors_do_not_collide_across_separately_rendered_sections()
    {
        static XDocument Section(string type) => XDocument.Parse(Bundle($"<entry><resource><{type}><id value='same'/><contained><Practitioner><id value='local'/></Practitioner></contained><performer><reference value='#local'/></performer></{type}></resource></entry>"));
        var first = Section("Observation");
        var second = Section("DiagnosticReport");
        FhirReferenceLinks.Annotate(first, new Uri(BaseUrl));
        FhirReferenceLinks.Annotate(second, new Uri(BaseUrl));
        var firstLink = (string?)first.Descendants(Fhir + "reference").Single().Attribute(P + "href");
        Assert.NotEqual(firstLink, (string?)second.Descendants(Fhir + "reference").Single().Attribute(P + "href"));
        FhirReferenceLinks.Annotate(first, new Uri(BaseUrl));
        Assert.Equal(firstLink, (string?)first.Descendants(Fhir + "reference").Single().Attribute(P + "href"));
    }
}
