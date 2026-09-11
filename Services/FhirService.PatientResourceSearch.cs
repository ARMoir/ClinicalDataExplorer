using ClinicalDataExplorer.Models;
using System.Xml.Linq;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    public const string PatientResourceTypes = "Account AdverseEvent AllergyIntolerance Appointment AppointmentResponse AuditEvent Basic BodyStructure CarePlan CareTeam ChargeItem Claim ClaimResponse ClinicalImpression Communication CommunicationRequest Composition Condition Consent Coverage CoverageEligibilityRequest CoverageEligibilityResponse DetectedIssue DeviceRequest DeviceUseStatement DiagnosticReport DocumentManifest DocumentReference Encounter EnrollmentRequest EpisodeOfCare ExplanationOfBenefit FamilyMemberHistory Flag Goal Group ImagingStudy Immunization ImmunizationEvaluation ImmunizationRecommendation Invoice List MeasureReport Media MedicationAdministration MedicationDispense MedicationRequest MedicationStatement MolecularSequence NutritionOrder Observation Person Procedure Provenance QuestionnaireResponse RelatedPerson RequestGroup ResearchSubject RiskAssessment Schedule ServiceRequest Specimen SupplyDelivery SupplyRequest VisionPrescription";

    // Each source owns its cursor. Commit only successful pages so a failed request can be retried.
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
        var uri = source.Next ?? new Uri(baseUri, $"Patient/{Uri.EscapeDataString(id)}/{source.ResourceType}?_count=50&_format=xml");
        if (source.Pages.Count >= 200 || source.Pages.Contains(uri.AbsoluteUri))
            throw new InvalidOperationException("This section exceeded the paging safety limit.");
        var document = ParseDocument(await GetXmlAsync(uri, baseUri, cancellationToken));
        EnsureBundle(document);
        var next = GetNextPageUri(document, baseUri);
        if (next is not null && (!IsWithinConfiguredServer(next, baseUri) || next == uri || source.Pages.Contains(next.AbsoluteUri)))
            throw new InvalidOperationException("The server returned an invalid or repeated continuation page.");
        await ResolvePractitionerReferencesAsync(document, cancellationToken, fetchMissing: false);
        var resources = source.Resources.Concat(document.Root!.Elements(Fhir + "entry")
            .Elements(Fhir + "resource").Elements().Where(r => r.Name != Fhir + "OperationOutcome"))
            .DistinctBy(r => Value(r, "id").Length > 0 ? r.Name.LocalName + "/" + Value(r, "id") : r.ToString())
            .Select(r => new XElement(r)).ToList();
        if (resources.Count > 10000) throw new InvalidOperationException("This section exceeded the 10,000-record safety limit.");
        cancellationToken.ThrowIfCancellationRequested();
        source.Resources = resources;
        source.Next = next;
        source.Loaded = true;
        source.Pages.Add(uri.AbsoluteUri);
    }
}
