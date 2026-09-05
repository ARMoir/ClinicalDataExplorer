namespace ClinicalDataExplorer.Models;

public enum PractitionerAssociationStatus { NotLoaded, Complete, Incomplete, Unavailable }

public sealed record PractitionerIdentifier(string System, string Value, string Label, bool IsResourceId = false);

// Association evidence for display and future policy evaluation, not an access grant.
// Identifier values must be matched together with their issuing system.
public sealed record PractitionerAssociation(
    string Reference,
    string Name,
    IReadOnlyList<PractitionerIdentifier> Identifiers,
    IReadOnlyList<string> Sources,
    bool IsResolved);
