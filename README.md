# Clinical Data Explorer

Production-oriented .NET 10 Blazor Web App for exploring clinical data from a FHIR server. The centered patient directory shows 10 patients per page, ordered by most recent encounter, with identifier/MRN and last/first-name search. Selecting a patient opens a branded, customizable report with demographics and expandable resource sections.

## Patient explorer

The main page uses standard FHIR searches:

- `Encounter?_sort=-date&_include=Encounter:patient` to find the 10 distinct patients with the most recent encounters
- `Patient?identifier=...` to search across patient identifiers
- `Patient?family=...&given=...` to search by last name, first name, or both
- `Patient/[id]/$everything` to retrieve patient records and supporting resources across all result pages
- `Encounter?patient=...&_sort=-date` for the selected patient's encounters
- `Observation?encounter=...&_summary=count` for encounter observation counts
- `Observation?encounter=...&_sort=-date` to open an encounter's complete observation list

Encounter Bundle pagination links are followed automatically, while paging is restricted to the configured FHIR server.

## Census

Open **Census** next to Reports (`/census`) to see all locations and their current encounters, with links to encounter and patient reports. Locations are ordered by their newest current encounter start date, and encounters within each location are newest first. Undated encounters follow dated encounters; empty locations appear last, with alphabetical ordering for ties. Encounters without a current assignment appear under **No current location recorded**. Refresh reloads the census and updates its displayed load time. The previous `/location-census` address remains available.

The census searches `Location` and `Encounter?status=arrived,triaged,in-progress,onleave&_include=Encounter:patient`, following all pages within the existing safety limits. Future and ended encounter periods are excluded. Location assignments must be active (or have no status) and within their recorded period. Completed, planned and reserved assignments are excluded. Encounters with multiple current assignments appear in each location; the overall encounter count is distinct. Search failures are displayed as errors rather than an empty or partial census.

## Run

```powershell
dotnet restore
dotnet run
```

## Tests

See [testing and server compatibility](docs/testing.md) for offline tests and opt-in live tests against HAPI or Microsoft FHIR Server with SQL Server persistence.

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

Clinical presentation is customizable through seven XSLT templates:

- `PatientList.xslt`
- `PatientDetails.xslt`
- `PatientResources.xslt`
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

The report stylesheet prefers `Reference.display`, then resolves included `PractitionerRole` and `Practitioner` resources to a human-readable provider name. Direct Practitioner and Organization performers are also supported. If a referenced resource is not returned, the raw performer reference is shown as a fallback. A server that rejects iterative includes produces a search error under strict handling; verify support on the deployed server using the live tests. Diagnostic-report searches follow all result pages and deduplicate repeated included resources.

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


## Expanded patient report

The patient report requires the FHIR R4 [Patient $everything operation](https://hl7.org/fhir/R4/patient-operation-everything.html). This retrieves patient-compartment records plus supporting records such as Practitioner, Organization, Medication, Device and Binary, as returned by the server for the current access context. Resource types are discovered from the response, so uncommon or additional returned types automatically receive sections. The R4 patient compartment catalog is available through **Show types with no returned records**; zero means no records returned, not proof of clinical absence.

All continuation pages are loaded and resources deduplicated by case-sensitive type/id before counts are shown. Repeated paging links, links outside the configured server, non-Bundle responses, and error/fatal OperationOutcomes fail explicitly. A maximum of 100 pages / 10,000 returned resources prevents unbounded loading; exceeding the limit reports an error without presenting partial counts. Servers that do not implement `$everything` show a resource-count error while demographics remain visible. Verify this operation on the target Microsoft/HAPI deployment before relying on the expanded report.

Report controls support finding sections, choosing visible sections, expanding/collapsing sections, and paging through 10 records within each section. Choices are local to the current report view. **Print visible pages** prints expanded sections and their current record pages. Counts describe complete returned resources, while contained resources and repeated nested elements remain inside their parent resource's **All FHIR fields** view.

`PatientDetails.xslt` renders demographics and the configured facility logo/colors. `PatientResources.xslt` supplies resource summaries and a recursive field renderer that preserves choice values, extensions, references, contained resources and nested fields. Server-provided narratives are rendered as text rather than executable HTML; reference/attachment URLs are displayed as text. Customize these templates to change presentation without recompiling.

Directory pagination follows server continuation links on demand and retains earlier pages for Previous. Recent-patient discovery deduplicates patients across encounter pages, retaining the original newest-encounter order. Identifier and name searches escape literal FHIR delimiters, follow next links, and reset to page 1 for a new search.
## Audit activity

The Audit page records application activity in SQLite and is available to administrators. All users are administrators by default for initial setup; Settings lets you restrict this to named Windows accounts. See [audit setup, coverage, and deployment requirements](docs/audit.md).
