using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public async Task<(PatientSummary Patient, EncounterSummary? Encounter)> GetSavedPatientAsync(
        string patientId, CancellationToken cancellationToken = default)
    {
        patientId = RequireId(patientId, "patient");
        var baseUri = GetBaseUri();
        var patient = await GetPatientByIdAsync(patientId, baseUri, cancellationToken)
            ?? throw new InvalidOperationException("Patient details are unavailable.");
        if (patient.Id != patientId) throw new InvalidOperationException("The server returned a different patient.");
        Uri? next = new(baseUri, "Encounter?patient=" + Uri.EscapeDataString(patientId) + "&_sort=-date&_count=1&_format=xml");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (next is not null)
        {
            if (seen.Count >= 100 || !seen.Add(next.AbsoluteUri)) throw new InvalidOperationException("Encounter search exceeded the paging safety limit.");
            var document = ParseDocument(await GetXmlAsync(next, baseUri, cancellationToken));
            EnsureBundle(document);
            var encounter = ParseEncounters(document).FirstOrDefault();
            if (encounter is not null) return (patient, encounter);
            next = GetNextPageUri(document, baseUri);
        }
        return (patient, null);
    }
}
