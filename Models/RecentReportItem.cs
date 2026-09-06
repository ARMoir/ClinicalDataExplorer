namespace ClinicalDataExplorer.Models;

public sealed record RecentReportItem(string Id, string Title, string Description, DateTimeOffset? Date,
    string Url, IReadOnlyList<PractitionerAssociation> Providers, PractitionerAssociationStatus ProviderStatus, string? ProviderError);
