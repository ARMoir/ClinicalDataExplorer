namespace ClinicalDataExplorer.Models;

public sealed record ReportContext(string? PatientXml, string? PatientUrl, string? EncounterUrl, string? PatientError);
