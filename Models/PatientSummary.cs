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
    internal TimeSpan RecentLookupDuration { get; init; }
    public string Initials { get; init; } = "?";
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public IReadOnlyList<PractitionerAssociation> Practitioners { get; init; } = [];
    public PractitionerAssociationStatus PractitionerStatus { get; init; }
    public string? PractitionerLookupError { get; init; }
}
