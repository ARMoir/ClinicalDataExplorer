using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed partial class FhirService
{
    private static readonly XNamespace Presentation = "urn:clinical-data-explorer:presentation";
    private static readonly TimeSpan RecentPatientBudget = TimeSpan.FromSeconds(5);

    public async Task<PatientSummary> AddRecentPatientPractitionersAsync(PatientSummary patient,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var found = new System.Collections.Concurrent.ConcurrentDictionary<string, PractitionerAssociation>(StringComparer.Ordinal);
        void Remember(PractitionerAssociation provider) => found.AddOrUpdate(provider.Reference, provider,
            (_, previous) => previous.IsResolved && !provider.IsResolved ? previous : provider);
        var remaining = RecentPatientBudget - patient.RecentLookupDuration;
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (remaining > TimeSpan.Zero)
        {
            budget.CancelAfter(remaining);
            try
            {
                return await LoadRecentProvidersAsync().WaitAsync(budget.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && budget.IsCancellationRequested) { }
        }
        return patient with
        {
            Practitioners = ConsolidatePractitioners(found.Values.ToList()),
            PractitionerStatus = PractitionerAssociationStatus.Incomplete,
            PractitionerLookupError = "Loading stopped after 5 seconds. There may be more providers. Search for this patient specifically to wait for the full record."
        };

        async Task<PatientSummary> LoadRecentProvidersAsync()
        {
            try
            {
                var baseUri = GetBaseUri();
                var uri = new Uri(baseUri, "Patient/" + Uri.EscapeDataString(RequireId(patient.Id, "patient")) +
                    "/$everything?_count=100&_format=xml");
                var xml = await GetAllBundlePagesAsync(uri, baseUri, budget.Token, async document =>
                {
                    // Publish providers already included before waiting for another page or reference.
                    await ResolvePractitionerReferencesAsync(document, budget.Token, Remember, fetchMissing: false);
                });
                var providers = await ResolvePractitionerReferencesAsync(ParseDocument(xml), budget.Token, Remember);
                var incomplete = providers.Any(p => !p.IsResolved);
                return patient with
                {
                    Practitioners = ConsolidatePractitioners(providers),
                    PractitionerStatus = incomplete ? PractitionerAssociationStatus.Incomplete : PractitionerAssociationStatus.Complete,
                    PractitionerLookupError = incomplete ? "Some provider references could not be resolved. There may be more providers." : null
                };
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Xml.XmlException)
            {
                return patient with
                {
                    Practitioners = ConsolidatePractitioners(found.Values.ToList()),
                    PractitionerStatus = PractitionerAssociationStatus.Unavailable,
                    PractitionerLookupError = "Provider associations unavailable. " + ex.Message
                };
            }
        }
    }

    private async Task<string> AddPractitionerPresentationAsync(string xml, CancellationToken cancellationToken)
    {
        var document = ParseDocument(xml);
        await ResolvePractitionerReferencesAsync(document, cancellationToken);
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public async Task<PatientSummary> AddPatientPractitionersAsync(PatientSummary patient,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var xml = await GetAllBundlePagesAsync("Patient/" + Uri.EscapeDataString(RequireId(patient.Id, "patient")) +
                "/$everything?_count=100&_format=xml", cancellationToken);
            var associations = await ResolvePractitionerReferencesAsync(ParseDocument(xml), cancellationToken);
            return patient with
            {
                Practitioners = ConsolidatePractitioners(associations),
                PractitionerStatus = associations.Any(p => !p.IsResolved)
                    ? PractitionerAssociationStatus.Incomplete : PractitionerAssociationStatus.Complete,
                PractitionerLookupError = associations.Any(p => !p.IsResolved)
                    ? "Some provider references could not be resolved." : null
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Xml.XmlException)
        {
            return patient with
            {
                Practitioners = [],
                PractitionerStatus = PractitionerAssociationStatus.Unavailable,
                PractitionerLookupError = "Provider associations unavailable. " + ex.Message
            };
        }
    }

    private static IReadOnlyList<PractitionerAssociation> ConsolidatePractitioners(IReadOnlyList<PractitionerAssociation> providers)
    {
        var parents = Enumerable.Range(0, providers.Count).ToArray();
        var matches = new Dictionary<(string Kind, string System, string Value), int>();
        int Root(int index)
        {
            while (parents[index] != index) { parents[index] = parents[parents[index]]; index = parents[index]; }
            return index;
        }
        for (var i = 0; i < providers.Count; i++)
        {
            var keys = providers[i].References.Select(r => (Kind: "reference", System: "", Value: r))
                .Concat(providers[i].Identifiers.Where(id => !string.IsNullOrWhiteSpace(id.Value))
                    .Select(id => (Kind: id.IsResourceId ? "resource-id" : "identifier", id.System, id.Value)));
            foreach (var key in keys)
            {
                if (matches.TryGetValue(key, out var previous)) parents[Root(i)] = Root(previous);
                else matches[key] = i;
            }
        }
        return Enumerable.Range(0, providers.Count).GroupBy(Root).Select(group =>
        {
            var records = group.Select(i => providers[i]).ToList();
            var preferred = records.OrderByDescending(p => p.IsResolved && p.Name != "Name not recorded")
                .ThenBy(p => p.Reference, StringComparer.Ordinal).First();
            return preferred with
            {
                References = records.SelectMany(p => p.References).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
                Identifiers = records.SelectMany(p => p.Identifiers).GroupBy(id => (id.System, id.Value, id.IsResourceId))
                    .Select(ids => ids.OrderByDescending(id => !string.IsNullOrWhiteSpace(id.Label)).First()).ToList(),
                Sources = records.SelectMany(p => p.Sources).Distinct(StringComparer.Ordinal).ToList(),
                IsResolved = records.All(p => p.IsResolved)
            };
        }).OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Reference, StringComparer.Ordinal).ToList();
    }

    private async Task<IReadOnlyList<PractitionerAssociation>> ResolvePractitionerReferencesAsync(
        XDocument document, CancellationToken cancellationToken,
        Action<PractitionerAssociation>? onProvider = null, bool fetchMissing = true)
    {
        var baseUri = GetBaseUri();
        var resources = document.Root!.Name == Fhir + "Bundle" ? document.Root.Elements(Fhir + "entry")
            .Select(e => e.Element(Fhir + "resource")?.Elements().FirstOrDefault()).OfType<XElement>().ToList() : [document.Root];
        var included = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            var id = Value(resource, "id");
            if (id.Length > 0) included[resource.Name.LocalName + "/" + id] = resource;
            var fullUrl = Value(resource.Parent?.Parent, "fullUrl");
            if (fullUrl.Length > 0) included[fullUrl] = resource;
        }
        var fetched = new Dictionary<string, XElement?>(StringComparer.Ordinal);
        var associations = new Dictionary<string, PractitionerAssociation>(StringComparer.Ordinal);

        // Only follow actual references; an unrelated included practitioner is not
        // evidence that the patient is associated with that practitioner.
        foreach (var resource in resources.Where(r => r.Name != Fhir + "Practitioner"))
        foreach (var reference in resource.Descendants(Fhir + "reference").ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = ElementValue(reference);
            var target = await Resolve(raw, resource);
            if (target.Type is not ("Practitioner" or "PractitionerRole")) continue;
            var source = resource.Name.LocalName + "/" + Value(resource, "id") + " · " + reference.Parent!.Name.LocalName + " → " + raw;
            var result = target.Resource;
            var key = target.Key;
            if (target.Type == "PractitionerRole" && result is not null)
            {
                var practitioner = await Resolve(Value(result.Element(Fhir + "practitioner"), "reference"), result);
                if (practitioner.Type == "Practitioner") { result = practitioner.Resource; key = practitioner.Key; }
                else result = null;
            }
            var resolved = result?.Name == Fhir + "Practitioner";
            var name = resolved ? ToPatient(result!).DisplayName : Value(reference.Parent, "display");
            if (name == "Unnamed patient" || string.IsNullOrWhiteSpace(name)) name = resolved ? "Name not recorded" : "Provider details unavailable";
            var identifiers = resolved ? result!.Elements(Fhir + "identifier")
                .Where(i => !string.IsNullOrWhiteSpace(Value(i, "value")))
                .Select(i => new PractitionerIdentifier(Value(i, "system"), Value(i, "value"),
                    i.Element(Fhir + "type") is { } identifierType ? ReadCodeableConcept(identifierType) : ""))
                .Distinct().ToList() : [];
            if (resolved && identifiers.Count == 0 && !string.IsNullOrWhiteSpace(Value(result, "id")))
            {
                // Scope resource IDs to their server (and parent for contained records).
                // Keep their origin explicit for future provider matching policies.
                var scope = key.Contains('#') ? key[..key.IndexOf('#')] + "#contained" : "Practitioner";
                identifiers.Add(new(new Uri(baseUri, scope).AbsoluteUri, Value(result, "id"), "Provider ID", IsResourceId: true));
            }
            var sources = associations.TryGetValue(key, out var existing)
                ? existing.Sources.Append(source).Distinct(StringComparer.Ordinal).ToList() : [source];
            associations[key] = new(key, name, identifiers, sources, resolved);
            onProvider?.Invoke(associations[key]);
            var identifierText = string.Join("; ", identifiers.Select(i =>
                (i.Label.Length > 0 ? i.Label + ": " : "") + i.Value + (i.System.Length > 0 ? " [" + i.System + "]" : "")));
            // Presentation metadata leaves original FHIR reference/display fields intact.
            reference.Parent.SetAttributeValue(Presentation + "provider", name + " · " + key +
                (identifierText.Length > 0 ? " · " + identifierText : resolved ? " · No identifiers recorded" : ""));
        }
        return associations.Values.OrderBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.Reference, StringComparer.Ordinal).ToList();

        async Task<(string Type, string Key, XElement? Resource)> Resolve(string raw, XElement owner)
        {
            if (raw.StartsWith('#'))
            {
                var contained = owner.Elements(Fhir + "contained").Elements().FirstOrDefault(r => Value(r, "id") == raw[1..]);
                return (contained?.Name.LocalName ?? "", owner.Name.LocalName + "/" + Value(owner, "id") + raw, contained);
            }
            if (included.TryGetValue(raw, out var exact)) return (exact.Name.LocalName, exact.Name.LocalName + "/" + Value(exact, "id"), exact);
            if (!Uri.TryCreate(baseUri, raw, out var uri)) return ("", raw, null);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var index = Array.FindLastIndex(segments, s => s is "Practitioner" or "PractitionerRole");
            if (index < 0 || index + 1 >= segments.Length) return ("", raw, null);
            var type = segments[index];
            if (!IsWithinConfiguredServer(uri, baseUri)) return (type, raw, null);
            var key = type + "/" + segments[index + 1];
            if (included.TryGetValue(key, out var local)) return (type, key, local);
            if (!fetchMissing) return (type, key, null);
            if (!fetched.TryGetValue(key, out var found))
            {
                found = null;
                try
                {
                    var response = ParseDocument(await GetXmlAsync(new Uri(baseUri, key + "?_format=xml"), baseUri, cancellationToken));
                    if (response.Root?.Name == Fhir + type && Value(response.Root, "id") == segments[index + 1]) found = response.Root;
                }
                catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or System.Xml.XmlException) { }
                fetched[key] = found;
            }
            return (type, key, found);
        }
    }
}
