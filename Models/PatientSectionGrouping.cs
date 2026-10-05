using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

// Presentation grouping only: the original FHIR types and references stay intact.
public static class PatientSectionGrouping
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";

    // Foreground records take precedence; enrichment only fills missing identities.
    public static IReadOnlyList<PatientResourceSection> Merge(IEnumerable<XElement> primary, IEnumerable<XElement> enrichment)
    {
        var resources = primary.Concat(enrichment).DistinctBy(r =>
            r.Name.LocalName + "/" + (r.Element(Fhir + "id")?.Attribute("value")?.Value ?? r.ToString()));
        return Build(new XDocument(new XElement(Fhir + "Bundle", resources.Select(r =>
            new XElement(Fhir + "entry", new XElement(Fhir + "resource", new XElement(r)))))));
    }

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
            .Select(g => new PatientResourceSection(g.Key,
                g.Key is "Coverage" or "RelatedPerson" ? GroupSharedRecords(g) : g.ToList())).ToList();

    private static IReadOnlyList<XElement> GroupSharedRecords(IEnumerable<XElement> resources) =>
        resources.GroupBy(SharedRecordKey, StringComparer.Ordinal).Select(group =>
        {
            var records = group.ToList();
            var representative = new XElement(records[0]);
            if (records.Count > 1)
                representative.Add(new XElement(Presentation + "sharedRecords",
                    records.Select(r => new XElement(Presentation + "record", new XElement(r)))));
            return representative;
        }).ToList();

    private static string SharedRecordKey(XElement resource)
    {
        var copy = new XElement(resource);
        copy.Elements(Fhir + "id").Remove();
        copy.Elements(Fhir + "meta").Elements()
            .Where(e => e.Name == Fhir + "versionId" || e.Name == Fhir + "lastUpdated").Remove();
        copy.Elements(Fhir + "meta").Where(e => !e.HasElements && !e.HasAttributes).Remove();
        copy.DescendantsAndSelf().Attributes().Where(a => a.Name.Namespace == Presentation).Remove();
        foreach (var encounter in copy.Elements(Fhir + "encounter"))
            encounter.ReplaceNodes();
        // Encounter associations can also be supplied as extension valueReferences.
        // Keep the extension URL and all other extension content in the comparison.
        foreach (var reference in copy.Descendants(Fhir + "valueReference")
            .Where(e => IsEncounterReference((string?)e.Element(Fhir + "reference")?.Attribute("value"))))
        {
            reference.Element(Fhir + "reference")!.SetAttributeValue("value", "Encounter/shared");
            reference.Elements(Fhir + "display").Remove();
        }
        return Canonical(copy).ToString(SaveOptions.DisableFormatting);
    }

    private static bool IsEncounterReference(string? value) => value is not null &&
        (value.StartsWith("Encounter/", StringComparison.Ordinal) ||
         Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.AbsolutePath.Contains("/Encounter/", StringComparison.Ordinal));

    // Ignore XML formatting and attribute order, while retaining field and array order.
    private static XElement Canonical(XElement element) => new(element.Name,
        element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal),
        element.Nodes().Select(n => n is XElement child ? (object)Canonical(child) :
            n is XText text && !string.IsNullOrWhiteSpace(text.Value) ? new XText(text.Value) : null));
}
