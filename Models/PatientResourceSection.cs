using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public sealed class PatientResourceSection(string resourceType, IReadOnlyList<XElement> resources)
{
    public string ResourceType { get; } = resourceType;
    public string Label => ResourcePresentation.Label(ResourceType);
    public IReadOnlyList<XElement> Resources { get; private set; } = resources.OrderByDescending(ResourceChronology.Date).ToList();
    public int Limit { get; set; } = int.MaxValue;
    public int Count => Math.Min(Resources.Count, Limit);
    public bool HasBufferedRecords => Resources.Count > Count;
    public bool Expanded { get; set; }
    public int Page { get; set; }
    public string? Html { get; set; }

    public void UpdateResources(IReadOnlyList<XElement> resources)
    {
        Resources = resources.OrderByDescending(ResourceChronology.Date).ToList();
        Page = Math.Min(Page, Math.Max(0, (Count - 1) / 10));
        Html = null;
    }

    public string PageXml()
    {
        XNamespace fhir = "http://hl7.org/fhir";
        return new XElement(fhir + "Bundle", Resources.Take(Count).Skip(Page * 10).Take(10)
            .Select(r => new XElement(fhir + "entry", new XElement(fhir + "resource", r)))).ToString();
    }
}
