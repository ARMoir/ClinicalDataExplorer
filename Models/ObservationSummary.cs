namespace ClinicalDataExplorer.Models;

public sealed record ObservationSummary(
    string Id,
    string Name,
    string Value,
    string Status,
    DateTimeOffset? EffectiveDate,
    string Interpretation,
    string ReferenceRange);
