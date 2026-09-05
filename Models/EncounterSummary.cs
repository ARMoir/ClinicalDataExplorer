namespace ClinicalDataExplorer.Models;

public sealed record EncounterSummary(
    string Id,
    IReadOnlyList<string> Identifiers,
    string Status,
    string Class,
    string Type,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string LocationOrProvider,
    int ObservationCount = 0);
