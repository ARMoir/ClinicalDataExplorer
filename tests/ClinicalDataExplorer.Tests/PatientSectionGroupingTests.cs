using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class PatientSectionGroupingTests
{
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
