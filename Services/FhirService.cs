namespace ClinicalDataExplorer.Services;

public sealed class FhirService(
    IHttpClientFactory httpClientFactory,
    ApplicationSettingsService settingsService)
{
    public async Task<FhirResponse> GetDiagnosticReportBundleXmlAsync(
        string identifier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("An identifier is required.", nameof(identifier));

        var client = httpClientFactory.CreateClient("Fhir");

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

        // Read the current JSON-backed setting for every request so a URL change
        // takes effect immediately without recreating the service or restarting.
        var configuredBaseUrl = settingsService.Current.FhirBaseUrl;

        if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "The configured FHIR base URL is invalid. Open Settings and enter an absolute HTTP or HTTPS URL.");
        }

        // Ensure relative FHIR resource paths append to the configured base path.
        if (!baseUri.AbsoluteUri.EndsWith('/'))
            baseUri = new Uri(baseUri.AbsoluteUri + "/");

        var requestUri = new Uri(baseUri, relativeUrl);

        using var response = await client.GetAsync(requestUri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"FHIR server returned {(int)response.StatusCode} {response.ReasonPhrase}.\n{body}");
        }

        var actualRequestUri = response.RequestMessage?.RequestUri?.ToString()
            ?? requestUri.ToString();

        return new FhirResponse(actualRequestUri, body);
    }
}

public sealed record FhirResponse(string RequestUri, string Xml);
