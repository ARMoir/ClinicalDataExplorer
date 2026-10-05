using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientSectionGroupingTests
{
    private static XElement SharedRecord(string type, string id, string encounter, string fields) =>
        XElement.Parse($"<" + type + " xmlns='http://hl7.org/fhir'>" +
            $"<id value='{id}'/><meta><versionId value='{id}'/><lastUpdated value='2026-10-01'/></meta>" +
            $"<extension url='urn:example:encounter'><valueReference><reference value='Encounter/{encounter}'/><display value='{encounter}'/></valueReference></extension>" +
            fields + "</" + type + ">");

    [Theory]
    [InlineData("Coverage", "<status value='active'/><subscriberId value='policy-1'/>")]
    [InlineData("RelatedPerson", "<patient><reference value='Patient/p1'/></patient><name><text value='Jane Doe'/></name>")]
    public void Shared_details_group_across_encounters_and_preserve_originals(string type, string fields)
    {
        var first = SharedRecord(type, "r1", "e1", fields);
        var second = SharedRecord(type, "r2", "e2", fields);
        var original = second.ToString();
        var section = Assert.Single(PatientSectionGrouping.Merge([first], [second]));
        Assert.Equal(1, section.Count);
        XNamespace p = "urn:clinical-data-explorer:presentation";
        Assert.Equal(2, section.Resources[0].Element(p + "sharedRecords")!.Elements().Count());
        Assert.Equal(original, second.ToString());
        var html = PatientExplorerTests.Transform("PatientResources", section.PageXml());
        Assert.Contains("Shared details across 2 records", html);
        Assert.Contains("Original records and encounter associations", html);
        Assert.Contains("Encounter/e1", html);
        Assert.Contains("Encounter/e2", html);
        Assert.Contains("r2", html);
    }

    [Theory]
    [InlineData("Coverage", "<subscriberId value='different'/>")]
    [InlineData("Coverage", "<period><end value='2026-10-01'/></period>")]
    [InlineData("Coverage", "<identifier><value value='different'/></identifier>")]
    [InlineData("RelatedPerson", "<relationship><text value='Parent'/></relationship>")]
    [InlineData("RelatedPerson", "<telecom><value value='555-0100'/></telecom>")]
    [InlineData("RelatedPerson", "<extension url='urn:other'><valueString value='different'/></extension>")]
    public void Different_details_remain_separate(string type, string difference)
    {
        var section = Assert.Single(PatientSectionGrouping.Merge(
            [SharedRecord(type, "r1", "e1", ""), SharedRecord(type, "r2", "e2", difference)], []));
        Assert.Equal(2, section.Count);
    }

    [Fact]
    public void Other_resource_types_are_not_grouped_by_content()
    {
        var section = Assert.Single(PatientSectionGrouping.Merge(
            [SharedRecord("Observation", "r1", "e1", ""), SharedRecord("Observation", "r2", "e2", "")], []));
        Assert.Equal(2, section.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<result><reference value='Observation/o1'/></result>")]
    public void Narrative_is_a_document_even_when_report_name_date_or_reference_matches(string result)
    {
        var bundle = XDocument.Parse("<Bundle xmlns='http://hl7.org/fhir'>" +
            "<entry><resource><Observation><id value='o1'/><code><text value='Example'/></code><effectiveDateTime value='2026-09-01'/><valueString value='FINDINGS&#10;Clear'/></Observation></resource></entry>" +
            "<entry><resource><DiagnosticReport><id value='r1'/><code><text value='Example'/></code><effectiveDateTime value='2026-09-01'/>" + result + "</DiagnosticReport></resource></entry>" +
            "<entry><resource><DocumentReference><id value='d1'/></DocumentReference></resource></entry>" +
            "<entry><resource><Observation><id value='scalar'/><valueQuantity><value value='12'/></valueQuantity></Observation></resource></entry></Bundle>");
        var original = bundle.ToString();
        var sections = PatientSectionGrouping.Build(bundle);
        var documents = Assert.Single(sections, s => s.ResourceType == "DocumentReference");
        Assert.Equal("Documents", documents.Label);
        Assert.Equal(2, documents.Count);
        Assert.Single(documents.Resources, r => r.Name.LocalName == "Observation");
        Assert.Equal(1, Assert.Single(sections, s => s.ResourceType == "DiagnosticReport").Count);
        Assert.Equal(1, Assert.Single(sections, s => s.ResourceType == "Observation").Count);
        Assert.Equal(original, bundle.ToString());
        Assert.DoesNotContain("consolidatedObservations", string.Join("", sections.Select(s => s.PageXml())));
    }
}
