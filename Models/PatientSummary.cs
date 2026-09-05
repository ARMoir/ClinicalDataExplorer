namespace ClinicalDataExplorer.Models;

public sealed record PatientSummary(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Identifiers,
    string? BirthDate,
    string? Gender,
    bool? Active,
    DateTimeOffset? LastUpdated,
    DateTimeOffset? MostRecentEncounter)
{
    public string Initials { get; init; } = "?";
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
}
