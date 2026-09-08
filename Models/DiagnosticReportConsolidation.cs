using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

// Presentation-only grouping. Never modify or write the server's FHIR resources.
public static class DiagnosticReportConsolidation
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";
    public static bool IsMovedObservation(XElement resource) => resource.Name == Fhir + "Observation" &&
        resource.Descendants().Attributes("value").Any(v => v.Value.Contains('\n') || v.Value.Contains(@"\n"));

    public static IReadOnlyList<XElement> Consolidate(XDocument bundle, Uri baseUri)
    {
        var entries = bundle.Root!.Elements(Fhir + "entry").ToList();
        var resources = entries.Select(e => e.Element(Fhir + "resource")?.Elements().FirstOrDefault())
            .Where(r => r is not null && r.Name != Fhir + "OperationOutcome").Select(r => new XElement(r!)).ToList();
        // This annotation is generated locally, never accepted from the server.
        foreach (var resource in resources) resource.Elements(Presentation + "consolidatedObservations").Remove();
        var reports = resources.Where(r => r.Name == Fhir + "DiagnosticReport").ToList();
        var grouped = new HashSet<XElement>();
        foreach (var observation in resources.Where(IsMovedObservation))
        {
            var id = Value(observation, "id");
            var fullUrls = entries.Where(e => Value(e.Element(Fhir + "resource")?.Element(Fhir + "Observation"), "id") == id)
                .Select(e => Value(e, "fullUrl")).Where(s => s.Length > 0).ToHashSet(StringComparer.Ordinal);
            bool ReferencesObservation(XElement reference, bool requireVersion)
            {
                var raw = (string?)reference.Attribute("value") ?? "";
                if (raw.Length == 0 || id.Length == 0) return false;
                if (fullUrls.Contains(raw)) return true;
                try
                {
                    var uri = FhirReferenceLinks.Resolve(raw, baseUri);
                    var path = uri.AbsolutePath[baseUri.AbsolutePath.Length..].Split('/');
                    return path[0] == "Observation" && path[1] == id &&
                        (!requireVersion || path.Length == 2 || path[3] == Value(observation.Element(Fhir + "meta"), "versionId"));
                }
                catch (InvalidOperationException) { return false; }
            }
            var referenced = reports.Where(r => CompatibleContext(r, observation, baseUri) &&
                r.Elements(Fhir + "result").Elements(Fhir + "reference").Any(reference => ReferencesObservation(reference, true))).ToList();
            var match = "result reference";
            if (referenced.Count == 0)
            {
                // A reference to a different version must not be overridden by a name/date guess.
                if (reports.Any(r => r.Elements(Fhir + "result").Elements(Fhir + "reference").Any(reference => ReferencesObservation(reference, false)))) continue;
                var name = Name(observation);
                var date = ClinicalDay(observation);
                if (name.Length == 0 || date is null) continue;
                var candidates = reports.Where(r => Name(r) == name && ClinicalDay(r) == date && CompatibleContext(r, observation, baseUri)).ToList();
                if (candidates.Count != 1) continue;
                referenced = candidates;
                match = "name and date";
            }
            foreach (var report in referenced)
            {
                var container = report.Element(Presentation + "consolidatedObservations");
                if (container is null) { container = new XElement(Presentation + "consolidatedObservations"); report.Add(container); }
                container.Add(new XElement(Presentation + "item", new XAttribute("match", match), new XElement(observation)));
                grouped.Add(observation);
            }
        }
        return resources.Where(r => !grouped.Contains(r)).ToList();
    }
    private static string Name(XElement resource)
    {
        var code = resource.Element(Fhir + "code");
        var name = Value(code, "text");
        if (string.IsNullOrWhiteSpace(name)) name = code?.Elements(Fhir + "coding").Select(c => Value(c, "display")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "";
        return Regex.Replace(name.Trim(), @"\s+", " ").ToUpperInvariant();
    }
    private static DateOnly? ClinicalDay(XElement resource)
    {
        // Use the recorded clinical day, not an administrative lastUpdated date.
        var value = Value(resource, "effectiveDateTime");
        if (value.Length == 0) value = Value(resource.Element(Fhir + "effectivePeriod"), "start");
        if (value.Length == 0) value = Value(resource, "issued");
        return value.Length >= 10 && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
    }
    private static bool CompatibleContext(XElement report, XElement observation, Uri baseUri)
    {
        foreach (var field in new[] { "subject", "encounter" })
        {
            var a = Value(report.Element(Fhir + field), "reference");
            var b = Value(observation.Element(Fhir + field), "reference");
            if (a.Length == 0 || b.Length == 0 || a == b) continue;
            try { if (FhirReferenceLinks.Resolve(a, baseUri) == FhirReferenceLinks.Resolve(b, baseUri)) continue; }
            catch (InvalidOperationException) { }
            return false;
        }
        return true;
    }
    private static string Value(XElement? parent, string child) => (string?)parent?.Element(Fhir + child)?.Attribute("value") ?? "";
}
