namespace ClinicalDataExplorer.Models;

public sealed record PatientSummary(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Identifiers,
    string? BirthDate,
    string? Gender,
    bool? Active,
    DateTimeOffset? LastUpdated,
    DateTimeOffset? MostRecentEncounter);
