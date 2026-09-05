using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public sealed class PatientResourceSection(string resourceType, IReadOnlyList<XElement> resources)
{
    public string ResourceType { get; } = resourceType;
    public IReadOnlyList<XElement> Resources { get; } = resources;
    public int Count => Resources.Count;
    public bool Expanded { get; set; }
    public int Page { get; set; }
    public string? Html { get; set; }

    public string PageXml()
    {
        XNamespace fhir = "http://hl7.org/fhir";
        return new XElement(fhir + "Bundle", Resources.Skip(Page * 10).Take(10)
            .Select(r => new XElement(fhir + "entry", new XElement(fhir + "resource", r)))).ToString();
    }
}
