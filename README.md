# Clinical Data Explorer

Production-oriented .NET 10 Blazor Web App foundation for exploring clinical data from a FHIR server. The main page lists the 10 most recently updated patients, supports Patient identifier searches, and loads the complete paged Encounter history for a selected patient.

## Patient explorer

The main page uses standard FHIR searches:

- `Encounter?_sort=-date&_include=Encounter:patient` to find the 10 distinct patients with the most recent encounters
- `Patient?identifier=...` to search across patient identifiers
- `Encounter?patient=...&_sort=-date` for the selected patient's encounters
- `Observation?encounter=...&_summary=count` for encounter observation counts
- `Observation?encounter=...&_sort=-date` to open an encounter's complete observation list

Encounter Bundle pagination links are followed automatically, while paging is restricted to the configured FHIR server.

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

Clinical presentation is customizable through six XSLT templates:

- `PatientList.xslt`
- `PatientDetails.xslt`
- `EncounterList.xslt`
- `EncounterDetails.xslt`
- `ObservationList.xslt`
- `DiagnosticReportBundle.xslt`

The dashboard handles interactive workflow in Razor, while dedicated patient, encounter, observation, and diagnostic-report views render FHIR XML through these templates. Each stylesheet receives these application parameters:

- `facilityName`
- `logoPath`
- `primaryColor`
- `secondaryColor`

Stylesheets are compiled on first use and automatically reloaded when their file timestamp changes. They can therefore be customized without recompiling or restarting the application. Scripts, DTDs, external resolvers, and the XSLT `document()` function remain disabled.

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

## Windows / Active Directory authentication

Clinical Data Explorer can use the Windows account already signed in on the user's workstation. It doesn't collect or store AD passwords and it doesn't require a domain-controller address or AD service-account credentials.

Authentication is controlled in `App_Data/application-settings.json` and on the Settings page:

```json
"AuthenticationMode": "Windows",
"WindowsDomain": ""
```

- `AuthenticationMode`: currently `Windows` or `Disabled`. `Disabled` is intended only for development/emergency troubleshooting.
- `WindowsDomain`: optional domain restriction such as `EMERSON`. Leave it blank initially so you can confirm the exact authenticated identity shown in the upper-left header.

The header shows both the short user name and the full Windows identity, for example `AMoir` and `EMERSON\\AMoir`.

### Local development

The project includes the `Microsoft.AspNetCore.Authentication.Negotiate` package and enables Windows Authentication for IIS Express in `Properties/launchSettings.json`. The normal Kestrel project profiles also use Negotiate when run on Windows.

### IIS deployment

On the Clinical Data Explorer IIS application/site:

1. Install/enable the IIS **Windows Authentication** role service if it isn't already installed.
2. Set **Windows Authentication = Enabled**.
3. Set **Anonymous Authentication = Disabled**.
4. Browse from a domain-connected workstation using Edge/Chrome. The browser normally supplies the active Windows account automatically on an intranet site.

For friendly DNS names or more complex deployments, Kerberos/SPN configuration may eventually be needed. Okta/OIDC can later replace Windows authentication without changing the application's authorization/audit model.
