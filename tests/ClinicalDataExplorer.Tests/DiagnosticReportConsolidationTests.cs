using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class DiagnosticReportConsolidationTests
{
    private static readonly XNamespace F = "http://hl7.org/fhir", P = "urn:clinical-data-explorer:presentation";
    private const string Observation = "<Observation><id value='o1'/><code><text value='Example test'/></code><effectiveDateTime value='2026-09-01T10:00:00Z'/><valueString value='FINDINGS&#10;Clear &lt;script&gt;'/></Observation>";
    private static string Report(string result = "", string name = "Example test", string date = "2026-09-01", string id = "r1") => $"<DiagnosticReport><id value='{id}'/><code><text value='{name}'/></code><effectiveDateTime value='{date}'/>{result}</DiagnosticReport>";
    private static XDocument Bundle(params string[] records) => XDocument.Parse("<Bundle xmlns='http://hl7.org/fhir'>" + string.Join("", records.Select(r => "<entry><resource>" + r + "</resource></entry>")) + "</Bundle>");
    private static IReadOnlyList<XElement> Group(XDocument bundle) => DiagnosticReportConsolidation.Consolidate(bundle, new Uri("https://fhir.example.test/r4/"));

    [Theory]
    [InlineData("Observation/o1")]
    [InlineData("https://fhir.example.test/r4/Observation/o1")]
    public void Explicit_reference_takes_priority_over_name_and_date(string reference)
    {
        var bundle = Bundle(Observation, Report($"<result><reference value='{reference}'/></result>", "Different name", "2026-09-02"), Report(id: "r2"));
        var original = bundle.ToString();
        var resources = Group(bundle);
        Assert.Equal(2, resources.Count);
        Assert.Single(resources[0].Descendants(P + "item"));
        Assert.Empty(resources[1].Descendants(P + "item"));
        Assert.Equal(original, bundle.ToString());
    }

    [Fact]
    public void Unique_name_and_clinical_day_match_consolidates_and_preserves_narrative()
    {
        var result = Assert.Single(Group(Bundle(Observation, Report(name: "  EXAMPLE   test ", date: "2026-09-01T16:00:00Z"))));
        Assert.Equal("name and date", (string?)Assert.Single(result.Descendants(P + "item")).Attribute("match"));
        var html = PatientExplorerTests.Transform("PatientResources", new PatientResourceSection("DiagnosticReport", [result]).PageXml());
        Assert.Contains("Included observation:", html);
        Assert.Contains("Matched by name and date", html);
        Assert.Contains("Clear &lt;script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("All included observation details", html);
    }

    [Fact]
    public void Ambiguous_or_different_day_matches_remain_separate()
    {
        Assert.Equal(3, Group(Bundle(Observation, Report(), Report(id: "r2"))).Count);
        Assert.Equal(2, Group(Bundle(Observation, Report(date: "2026-09-02"))).Count);
        Assert.Equal(2, Group(Bundle(Observation, Report(date: "2026-09"))).Count);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("encounter")]
    public void Conflicting_context_does_not_merge(string field)
    {
        var observation = Observation.Replace("</Observation>", $"<{field}><reference value='Patient/one'/></{field}></Observation>");
        var report = Report().Replace("</DiagnosticReport>", $"<{field}><reference value='Patient/two'/></{field}></DiagnosticReport>");
        Assert.Equal(2, Group(Bundle(observation, report)).Count);
    }

    [Fact]
    public void Different_version_is_not_overridden_by_name_match()
    {
        Assert.Equal(2, Group(Bundle(Observation, Report("<result><reference value='Observation/o1/_history/2'/></result>"))).Count);
    }

    [Fact]
    public void Full_url_links_match_and_scalar_observations_stay_in_their_section()
    {
        var bundle = Bundle(Observation, Report("<result><reference value='urn:uuid:example'/></result>", "Different"), "<Observation><id value='scalar'/><valueQuantity><value value='12'/></valueQuantity></Observation>");
        bundle.Root!.Elements(F + "entry").First().AddFirst(new XElement(F + "fullUrl", new XAttribute("value", "urn:uuid:example")));
        var resources = Group(bundle);
        Assert.Equal(2, resources.Count);
        Assert.Single(resources[0].Descendants(P + "item"));
        Assert.Equal("scalar", (string?)resources[1].Element(F + "id")?.Attribute("value"));
    }
}
