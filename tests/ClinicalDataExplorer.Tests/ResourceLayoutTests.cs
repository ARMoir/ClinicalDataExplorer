using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;
using static ClinicalDataExplorer.Tests.FhirCompatibilityTests;

namespace ClinicalDataExplorer.Tests;

public sealed class ResourceLayoutTests
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static XElement Record(string id, string fields) => XElement.Parse($"<Observation xmlns='http://hl7.org/fhir'><id value='{id}'/>{fields}</Observation>");

    [Fact]
    public void Sections_sort_before_pagination_and_put_undated_records_last()
    {
        var records = Enumerable.Range(1, 12).Select(day => Record($"o{day}", $"<effectiveDateTime value='2026-09-{day:00}'/>"));
        var section = new PatientResourceSection("Observation", new[] { Record("undated1", ""), Record("undated2", "") }.Concat(records).ToList());
        Assert.Equal("o12", section.Resources[0].Element(Fhir + "id")!.Attribute("value")!.Value);
        Assert.Equal(14, section.Count);
        Assert.Equal(10, XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation").Count());
        section.Page = 1;
        Assert.Equal(new[] { "o2", "o1", "undated1", "undated2" }, XDocument.Parse(section.PageXml()).Descendants(Fhir + "id").Select(id => (string)id.Attribute("value")!));
    }

    [Fact]
    public void Clinical_dates_take_precedence_over_updates_and_offsets_are_compared_as_instants()
    {
        var records = new[] {
            Record("old", "<effectiveDateTime value='2020-01-01'/><meta><lastUpdated value='2026-10-01'/></meta>"),
            Record("early", "<effectivePeriod><start value='2026-09-05T10:00:00+02:00'/></effectivePeriod>"),
            Record("later", "<effectiveDateTime value='2026-09-05T09:00:00Z'/>"),
            Record("fallback", "<effectiveDateTime value='invalid'/><meta><lastUpdated value='2026-09-06'/></meta>")
        };
        var section = new PatientResourceSection("Observation", records);
        Assert.Equal(new[] { "fallback", "later", "early", "old" }, section.Resources.Select(r => (string)r.Element(Fhir + "id")!.Attribute("value")!));
    }

    [Fact]
    public void Lab_panels_show_component_results_flags_ranges_notes_and_full_details()
    {
        var xml = Bundle("""
            <entry><resource><Observation><id value='panel'/><status value='final'/><code><text value='Blood panel'/></code>
              <effectiveDateTime value='2026-09-05T12:00:00Z'/><performer><display value='Dr. Example'/></performer>
              <component><code><text value='Potassium'/></code><valueQuantity><comparator value='&gt;'/><value value='6.5'/><unit value='mmol/L'/></valueQuantity>
                <interpretation><coding><code value='HH'/></coding></interpretation><referenceRange><text value='3.5–5.0 mmol/L'/></referenceRange></component>
              <component><code><text value='Other result'/></code><valueInteger value='0'/></component>
              <note><text value='Follow-up requested.'/></note>
            </Observation></resource></entry>
            """);
        var html = PatientExplorerTests.Transform("PatientResources", xml);
        Assert.Contains("patient-lab-results", html);
        Assert.Contains("Blood panel", html);
        Assert.Contains("Potassium", html);
        Assert.Contains("&gt;6.5</td>", html);
        Assert.Contains("class=\"lab-units\">mmol/L</td>", html);
        Assert.Contains("colspan=\"8\"", html);
        Assert.Contains("lab-critical", html);
        Assert.Contains("3.5–5.0 mmol/L", html);
        Assert.Contains(">0</td>", html);
        Assert.Contains("Dr. Example", html);
        Assert.Contains("Follow-up requested.", html);
        Assert.Contains("All record details", html);
    }

    [Theory]
    [InlineData("<unit value='mg/dL'/><code value='coded-unit'/>", "mg/dL")]
    [InlineData("<code value='mm[Hg]'/>", "mm[Hg]")]
    [InlineData("<unit value=' '/><code value='kg'/>", "kg")]
    [InlineData("", "—")]
    public void Observation_units_prefer_label_fall_back_to_code_and_leave_missing_units_unspecified(string fields, string expected)
    {
        var xml = Bundle("<entry><resource><Observation><id value='o1'/><valueQuantity><value value='12'/>" + fields + "</valueQuantity></Observation></resource></entry>");
        var html = PatientExplorerTests.Transform("PatientResources", xml);
        Assert.Contains("<th scope=\"col\">Units</th>", html);
        Assert.Contains("class=\"lab-value\">12</td>", html);
        Assert.Contains("class=\"lab-units\">" + expected + "</td>", html);
    }
}
