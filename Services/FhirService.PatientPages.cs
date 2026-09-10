using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    // $everything pages mix resource types. Keep the cursor suspended between
    // requests so the UI can stop at its section limits without losing records.
    public async IAsyncEnumerable<(IReadOnlyList<PatientResourceSection> Sections, bool HasMore)> StreamPatientResourceSectionsAsync(
        string patientId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var baseUri = GetBaseUri();
        Uri? next = new(baseUri, "Patient/" + Uri.EscapeDataString(RequireId(patientId, "patient")) +
            "/$everything?_count=50&_format=xml");
        var combined = new XElement(Fhir + "Bundle");
        var seenPages = new HashSet<string>(StringComparer.Ordinal);
        var seenRecords = new HashSet<string>(StringComparer.Ordinal);
        var entries = 0;
        while (next is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seenPages.Count >= 200 || !seenPages.Add(next.AbsoluteUri))
                throw new InvalidOperationException("FHIR report exceeded the paging safety limit.");
            var document = ParseDocument(await GetXmlAsync(next, baseUri, cancellationToken));
            EnsureBundle(document);
            foreach (var entry in document.Root!.Elements(Fhir + "entry"))
            {
                var resource = entry.Element(Fhir + "resource")?.Elements().FirstOrDefault();
                if (resource is null) continue;
                var id = Value(resource, "id");
                if (id.Length > 0 && !seenRecords.Add(resource.Name.LocalName + "/" + id)) continue;
                if (++entries > 10000) throw new InvalidOperationException("FHIR report exceeded the 10,000-record safety limit.");
                combined.Add(new XElement(entry));
            }
            next = GetNextPageUri(document, baseUri);
            var snapshot = new XDocument(new XElement(combined));
            // Resolve included providers without starting unbounded extra reads.
            await ResolvePractitionerReferencesAsync(snapshot, cancellationToken, fetchMissing: false);
            var sections = PatientSectionGrouping.Build(snapshot);
            yield return (sections, next is not null);
        }
    }
}
