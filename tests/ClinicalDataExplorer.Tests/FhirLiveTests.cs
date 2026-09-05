using System.Xml.Linq;
using Xunit;

namespace ClinicalDataExplorer.Tests;

public sealed class LiveFhirFactAttribute : FactAttribute
{
    public LiveFhirFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("FHIR_LIVE_TESTS") != "1")
            Skip = "Set FHIR_LIVE_TESTS=1 to run read-only tests against a live FHIR R4 server.";
    }
}

// One class keeps live tests sequential. Every test has an overall deadline,
// and every HTTP request has a 30-second timeout. No writes or fixed record IDs.
[Trait("Category", "LiveFhir")]
public sealed class FhirLiveTests : IDisposable
{
    private static readonly XNamespace Fhir = "http://hl7.org/fhir";
    private readonly FhirTestContext context = new(
        Environment.GetEnvironmentVariable("FHIR_TEST_BASE_URL") ?? "https://hapi.fhir.org/baseR4/");
    private readonly CancellationTokenSource deadline = new(TimeSpan.FromMinutes(2));

    [LiveFhirFact]
    public async Task Endpoint_advertises_R4_XML_and_required_search_parameters()
    {
        var metadata = await ReadAsync("metadata?_format=xml");
        Assert.Equal(Fhir + "CapabilityStatement", metadata.Root!.Name);
        Assert.StartsWith("4.0.", Value(metadata.Root, "fhirVersion"));
        Assert.Contains(metadata.Root.Elements(Fhir + "format"),
            format => format.Attribute("value")!.Value.Contains("xml", StringComparison.OrdinalIgnoreCase));
        var resources = metadata.Root.Elements(Fhir + "rest").SelectMany(rest => rest.Elements(Fhir + "resource")).ToList();
        foreach (var (type, parameter) in new[]
        {
            ("Patient", "identifier"), ("Encounter", "patient"), ("Encounter", "date"),
            ("Observation", "encounter"), ("Observation", "date"), ("DiagnosticReport", "identifier")
        })
        {
            var resource = Assert.Single(resources, item => Value(item, "type") == type);
            Assert.Contains(resource.Elements(Fhir + "searchParam"), item => Value(item, "name") == parameter);
        }
    }

    [LiveFhirFact]
    public async Task Patient_identifier_search_and_XML_read_round_trip()
    {
        var patient = await SampleAsync("Patient", "identifier:missing=false");
        var id = Value(patient, "id");
        var identifier = Identifier(patient);
        var summaries = await context.Service.SearchPatientsByIdentifierAsync(identifier, deadline.Token);
        Assert.Contains(summaries, item => item.Id == id);
        var read = XDocument.Parse(await context.Service.GetPatientXmlAsync(id, deadline.Token));
        Assert.Equal(Fhir + "Patient", read.Root!.Name);
        Assert.Equal(id, Value(read.Root, "id"));
        var bundle = XDocument.Parse(await context.Service.SearchPatientBundleXmlAsync(identifier, deadline.Token));
        Assert.Contains(Resources(bundle, "Patient"), item => Value(item, "id") == id);
    }

    [LiveFhirFact]
    public async Task Recent_encounters_return_distinct_patients_in_date_order()
    {
        var patients = await context.Service.GetPatientsWithRecentEncountersAsync(deadline.Token);
        Assert.InRange(patients.Count, 1, 10);
        Assert.All(patients, item => Assert.False(string.IsNullOrWhiteSpace(item.Id)));
        Assert.Equal(patients.Count, patients.Select(item => item.Id).Distinct().Count());
        var dates = patients.Where(item => item.MostRecentEncounter.HasValue).Select(item => item.MostRecentEncounter!.Value).ToArray();
        Assert.Equal(dates.OrderDescending(), dates);
    }

    [LiveFhirFact]
    public async Task Encounter_read_history_and_observation_counts_agree()
    {
        var encounter = await SampleAsync("Encounter", "patient:missing=false");
        var id = Value(encounter, "id");
        var patientId = ReferenceId(Value(encounter.Element(Fhir + "subject"), "reference"), "Patient");
        var read = XDocument.Parse(await context.Service.GetEncounterXmlAsync(id, deadline.Token));
        Assert.Equal(Fhir + "Encounter", read.Root!.Name);
        Assert.Equal(id, Value(read.Root, "id"));
        var history = await context.Service.GetPatientEncountersAsync(patientId, deadline.Token);
        var summary = Assert.Single(history, item => item.Id == id);
        var observations = await context.Service.GetEncounterObservationsAsync(id, deadline.Token);
        Assert.Equal(observations.Count, summary.ObservationCount);
        var bundle = XDocument.Parse(await context.Service.GetPatientEncounterBundleXmlAsync(patientId, deadline.Token));
        Assert.Equal(history.Select(item => item.Id).Order(), Resources(bundle, "Encounter").Select(item => Value(item, "id")).Order());
    }

