using ClinicalDataExplorer.Models;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public const string PatientResourceTypes = "Account AdverseEvent AllergyIntolerance Appointment AppointmentResponse AuditEvent Basic BodyStructure CarePlan CareTeam ChargeItem Claim ClaimResponse ClinicalImpression Communication CommunicationRequest Composition Condition Consent Coverage CoverageEligibilityRequest CoverageEligibilityResponse DetectedIssue DeviceRequest DeviceUseStatement DiagnosticReport DocumentManifest DocumentReference Encounter EnrollmentRequest EpisodeOfCare ExplanationOfBenefit FamilyMemberHistory Flag Goal Group ImagingStudy Immunization ImmunizationEvaluation ImmunizationRecommendation Invoice List MeasureReport Media MedicationAdministration MedicationDispense MedicationRequest MedicationStatement MolecularSequence NutritionOrder Observation Person Procedure Provenance QuestionnaireResponse RelatedPerson RequestGroup ResearchSubject RiskAssessment Schedule ServiceRequest Specimen SupplyDelivery SupplyRequest VisionPrescription";

    // Failed requests can be retried; rejected continuations stop paging.
    public async Task LoadPatientResourcePageAsync(string patientId, PatientResourceLoad source,
        CancellationToken cancellationToken = default)
    {
        if (!PatientResourceTypes.Split(' ').Contains(source.ResourceType, StringComparer.Ordinal))
            throw new ArgumentException("Unknown patient resource type.", nameof(source));
        var id = RequireId(patientId, "patient");
        var baseUri = GetBaseUri();
        if (source.Server is not null && (source.Server != baseUri || source.PatientId != id))
            throw new InvalidOperationException("Reload the report after changing patient or connection.");
        source.Server = baseUri;
        source.PatientId = id;
        if (!source.HasMore) return;
        var uri = source.Next ?? new Uri(baseUri, $"Patient/{Uri.EscapeDataString(id)}/{source.ResourceType}?_count={settingsService.Current.FhirRequestPageSize}&_format=xml");
        if (source.Pages.Count >= 200 || source.Pages.Contains(uri.AbsoluteUri))
            throw new InvalidOperationException("This section exceeded the paging safety limit.");
        var document = ParseDocument(await GetXmlAsync(uri, baseUri, cancellationToken));
        EnsureBundle(document);
        await ResolvePractitionerReferencesAsync(document, cancellationToken, fetchMissing: false);
        var resources = source.Resources.Concat(document.Root!.Elements(Fhir + "entry")
            .Elements(Fhir + "resource").Elements().Where(r => r.Name != Fhir + "OperationOutcome"))
            .DistinctBy(r => Value(r, "id").Length > 0 ? r.Name.LocalName + "/" + Value(r, "id") : r.ToString())
            .Select(r => new XElement(r)).ToList();
        if (resources.Count > 10000) throw new InvalidOperationException("This section exceeded the 10,000-record safety limit.");
        cancellationToken.ThrowIfCancellationRequested();
        source.Resources = resources;
        source.Loaded = true;
        // Keep successful records even when the continuation is unusable.
        source.Next = null;
        Uri? next;
        try { next = GetNextPageUri(document, uri); }
        catch (UriFormatException ex)
        {
            throw new InvalidOperationException("The server returned a malformed continuation URL.", ex);
        }
        if (next is not null)
        {
            if (!IsWithinConfiguredServer(next, baseUri))
                throw new InvalidOperationException("The server returned a continuation URL outside the configured server.");
            if (next == uri)
                throw new InvalidOperationException("The server returned a continuation URL pointing to the current page.");
            if (source.Pages.Contains(next.AbsoluteUri))
                throw new InvalidOperationException("The server returned a continuation URL pointing to a previously visited page.");
        }
        source.Pages.Add(uri.AbsoluteUri);
        source.Next = next;
    }
}
