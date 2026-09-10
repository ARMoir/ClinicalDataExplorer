using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

// Presentation grouping only: the original FHIR types and references stay intact.
public static class PatientSectionGrouping
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";

    public static IReadOnlyList<PatientResourceSection> Build(XDocument bundle) =>
        bundle.Root!.Elements(Fhir + "entry").Elements(Fhir + "resource").Elements()
            .Where(r => r.Name != Fhir + "OperationOutcome")
            .Select(r =>
            {
                var copy = new XElement(r);
                copy.Elements(Presentation + "consolidatedObservations").Remove();
                return copy;
            })
            .GroupBy(r => r.Name == Fhir + "Observation" &&
                r.Descendants().Attributes("value").Any(v => v.Value.Contains('\n') || v.Value.Contains(@"\n"))
                    ? "DocumentReference" : r.Name.LocalName)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new PatientResourceSection(g.Key, g.ToList())).ToList();
}