    [LiveFhirFact]
    public async Task Observation_search_returns_a_discovered_observation_and_matching_XML_bundle()
    {
        var observation = await SampleAsync("Observation", "encounter:missing=false");
        var id = Value(observation, "id");
        var encounterId = ReferenceId(Value(observation.Element(Fhir + "encounter"), "reference"), "Encounter");
        var summaries = await context.Service.GetEncounterObservationsAsync(encounterId, deadline.Token);
        Assert.Contains(summaries, item => item.Id == id);
        var bundle = XDocument.Parse(await context.Service.GetEncounterObservationBundleXmlAsync(encounterId, deadline.Token));
        Assert.Equal(summaries.Select(item => item.Id).Order(), Resources(bundle, "Observation").Select(item => Value(item, "id")).Order());
    }

    [LiveFhirFact]
    public async Task Diagnostic_report_search_supports_result_and_iterative_provider_includes()
    {
        var report = await SampleAsync("DiagnosticReport", "identifier:missing=false");
        var response = await context.Service.GetDiagnosticReportBundleXmlAsync(Identifier(report), deadline.Token);
        var bundle = XDocument.Parse(response.Xml);
        Assert.Equal(Fhir + "Bundle", bundle.Root!.Name);
        Assert.Contains(Resources(bundle, "DiagnosticReport"), item => Value(item, "id") == Value(report, "id"));
        // With handling=strict, an unsupported include must fail this test.
        Assert.Contains("_include:iterate=", response.RequestUri);
    }

    [LiveFhirFact]
    public async Task Unknown_identifier_returns_an_empty_search_bundle()
    {
        var identifier = "urn:clinical-data-explorer:test|" + Guid.NewGuid().ToString("N");
        var patients = await context.Service.SearchPatientsByIdentifierAsync(identifier, deadline.Token);
        Assert.Empty(patients);
        var response = await context.Service.GetDiagnosticReportBundleXmlAsync(identifier, deadline.Token);
        Assert.Empty(Resources(XDocument.Parse(response.Xml), "DiagnosticReport"));
    }

    private async Task<XElement> SampleAsync(string resourceType, string filter)
    {
        var document = await ReadAsync($"{resourceType}?{filter}&_count=10&_format=xml");
        Assert.Equal(Fhir + "Bundle", document.Root!.Name);
        var resources = Resources(document, resourceType).ToList();
        Assert.True(resources.Count > 0, $"Live fixture requires an existing {resourceType} matching {filter}. No records are created by these tests.");
        return resources[0];
    }

    private async Task<XDocument> ReadAsync(string path)
    {
        using var response = await context.Client.GetAsync(path, deadline.Token);
        response.EnsureSuccessStatusCode();
        return XDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
    }

    private static string Identifier(XElement resource)
    {
        var identifier = resource.Elements(Fhir + "identifier").First(item => Value(item, "value").Length > 0);
        // Escape FHIR token delimiters before the service URL-encodes the query.
        static string Escape(string value) => value.Replace("\\", "\\\\").Replace("$", "\\$").Replace(",", "\\,").Replace("|", "\\|");
        var system = Value(identifier, "system");
        return (system.Length == 0 ? "" : Escape(system) + "|") + Escape(Value(identifier, "value"));
    }

    private static string ReferenceId(string reference, string resourceType)
    {
        var parts = reference.Split('/');
        var index = Array.LastIndexOf(parts, resourceType);
        Assert.True(index >= 0 && index + 1 < parts.Length, $"Live fixture requires a {resourceType} reference.");
        return parts[index + 1];
    }

    private static IEnumerable<XElement> Resources(XDocument document, string type) =>
        document.Root!.Elements(Fhir + "entry").SelectMany(entry => entry.Elements(Fhir + "resource")).Elements(Fhir + type);

    private static string Value(XElement? element, string name) => element?.Element(Fhir + name)?.Attribute("value")?.Value ?? "";

    public void Dispose()
    {
        deadline.Dispose();
        context.Dispose();
    }
}
