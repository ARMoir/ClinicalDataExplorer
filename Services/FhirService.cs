using System.Xml;
using System.Xml.Linq;
using ClinicalDataExplorer.Models;

namespace ClinicalDataExplorer.Services;

public sealed class FhirService(
    IHttpClientFactory httpClientFactory,
    ApplicationSettingsService settingsService)
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";

    public async Task<IReadOnlyList<PatientSummary>> GetPatientsWithRecentEncountersAsync(
        CancellationToken cancellationToken = default)
    {
        var baseUri = GetBaseUri();
        Uri? nextUri = new(baseUri,
            "Encounter?_sort=-date&_include=Encounter:patient&_count=100&_format=xml");
        var patients = new List<PatientSummary>(10);
        var seenPatientIds = new HashSet<string>(StringComparer.Ordinal);

        // Encounters arrive newest-first. The first encounter seen for a patient
        // is therefore that patient's most recent encounter.
        for (var page = 0; nextUri is not null && patients.Count < 10; page++)
        {
            if (page >= 100)
                throw new InvalidOperationException("Patient discovery exceeded the 10,000-encounter safety limit.");

            var document = ParseDocument(await GetXmlAsync(nextUri, baseUri, cancellationToken));
            var includedPatients = ParsePatientResources(document)
                .GroupBy(patient => patient.Id, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var encounterElement in ReadResources(document, "Encounter"))
            {
                var patientId = ReadPatientId(encounterElement);
                if (string.IsNullOrWhiteSpace(patientId) || !seenPatientIds.Add(patientId))
                    continue;

                if (!includedPatients.TryGetValue(patientId, out var patient))
                    patient = await GetPatientByIdAsync(patientId, baseUri, cancellationToken);

                if (patient is null)
                    continue;

                var encounter = ToEncounter(encounterElement);
                patients.Add(patient with { MostRecentEncounter = encounter.Start });
                if (patients.Count == 10)
                    break;
            }

            nextUri = GetNextPageUri(document, baseUri);
        }

        return patients;
    }

    public async Task<IReadOnlyList<PatientSummary>> SearchPatientsByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return [];

        var url = "Patient?identifier=" + Uri.EscapeDataString(identifier.Trim()) + "&_count=50&_format=xml";
        return ParsePatients(await GetXmlAsync(url, cancellationToken));
    }

    public async Task<IReadOnlyList<EncounterSummary>> GetPatientEncountersAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(patientId))
            throw new ArgumentException("A patient id is required.", nameof(patientId));

        var baseUri = GetBaseUri();
        Uri? nextUri = new(baseUri,
            "Encounter?patient=" + Uri.EscapeDataString(patientId.Trim()) +
            "&_sort=-date&_count=100&_format=xml");
        var encounters = new List<EncounterSummary>();

        // Protect against a malformed server returning an endless next-link loop.
        for (var page = 0; nextUri is not null; page++)
        {
            if (page >= 100)
                throw new InvalidOperationException("Encounter history exceeded the 10,000-record safety limit.");

            var document = ParseDocument(await GetXmlAsync(nextUri, baseUri, cancellationToken));
            encounters.AddRange(ParseEncounters(document));
            nextUri = GetNextPageUri(document, baseUri);
        }

        return await AddObservationCountsAsync(encounters, baseUri, cancellationToken);
    }

    public async Task<IReadOnlyList<ObservationSummary>> GetEncounterObservationsAsync(
        string encounterId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(encounterId))
            throw new ArgumentException("An encounter id is required.", nameof(encounterId));

        var baseUri = GetBaseUri();
        Uri? nextUri = new(baseUri,
            "Observation?encounter=" + Uri.EscapeDataString(encounterId.Trim()) +
            "&_sort=-date&_count=100&_format=xml");
        var observations = new List<ObservationSummary>();

        for (var page = 0; nextUri is not null; page++)
        {
            if (page >= 100)
                throw new InvalidOperationException("Observation history exceeded the 10,000-record safety limit.");

            var document = ParseDocument(await GetXmlAsync(nextUri, baseUri, cancellationToken));
            observations.AddRange(ReadResources(document, "Observation").Select(ToObservation));
            nextUri = GetNextPageUri(document, baseUri);
        }

        return observations;
    }

    public Task<string> GetPatientXmlAsync(string patientId, CancellationToken cancellationToken = default) =>
        GetResourceXmlAsync("Patient", patientId, cancellationToken);

    public Task<string> GetEncounterXmlAsync(string encounterId, CancellationToken cancellationToken = default) =>
        GetResourceXmlAsync("Encounter", encounterId, cancellationToken);

    public Task<string> GetPatientEncounterBundleXmlAsync(string patientId, CancellationToken cancellationToken = default) =>
        GetAllBundlePagesAsync(
            "Encounter?patient=" + Uri.EscapeDataString(RequireId(patientId, "patient")) +
            "&_sort=-date&_count=100&_format=xml",
            cancellationToken);

    public Task<string> GetEncounterObservationBundleXmlAsync(string encounterId, CancellationToken cancellationToken = default) =>
        GetAllBundlePagesAsync(
            "Observation?encounter=" + Uri.EscapeDataString(RequireId(encounterId, "encounter")) +
            "&_sort=-date&_count=100&_format=xml",
            cancellationToken);

    public Task<string> SearchPatientBundleXmlAsync(string identifier, CancellationToken cancellationToken = default) =>
        GetAllBundlePagesAsync(
            "Patient?identifier=" + Uri.EscapeDataString(RequireId(identifier, "identifier")) +
            "&_count=100&_format=xml",
            cancellationToken);

    public async Task<FhirResponse> GetDiagnosticReportBundleXmlAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("An identifier is required.", nameof(identifier));

        // Retrieve the report results plus performer resources so the report
        // can resolve clinician names rather than showing only FHIR references.
        // Equivalent to:
        // DiagnosticReport?identifier=0000004676
        //   &_include=DiagnosticReport:result
        //   &_include:iterate=Observation:performer
        //   &_include:iterate=PractitionerRole:practitioner
        //   &_format=xml
        var relativeUrl =
            "DiagnosticReport" +
            "?identifier=" + Uri.EscapeDataString(identifier.Trim()) +
            "&_include=" + Uri.EscapeDataString("DiagnosticReport:result") +
            "&_include:iterate=" + Uri.EscapeDataString("Observation:performer") +
            "&_include:iterate=" + Uri.EscapeDataString("PractitionerRole:practitioner") +
            "&_format=xml";

        var baseUri = GetBaseUri();
        var requestUri = new Uri(baseUri, relativeUrl);
        var body = await GetAllBundlePagesAsync(requestUri, baseUri, cancellationToken);
        return new FhirResponse(requestUri.ToString(), body);
    }

    private async Task<string> GetXmlAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var baseUri = GetBaseUri();
        return await GetXmlAsync(new Uri(baseUri, relativeUrl), baseUri, cancellationToken);
    }

    private Task<string> GetResourceXmlAsync(string resourceType, string id, CancellationToken cancellationToken)
    {
        var safeId = RequireId(id, resourceType);
        return GetXmlAsync(resourceType + "/" + Uri.EscapeDataString(safeId) + "?_format=xml", cancellationToken);
    }

    private async Task<string> GetAllBundlePagesAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var baseUri = GetBaseUri();
        return await GetAllBundlePagesAsync(new Uri(baseUri, relativeUrl), baseUri, cancellationToken);
    }

    private async Task<string> GetAllBundlePagesAsync(Uri requestUri, Uri baseUri, CancellationToken cancellationToken)
    {
        Uri? nextUri = requestUri;
        var combined = new XElement(Fhir + "Bundle",
            new XElement(Fhir + "type", new XAttribute("value", "searchset")));
        var count = 0;
        var seenResources = new HashSet<string>(StringComparer.Ordinal);

        for (var page = 0; nextUri is not null; page++)
        {
            if (page >= 100)
                throw new InvalidOperationException("FHIR report exceeded the 10,000-record safety limit.");

            var document = ParseDocument(await GetXmlAsync(nextUri, baseUri, cancellationToken));
            foreach (var entry in document.Root?.Elements(Fhir + "entry") ?? [])
            {
                // Servers can repeat included resources across search pages.
                var resource = entry.Element(Fhir + "resource")?.Elements().FirstOrDefault();
                var id = Value(resource, "id");
                if (resource is not null && id.Length > 0 &&
                    !seenResources.Add(resource.Name.LocalName + "/" + id))
                    continue;
                combined.Add(new XElement(entry));
                // Bundle.total counts matches, not included resources or outcomes.
                var mode = Value(entry.Element(Fhir + "search"), "mode");
                if (mode != "include" && mode != "outcome" && resource?.Name != Fhir + "OperationOutcome")
                    count++;
            }
            nextUri = GetNextPageUri(document, baseUri);
        }

        combined.AddFirst(new XElement(Fhir + "total", new XAttribute("value", count)));
        return new XDocument(combined).ToString(SaveOptions.DisableFormatting);
    }

    private static string RequireId(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"A {name} is required.", name);

    private async Task<string> GetXmlAsync(Uri requestUri, Uri configuredBaseUri, CancellationToken cancellationToken)
    {
        if (!IsWithinConfiguredServer(requestUri, configuredBaseUri))
            throw new InvalidOperationException("The FHIR server returned a paging link outside the configured server.");

        var client = httpClientFactory.CreateClient("Fhir");
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.ParseAdd("application/fhir+xml");
        // Microsoft FHIR Server otherwise handles unsupported searches leniently.
        // Never silently show incorrectly filtered or ordered clinical results.
        request.Headers.TryAddWithoutValidation("Prefer", "handling=strict");
        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"FHIR server returned {(int)response.StatusCode} {response.ReasonPhrase}. " +
                "Check server search capabilities and access configuration.", null, response.StatusCode);

        if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
            throw new InvalidOperationException(
                "The FHIR server returned JSON when XML was requested. This application's reports require an XML-enabled FHIR R4 endpoint.");

        return body;
    }

    private Uri GetBaseUri()
    {
        var configuredBaseUrl = settingsService.Current.FhirBaseUrl;
        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("The configured FHIR base URL is invalid. Open Settings and enter an absolute HTTP or HTTPS URL.");

        return baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
    }

    private static bool IsWithinConfiguredServer(Uri requestUri, Uri baseUri) =>
        string.Equals(requestUri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(requestUri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase) &&
        requestUri.Port == baseUri.Port &&
        requestUri.AbsolutePath.StartsWith(baseUri.AbsolutePath, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<PatientSummary> ParsePatients(string xml)
    {
        var document = ParseDocument(xml);
        return document.Root?.Elements(Fhir + "entry")
            .Select(entry => entry.Element(Fhir + "resource")?.Element(Fhir + "Patient"))
            .Where(patient => patient is not null)
            .Select(patient => ToPatient(patient!))
            .Where(patient => !string.IsNullOrWhiteSpace(patient.Id))
            .ToList() ?? [];
    }

    private static IEnumerable<EncounterSummary> ParseEncounters(XDocument document) =>
        document.Root?.Elements(Fhir + "entry")
            .Select(entry => entry.Element(Fhir + "resource")?.Element(Fhir + "Encounter"))
            .Where(encounter => encounter is not null)
            .Select(encounter => ToEncounter(encounter!)) ?? [];

    private static PatientSummary ToPatient(XElement patient)
    {
        var names = patient.Elements(Fhir + "name").ToList();
        var name = names.FirstOrDefault(item => Value(item, "use") == "official")
            ?? names.FirstOrDefault(item => Value(item, "use") == "usual")
            ?? names.FirstOrDefault();
        var family = Value(name, "family");
        var given = name?.Elements(Fhir + "given").Select(ElementValue)
            .Where(value => !string.IsNullOrWhiteSpace(value)) ?? [];
        var displayName = string.Join(" ", given.Append(family).Where(value => !string.IsNullOrWhiteSpace(value)));

        return new PatientSummary(
            Value(patient, "id"),
            string.IsNullOrWhiteSpace(displayName) ? "Unnamed patient" : displayName,
            ReadIdentifiers(patient),
            EmptyToNull(Value(patient, "birthDate")),
            EmptyToNull(Value(patient, "gender")),
            bool.TryParse(Value(patient, "active"), out var active) ? active : null,
            ParseDate(Value(patient.Element(Fhir + "meta"), "lastUpdated")),
            null);
    }

    private async Task<PatientSummary?> GetPatientByIdAsync(
        string patientId,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        var uri = new Uri(baseUri, "Patient/" + Uri.EscapeDataString(patientId) + "?_format=xml");
        var document = ParseDocument(await GetXmlAsync(uri, baseUri, cancellationToken));
        return document.Root?.Name == Fhir + "Patient" ? ToPatient(document.Root) : null;
    }

    private static IReadOnlyList<PatientSummary> ParsePatientResources(XDocument document) =>
        ReadResources(document, "Patient")
            .Select(ToPatient)
            .Where(patient => !string.IsNullOrWhiteSpace(patient.Id))
            .ToList();

    private static IEnumerable<XElement> ReadResources(XDocument document, string resourceName) =>
        document.Root?.Elements(Fhir + "entry")
            .Select(entry => entry.Element(Fhir + "resource")?.Element(Fhir + resourceName))
            .Where(resource => resource is not null)
            .Select(resource => resource!) ?? [];

    private static string ReadPatientId(XElement encounter)
    {
        var reference = Value(encounter.Element(Fhir + "subject"), "reference");
        if (string.IsNullOrWhiteSpace(reference))
            return string.Empty;

        var marker = "Patient/";
        var markerIndex = reference.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return string.Empty;

        var id = reference[(markerIndex + marker.Length)..];
        var historyIndex = id.IndexOf("/_history/", StringComparison.OrdinalIgnoreCase);
        return historyIndex >= 0 ? id[..historyIndex] : id.TrimEnd('/');
    }

    private static EncounterSummary ToEncounter(XElement encounter)
    {
        var type = encounter.Elements(Fhir + "type").Select(ReadCodeableConcept)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "Not specified";
        var encounterClass = encounter.Element(Fhir + "class");
        var period = encounter.Element(Fhir + "period");
        var provider = Value(encounter.Element(Fhir + "serviceProvider"), "display");
        var location = encounter.Elements(Fhir + "location")
            .Select(item => Value(item.Element(Fhir + "location"), "display"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return new EncounterSummary(
            Value(encounter, "id"),
            ReadIdentifiers(encounter),
            EmptyToNull(Value(encounter, "status")) ?? "unknown",
            EmptyToNull(Value(encounterClass, "display")) ?? EmptyToNull(Value(encounterClass, "code")) ?? "Not specified",
            type,
            ParseDate(Value(period, "start")),
            ParseDate(Value(period, "end")),
            EmptyToNull(location) ?? EmptyToNull(provider) ?? "Not specified");
    }

    private async Task<IReadOnlyList<EncounterSummary>> AddObservationCountsAsync(
        IReadOnlyList<EncounterSummary> encounters,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        var results = new EncounterSummary[encounters.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, encounters.Count),
            new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var encounter = encounters[index];
                var uri = new Uri(baseUri,
                    "Observation?encounter=" + Uri.EscapeDataString(encounter.Id) +
                    "&_summary=count&_total=accurate&_format=xml");
                var document = ParseDocument(await GetXmlAsync(uri, baseUri, token));
                var totalText = Value(document.Root, "total");
                if (!int.TryParse(totalText, out var total) || total < 0)
                    throw new InvalidOperationException("The FHIR server did not return a valid observation count for _summary=count.");
                results[index] = encounter with
                {
                    ObservationCount = total
                };
            });

        return results;
    }

    private static ObservationSummary ToObservation(XElement observation)
    {
        var code = ReadCodeableConcept(observation.Element(Fhir + "code") ?? new XElement(Fhir + "code"));
        var value = ReadObservationValue(observation);
        var effective = Value(observation, "effectiveDateTime");
        if (string.IsNullOrWhiteSpace(effective))
            effective = Value(observation, "issued");
        var interpretation = observation.Elements(Fhir + "interpretation")
            .Select(ReadCodeableConcept)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "";
        var referenceRange = observation.Elements(Fhir + "referenceRange")
            .Select(ReadReferenceRange)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "";

        return new ObservationSummary(
            Value(observation, "id"),
            string.IsNullOrWhiteSpace(code) ? "Unnamed observation" : code,
            string.IsNullOrWhiteSpace(value) ? "No value recorded" : value,
            EmptyToNull(Value(observation, "status")) ?? "unknown",
            ParseDate(effective),
            interpretation,
            referenceRange);
    }

    private static string ReadObservationValue(XElement observation)
    {
        var quantity = observation.Element(Fhir + "valueQuantity");
        if (quantity is not null)
        {
            var number = Value(quantity, "value");
            var unit = EmptyToNull(Value(quantity, "unit")) ?? EmptyToNull(Value(quantity, "code"));
            return string.Join(" ", new[] { number, unit }.Where(item => !string.IsNullOrWhiteSpace(item)));
        }

        foreach (var name in new[] { "valueString", "valueCode", "valueDateTime", "valueInteger", "valueBoolean" })
        {
            var value = Value(observation, name);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        var concept = observation.Element(Fhir + "valueCodeableConcept");
        return concept is null ? string.Empty : ReadCodeableConcept(concept);
    }

    private static string ReadReferenceRange(XElement range)
    {
        var text = Value(range, "text");
        if (!string.IsNullOrWhiteSpace(text)) return text;

        var low = range.Element(Fhir + "low");
        var high = range.Element(Fhir + "high");
        var lowText = FormatQuantity(low);
        var highText = FormatQuantity(high);
        return string.IsNullOrWhiteSpace(lowText) && string.IsNullOrWhiteSpace(highText)
            ? string.Empty
            : $"{lowText} – {highText}".Trim(' ', '–');
    }

    private static string FormatQuantity(XElement? quantity)
    {
        if (quantity is null) return string.Empty;
        var value = Value(quantity, "value");
        var unit = EmptyToNull(Value(quantity, "unit")) ?? EmptyToNull(Value(quantity, "code"));
        return string.Join(" ", new[] { value, unit }.Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    private static IReadOnlyList<string> ReadIdentifiers(XElement resource) =>
        resource.Elements(Fhir + "identifier")
            .Select(identifier =>
            {
                var value = Value(identifier, "value");
                var system = Value(identifier, "system");
                return string.IsNullOrWhiteSpace(system) ? value : $"{system} | {value}";
            })
            .Where(value => !string.IsNullOrWhiteSpace(value)).ToList();

    private static string ReadCodeableConcept(XElement concept)
    {
        var text = Value(concept, "text");
        if (!string.IsNullOrWhiteSpace(text)) return text;
        var coding = concept.Elements(Fhir + "coding").FirstOrDefault();
        return EmptyToNull(Value(coding, "display")) ?? EmptyToNull(Value(coding, "code")) ?? string.Empty;
    }

    private static Uri? GetNextPageUri(XDocument document, Uri baseUri)
    {
        var next = document.Root?.Elements(Fhir + "link")
            .FirstOrDefault(link => Value(link, "relation") == "next");
        var url = Value(next, "url");
        return string.IsNullOrWhiteSpace(url) ? null : new Uri(baseUri, url);
    }

    private static XDocument ParseDocument(string xml)
    {
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null
        });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private static string Value(XElement? parent, string childName) =>
        parent?.Element(Fhir + childName)?.Attribute("value")?.Value ?? string.Empty;

    private static string ElementValue(XElement element) => element.Attribute("value")?.Value ?? string.Empty;
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static DateTimeOffset? ParseDate(string? value) =>
        DateTimeOffset.TryParse(value, out var date) ? date : null;
}

public sealed record FhirResponse(string RequestUri, string Xml);
