using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public async IAsyncEnumerable<RecentReportItem> StreamRecentReportsAsync(bool diagnostic,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!diagnostic)
        {
            await foreach (var patient in StreamRecentPatientsAsync(cancellationToken))
            {
                var enriched = await AddPatientPractitionersAsync(patient, cancellationToken);
                yield return new(patient.Id, patient.DisplayName, string.Join(" · ", patient.Identifiers), patient.MostRecentEncounter,
                    "/patient/" + Uri.EscapeDataString(patient.Id), enriched.Practitioners, enriched.PractitionerStatus, enriched.PractitionerLookupError);
            }
            yield break;
        }

        var baseUri = GetBaseUri();
        Uri? next = new(baseUri, "DiagnosticReport?_sort=-date&_count=10&_format=xml");
        var pages = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (next is not null)
        {
            if (pages.Count >= 100 || !pages.Add(next.AbsoluteUri))
                throw new InvalidOperationException("Recent diagnostic reports exceeded the paging safety limit.");
            var document = ParseDocument(await GetXmlAsync(next, baseUri, cancellationToken));
            EnsureBundle(document);
            foreach (var report in ReadResources(document, "DiagnosticReport"))
            {
                var id = Value(report, "id");
                if (id.Length == 0 || !seen.Add(id)) continue;
                IReadOnlyList<PractitionerAssociation> providers = [];
                var status = PractitionerAssociationStatus.Complete;
                string? error = null;
                try
                {
                    var loaded = await LoadDiagnosticReportByIdAsync(id, cancellationToken);
                    providers = loaded.Providers;
                    if (providers.Any(p => !p.IsResolved))
                    {
                        status = PractitionerAssociationStatus.Incomplete;
                        error = "Some provider references could not be resolved.";
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Xml.XmlException)
                {
                    status = PractitionerAssociationStatus.Unavailable;
                    error = "Provider associations unavailable. " + ex.Message;
                }
                var title = report.Element(Fhir + "code") is { } code ? ReadCodeableConcept(code) : "Diagnostic report";
                var subject = report.Element(Fhir + "subject");
                var description = Value(subject, "display");
                if (description.Length == 0) description = Value(subject, "reference");
                yield return new(id, string.IsNullOrWhiteSpace(title) ? "Diagnostic report" : title, description,
                    ResourceChronology.Date(report), "/reports?reportId=" + Uri.EscapeDataString(id), providers, status, error);
            }
            next = GetNextPageUri(document, baseUri);
        }
    }

    public async Task<FhirResponse> GetDiagnosticReportByIdAsync(string id, CancellationToken cancellationToken = default) =>
        (await LoadDiagnosticReportByIdAsync(id, cancellationToken)).Response;

    private async Task<(FhirResponse Response, IReadOnlyList<PractitionerAssociation> Providers)> LoadDiagnosticReportByIdAsync(
        string id, CancellationToken cancellationToken)
    {
        var baseUri = GetBaseUri();
        var uri = new Uri(baseUri, "DiagnosticReport?_id=" + Uri.EscapeDataString(RequireId(id, "report id")) +
            "&_include=DiagnosticReport:result&_include:iterate=Observation:performer&_include:iterate=PractitionerRole:practitioner&_format=xml");
        var document = ParseDocument(await GetAllBundlePagesAsync(uri, baseUri, cancellationToken));
        var reports = ReadResources(document, "DiagnosticReport").ToList();
        if (reports.Count != 1 || Value(reports[0], "id") != id)
            throw new InvalidOperationException("The server did not return the selected diagnostic report.");
        var providers = await ResolvePractitionerReferencesAsync(document, cancellationToken);
        return (new(uri.AbsoluteUri, document.ToString(SaveOptions.DisableFormatting)), ConsolidatePractitioners(providers));
    }
}
