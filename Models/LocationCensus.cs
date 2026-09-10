namespace ClinicalDataExplorer.Models;

public sealed record LocationCensusGroup(string Key, string Name, string? Status, IReadOnlyList<LocationCensusEncounter> Encounters);

public sealed record LocationCensusEncounter(EncounterSummary Encounter, PatientSummary? Patient, string? PatientId, string PatientDisplay);
