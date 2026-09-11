using System.Xml.Linq;

namespace ClinicalDataExplorer.Models;

public sealed class PatientResourceLoad(string resourceType)
{
    public string ResourceType { get; } = resourceType;
    public bool Loading { get; set; }
    public bool Loaded { get; internal set; }
    public string? Error { get; set; }
    public Uri? Next { get; internal set; }
    public bool HasMore => !Loaded || Next is not null;
    public IReadOnlyList<XElement> Resources { get; internal set; } = [];
    internal HashSet<string> Pages { get; } = new(StringComparer.Ordinal);
    internal Uri? Server { get; set; }
    internal string? PatientId { get; set; }
}
