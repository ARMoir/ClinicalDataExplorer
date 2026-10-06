using System.Xml.Linq;
using ClinicalDataExplorer.Models;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class ObservationFilterTests
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";

    private static XElement Observation(string id, string? status) => new(Fhir + "Observation",
        new XElement(Fhir + "id", new XAttribute("value", id)),
        status is null ? null : new XElement(Fhir + "status", new XAttribute("value", status)));

    [Theory]
    [InlineData("final")]
    [InlineData("complete")]
    [InlineData("signed")]
    [InlineData("Final")]
    [InlineData("Complete")]
    [InlineData("Signed")]
    public void Filter_includes_each_requested_status(string status)
    {
        var section = new PatientResourceSection("Observation", [Observation("result", status)])
            { FinalizedResultsOnly = true };
        Assert.Equal(1, section.Count);
        Assert.Single(XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation"));
    }

    [Fact]
    public void Filter_applies_before_paging_and_restores_all_statuses_when_disabled()
    {
        var records = Enumerable.Range(1, 11).Select(i => Observation($"final-{i}", "final"))
            .Concat(new[] { "preliminary", "amended", "corrected", "cancelled", "entered-in-error", null }
                .Select((status, i) => Observation($"other-{i}", status))).ToList();
        var section = new PatientResourceSection("Observation", records) { Page = 1, FinalizedResultsOnly = true };

        Assert.Equal(0, section.Page);
        Assert.Equal(11, section.Count);
        Assert.Equal(10, XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation").Count());
        Assert.All(XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation"),
            r => Assert.Equal("final", (string?)r.Element(Fhir + "status")?.Attribute("value")));
        section.Page = 1;
        Assert.Single(XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation"));

        section.Html = "cached";
        section.FinalizedResultsOnly = false;
        Assert.Equal(0, section.Page);
        Assert.Null(section.Html);
        Assert.Equal(records.Count, section.Count);
        Assert.Equal(records.Count, section.Resources.Count);
    }

    [Fact]
    public void Newly_received_records_respect_filter_and_empty_filtered_pages_are_clamped()
    {
        var section = new PatientResourceSection("Observation", [Observation("pending", "preliminary")])
            { FinalizedResultsOnly = true };
        Assert.Equal(0, section.Count);
        Assert.Empty(XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation"));
        section.UpdateResources([.. section.Resources, Observation("complete", "final")]);
        Assert.True(section.FinalizedResultsOnly);
        Assert.Equal(1, section.Count);
        section.Page = 1;
        section.UpdateResources([Observation("pending", "preliminary")]);
        Assert.Equal(0, section.Page);
        section.FinalizedResultsOnly = false;
        Assert.Equal(1, section.Count);
    }

    [Fact]
    public void Filter_does_not_change_document_observations()
    {
        var section = new PatientResourceSection("DocumentReference", [Observation("narrative", "preliminary")])
            { FinalizedResultsOnly = true };
        Assert.Equal(1, section.Count);
        Assert.Single(XDocument.Parse(section.PageXml()).Descendants(Fhir + "Observation"));
    }
}
