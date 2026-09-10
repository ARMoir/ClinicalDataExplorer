using System.Text.RegularExpressions;

namespace ClinicalDataExplorer.Models;

public static class ResourcePresentation
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Patient"] = "Patient records", ["Account"] = "Accounts", ["Appointment"] = "Appointments",
        ["AppointmentResponse"] = "Appointment responses", ["Coverage"] = "Coverage", ["RelatedPerson"] = "Related persons",
        ["Encounter"] = "Visits and encounters", ["Observation"] = "Observations and measurements",
        ["DiagnosticReport"] = "Diagnostic reports", ["AllergyIntolerance"] = "Allergies and intolerances",
        ["Condition"] = "Conditions", ["Procedure"] = "Procedures", ["MedicationRequest"] = "Medication orders",
        ["MedicationStatement"] = "Reported medications", ["MedicationAdministration"] = "Medication administrations",
        ["MedicationDispense"] = "Dispensed medications", ["DocumentReference"] = "Documents", ["ImagingStudy"] = "Imaging studies",
        ["ServiceRequest"] = "Service orders", ["QuestionnaireResponse"] = "Completed forms", ["Practitioner"] = "Clinicians",
        ["PractitionerRole"] = "Provider roles", ["Organization"] = "Organizations", ["Location"] = "Locations",
        ["HealthcareService"] = "Healthcare services", ["Endpoint"] = "Service connections", ["Medication"] = "Medication catalog",
        ["CodeSystem"] = "Code systems", ["ValueSet"] = "Code lists", ["ConceptMap"] = "Code mappings",
        ["NamingSystem"] = "Identifier systems", ["StructureDefinition"] = "Record definitions", ["StructureMap"] = "Record mappings",
        ["SearchParameter"] = "Search definitions", ["OperationDefinition"] = "Operation definitions",
        ["CompartmentDefinition"] = "Record grouping rules", ["CapabilityStatement"] = "Server capability statements",
        ["TerminologyCapabilities"] = "Terminology capability statements", ["Questionnaire"] = "Forms and questionnaires",
        ["Library"] = "Knowledge libraries", ["PlanDefinition"] = "Care plan templates", ["ActivityDefinition"] = "Activity templates",
        ["Measure"] = "Quality measure definitions", ["ObservationDefinition"] = "Measurement definitions",
        ["DeviceDefinition"] = "Device catalog", ["SpecimenDefinition"] = "Specimen definitions", ["Task"] = "Tasks",
        ["Binary"] = "File content", ["Provenance"] = "Record history", ["AuditEvent"] = "Access history"
    };

    // Patient report reading order: care context, observations, documents and
    // medications, then other investigations, history and supporting records.
    private static readonly Dictionary<string, int> PatientSectionRanks = (
        "Patient Account Appointment AppointmentResponse Encounter EpisodeOfCare " +
        "PractitionerRole Practitioner CareTeam Coverage RelatedPerson " +
        "Observation DocumentReference MedicationStatement MedicationRequest MedicationAdministration MedicationDispense Medication " +
        "ServiceRequest DiagnosticReport ImagingStudy Specimen " +
        "Condition AllergyIntolerance Flag " +
        "Procedure Immunization ImmunizationEvaluation ImmunizationRecommendation " +
        "ClinicalImpression FamilyMemberHistory RiskAssessment DetectedIssue AdverseEvent " +
        "CarePlan Goal NutritionOrder DeviceRequest DeviceUseStatement " +
        "Communication CommunicationRequest Task RequestGroup QuestionnaireResponse " +
        "Composition DocumentManifest Media Consent " +
        "CoverageEligibilityRequest CoverageEligibilityResponse EnrollmentRequest Claim ClaimResponse " +
        "ExplanationOfBenefit ChargeItem Invoice SupplyRequest SupplyDelivery " +
        "ResearchSubject MeasureReport MolecularSequence BodyStructure VisionPrescription " +
        "Person Group List Basic Schedule Slot Organization OrganizationAffiliation Location HealthcareService " +
        "Device Substance Questionnaire ResearchStudy Endpoint Binary Provenance AuditEvent")
        .Split(' ').Select((type, rank) => (type, rank))
        .ToDictionary(item => item.type, item => item.rank, StringComparer.Ordinal);

    public static int PatientSectionOrder(string type) => PatientSectionRanks.GetValueOrDefault(type, int.MaxValue);

    // Shared definitions and directories, not unscoped patient records. New types can
    // be assigned here deliberately; metadata still lists every advertised type.
    private const string ReferenceTypes = "CapabilityStatement TerminologyCapabilities CodeSystem ValueSet ConceptMap NamingSystem StructureDefinition StructureMap SearchParameter OperationDefinition CompartmentDefinition ImplementationGuide MessageDefinition EventDefinition GraphDefinition ExampleScenario Questionnaire Library PlanDefinition ActivityDefinition Measure ObservationDefinition DeviceDefinition SpecimenDefinition ResearchStudy ResearchDefinition ResearchElementDefinition Evidence EvidenceVariable EffectEvidenceSynthesis RiskEvidenceSynthesis TestScript TestReport Organization OrganizationAffiliation Practitioner PractitionerRole Location HealthcareService Endpoint Medication MedicationKnowledge Substance SubstanceSpecification SubstanceNucleicAcid SubstancePolymer SubstanceProtein SubstanceReferenceInformation SubstanceSourceMaterial MedicinalProduct MedicinalProductAuthorization MedicinalProductContraindication MedicinalProductIndication MedicinalProductIngredient MedicinalProductInteraction MedicinalProductManufactured MedicinalProductPackaged MedicinalProductPharmaceutical MedicinalProductUndesirableEffect InsurancePlan ChargeItemDefinition CatalogEntry";
    private static readonly HashSet<string> ReferenceSet = new(ReferenceTypes.Split(' '), StringComparer.Ordinal);
    public static bool IsReference(string type) => ReferenceSet.Contains(type);
    public static string Label(string type) => Labels.GetValueOrDefault(type) ?? Regex.Replace(type, "(?<=[a-z0-9])(?=[A-Z])", " ");
}

public sealed record ServerInformation(string Xml, IReadOnlyList<string> ReferenceTypes);
public sealed record ReferencePage(string Xml, Uri? Next, int Count, int? Total, string Source);
