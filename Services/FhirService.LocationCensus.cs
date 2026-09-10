using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public async Task<IReadOnlyList<LocationCensusGroup>> GetLocationCensusAsync(CancellationToken cancellationToken = default)
    {
        var baseUri = GetBaseUri();
        var now = DateTimeOffset.UtcNow;
        var locations = ParseDocument(await GetAllBundlePagesAsync(new Uri(baseUri, "Location?_count=100&_format=xml"), baseUri, cancellationToken));
        var encounters = ParseDocument(await GetAllBundlePagesAsync(new Uri(baseUri,
            "Encounter?status=arrived,triaged,in-progress,onleave&_include=Encounter:patient&_count=100&_format=xml"), baseUri, cancellationToken));
        var groups = new Dictionary<string, (string Name, string? Status, List<LocationCensusEncounter> Rows)>(StringComparer.Ordinal);
        foreach (var location in ReadResources(locations, "Location"))
        {
            var id = Value(location, "id");
            if (id.Length > 0)
                groups["Location/" + id] = (EmptyToNull(Value(location, "name")) ?? "Location " + id, EmptyToNull(Value(location, "status")), []);
        }

        var patients = ParsePatientResources(encounters).ToDictionary(p => p.Id, StringComparer.Ordinal);
        var attemptedPatients = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in ReadResources(encounters, "Encounter"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Value(resource, "status") is not ("arrived" or "triaged" or "in-progress" or "onleave") ||
                !CensusPeriodIsCurrent(resource.Element(Fhir + "period"), now)) continue;
            var encounter = ToEncounter(resource);
            if (encounter.Id.Length == 0) throw new InvalidOperationException("A census encounter has no record ID.");
            var subject = resource.Element(Fhir + "subject");
            var patientKey = CensusReferenceKey(Value(subject, "reference"), "Patient", baseUri, encounters);
            var patientId = patientKey?.StartsWith("Patient/", StringComparison.Ordinal) == true ? patientKey[8..] : null;
            PatientSummary? patient = null;
            if (patientId is not null && !patients.TryGetValue(patientId, out patient) && attemptedPatients.Add(patientId))
            {
                try
                {
                    patient = await GetPatientByIdAsync(patientId, baseUri, cancellationToken);
                    if (patient is not null) patients[patientId] = patient;
                }
                catch (HttpRequestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone) { }
            }
            var row = new LocationCensusEncounter(encounter, patient, patientId,
                patient?.DisplayName ?? EmptyToNull(Value(subject, "display")) ?? EmptyToNull(Value(subject, "reference")) ?? "Patient not recorded");
            var currentLocations = resource.Elements(Fhir + "location")
                .Where(l => Value(l, "status") is "" or "active" && CensusPeriodIsCurrent(l.Element(Fhir + "period"), now)).ToList();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var location in currentLocations)
            {
                var reference = location.Element(Fhir + "location");
                var raw = Value(reference, "reference");
                var display = Value(reference, "display");
                var key = CensusReferenceKey(raw, "Location", baseUri, locations)
                    ?? (raw.Length > 0 ? "unresolved:" + raw : display.Length > 0 ? "display:" + display : "unassigned");
                if (!groups.ContainsKey(key)) groups[key] = (EmptyToNull(display) ?? EmptyToNull(raw) ?? "No current location recorded", null, []);
                if (keys.Add(key)) groups[key].Rows.Add(row);
            }
            if (keys.Count == 0)
            {
                if (!groups.ContainsKey("unassigned")) groups["unassigned"] = ("No current location recorded", null, []);
                groups["unassigned"].Rows.Add(row);
            }
        }
        return groups.OrderByDescending(g => g.Value.Rows.Count > 0)
            .ThenByDescending(g => g.Value.Rows.Max(r => r.Encounter.Start))
            .ThenBy(g => g.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new LocationCensusGroup(g.Key, g.Value.Name, g.Value.Status,
                g.Value.Rows.OrderByDescending(r => r.Encounter.Start).ThenBy(r => r.Encounter.Id, StringComparer.Ordinal).ToList())).ToList();
    }

    private static bool CensusPeriodIsCurrent(XElement? period, DateTimeOffset now) =>
        (ParseDate(Value(period, "start")) is not { } start || start <= now) &&
        (ParseDate(Value(period, "end")) is not { } end || end > now);

    private static string? CensusReferenceKey(string reference, string type, Uri baseUri, XDocument bundle)
    {
        if (string.IsNullOrWhiteSpace(reference)) return null;
        var included = bundle.Root!.Elements(Fhir + "entry")
            .FirstOrDefault(e => Value(e, "fullUrl") == reference)?.Element(Fhir + "resource")?.Element(Fhir + type);
        if (included is not null && Value(included, "id").Length > 0) return type + "/" + Value(included, "id");
        try
        {
            var uri = FhirReferenceLinks.Resolve(reference, baseUri);
            var path = uri.AbsolutePath[baseUri.AbsolutePath.Length..].Split('/');
            return path[0] == type ? type + "/" + path[1] : null;
        }
        catch (InvalidOperationException) { return null; }
    }
}
