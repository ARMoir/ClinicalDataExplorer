# Clinical Data Explorer

Production-oriented .NET 10 Blazor Web App foundation for exploring clinical data from a FHIR server. The current report view retrieves a `DiagnosticReport` search Bundle, includes result Observations and performer resources, and displays both a formatted HTML report and syntax-highlighted raw XML.

## Run

```powershell
dotnet restore
dotnet run
```

## Application settings

Open `/settings` from the top navigation to configure:

- Product name (user-facing application title)
- Facility name
- FHIR base URL
- Facility logo (PNG, JPG, WebP; max 5 MB)
- Primary color
- Secondary color

Settings are persisted to:

`App_Data/application-settings.json`

Example:

```json
{
  "ProductName": "Clinical Data Explorer",
  "FacilityName": "Your Facility",
  "FhirBaseUrl": "http://summittest:8080/",
  "LogoPath": null,
  "PrimaryColor": "#1F618D",
  "SecondaryColor": "#17202A"
}
```

The product name and FHIR base URL are read from these application settings at runtime. Changing the product name updates the user-facing application header/browser titles, while changing the FHIR base URL affects the next API request without rebuilding the application.

Uploaded logos are stored under:

`wwwroot/uploads/`

If hosted under IIS or a Windows service account, the application identity needs write permission to those two locations for settings/logo changes to persist.

The branding is used by the shared application header and is also passed into `DiagnosticReportBundle.xslt` as XSLT parameters at report-render time.

## Report formatting

`XSLT/DiagnosticReportBundle.xslt` handles a FHIR search `Bundle`, separates long/multiline `Observation.valueString` content from ordinary result Observations, and applies generic narrative formatting. The stylesheet receives these application parameters:

- `facilityName`
- `logoPath`
- `primaryColor`
- `secondaryColor`

The XSLT is compiled once when the application starts. Application branding remains runtime-configurable because values are passed through `XsltArgumentList` for each transformation.

## Provider name resolution

DiagnosticReport searches request:

```text
_include=DiagnosticReport:result
_include:iterate=Observation:performer
_include:iterate=PractitionerRole:practitioner
```

The report stylesheet prefers `Reference.display`, then resolves included `PractitionerRole` and `Practitioner` resources to a human-readable provider name. Direct Practitioner and Organization performers are also supported. If the FHIR server does not support iterative includes or does not return the referenced resource, the raw performer reference is shown as a fallback.

## Current report columns

Scalar Observation results currently display:

- Test
- Result
- Interpretation / abnormal flag
- Reference range
- Observation date
- Provider
- Status
- Observation notes directly beneath the associated result
