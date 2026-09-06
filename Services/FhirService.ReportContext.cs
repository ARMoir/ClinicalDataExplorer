using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public async Task<ReportContext> GetReportContextAsync(string reportXml, CancellationToken cancellationToken = default)
    {
        var document = ParseDocument(reportXml);
        var report = ReadResources(document, "DiagnosticReport").FirstOrDefault();
        if (report is null) return new(null, null, null, null);
        var baseUri = GetBaseUri();
        string? Link(string reference, string type)
        {
            try
            {
                var uri = FhirReferenceLinks.Resolve(reference, baseUri);
                var path = uri.AbsolutePath[baseUri.AbsolutePath.Length..].Split('/');
                if (path[0] != type) return null;
                return path.Length == 2 ? "/" + type.ToLowerInvariant() + "/" + Uri.EscapeDataString(path[1])
                    : "/record?reference=" + Uri.EscapeDataString(uri.AbsoluteUri);
            }
            catch (InvalidOperationException) { return null; }
        }
        var patientRef = Value(report.Element(Fhir + "subject"), "reference");
        var encounterUrl = Link(Value(report.Element(Fhir + "encounter"), "reference"), "Encounter");
        if (patientRef.Length == 0) return new(null, null, encounterUrl, "No patient reference was returned for this report.");
        XElement? patient = null;
        if (patientRef.StartsWith('#'))
            patient = report.Elements(Fhir + "contained").Elements(Fhir + "Patient").FirstOrDefault(p => Value(p, "id") == patientRef[1..]);
        else
            patient = document.Root!.Elements(Fhir + "entry").FirstOrDefault(e => Value(e, "fullUrl") == patientRef)?.Element(Fhir + "resource")?.Element(Fhir + "Patient");
        if (patient is not null)
            return new(patient.ToString(), patientRef.StartsWith("urn:", StringComparison.Ordinal)
                ? Link("Patient/" + Value(patient, "id"), "Patient") : Link(patientRef, "Patient"), encounterUrl, null);
        var patientUrl = Link(patientRef, "Patient");
        if (patientUrl is null) return new(null, null, encounterUrl, "Patient details could not be resolved on the configured server.");
        try
        {
            var uri = FhirReferenceLinks.Resolve(patientRef, baseUri);
            var parts = uri.AbsolutePath[baseUri.AbsolutePath.Length..].Split('/');
            if (parts.Length == 2) patient = ReadResources(document, "Patient").FirstOrDefault(p => Value(p, "id") == parts[1]);
            patient ??= ParseDocument(await GetXmlAsync(new Uri(uri.AbsoluteUri + "?_format=xml"), baseUri, cancellationToken)).Root;
            if (patient?.Name != Fhir + "Patient" || Value(patient, "id") != parts[1])
                throw new InvalidOperationException("The server did not return the referenced patient.");
            return new(patient.ToString(), patientUrl, encounterUrl, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Xml.XmlException)
        {
            return new(null, patientUrl, encounterUrl, "Patient details unavailable. " + ex.Message);
        }
    }
}
