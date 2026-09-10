# Clinical Data Explorer

.NET 10 Blazor Web App for exploring FHIR R4 clinical data. It provides patient search, customizable patient and diagnostic reports, a location census, private patient lists, and an administrator audit log. Clinical records are read from the configured FHIR server; application settings, branding uploads, audit events, and saved list references are stored locally.

## Run and test

Install the .NET 10 SDK, then run from the repository root:

```powershell
dotnet restore
dotnet run
```

Windows authentication is enabled by default. Configure the FHIR endpoint and branding in **Settings**. The default endpoint is `http://summittest:8080/`; replace it with the endpoint for your environment.

```powershell
dotnet test ClinicalDataExplorer.sln
```

Offline tests use synthetic records and scripted HTTP responses. Live FHIR tests are opt-in and skipped by default. See [testing and server compatibility](docs/testing.md) for HAPI and Microsoft FHIR Server test instructions. Passing offline tests does not verify a deployed server or its authentication configuration.

## Build and deployment files

Build and publish outputs include `wwwroot/` (including branding files), all files under `XSLT/`, `appsettings*.json`, and `App_Data/application-settings.json`, preserving their directory structure. Changed source files are copied with `PreserveNewest`.

For deployment to another machine, create a publish folder and deploy its entire contents:

```powershell
dotnet publish ClinicalDataExplorer.csproj -c Release -o .artifacts/publish
```

The build copies resolved static web assets, including Blazor's framework scripts and compressed variants, into its output `wwwroot/` so a Production launch does not depend on the SDK/package cache. Publishing remains the recommended deployment workflow and also produces the deployment static asset manifest. Launch from the deployed folder so the content root resolves `XSLT/` and `App_Data/` correctly.

The app creates `App_Data/`, its audit database, and `wwwroot/uploads/` as needed at startup. Preserve the destination's runtime settings, uploaded logos, and audit database when updating an existing installation; copying packaged settings can replace local configuration. The application identity needs write access to these runtime data directories.

## Navigation

| View | Address | Purpose |
| --- | --- | --- |
| Patients | `/` | Search patients and browse recent encounters |
| Patient report | `/patient/{id}` | Demographics and expandable clinical sections |
| Reports | `/reports` | Find diagnostic reports by identifier or browse recent reports |
| Diagnostic report | `/reports/view?identifier=...` or `?reportId=...` | View a report with patient and encounter context |
| Encounter report | `/encounter/{id}` | Encounter details and observations |
| Census | `/census` | Current encounters grouped by location |
| My lists | `/patient-lists` | Manage private saved patient lists |
| Referenced record | `/record?reference=...` | View a supported linked FHIR record |
| Settings | `/settings` | Administrator configuration and branding |
| Audit | `/audit` | Administrator activity review |

The previous `/location-census` and `/diagnostic-report` addresses remain available. The shared footer contains **System & reference information**.

## Patient directory and providers

The directory shows **five patients per page**, ordered by most recent encounter. Search by patient identifier / MRN, last name, first name, or both names. New searches reset to page one. Next follows server continuation links on demand; Previous returns to retained pages. Recent-patient discovery deduplicates patients across encounter pages while preserving newest-encounter order.

Patient cards show identifiers, demographics, contact details, record ID, and associated providers. Expand provider identifiers to see names, references, identifier values, and issuing systems. Missing or incomplete provider lookups are shown explicitly. Recent browsing has a five-second per-patient lookup budget; slow records may be skipped or have incomplete provider details. Searching for a patient specifically removes that short browsing budget, while ordinary request timeouts and paging limits still apply.

For signed-in users, **Save to patient list** appears below associated providers in the directory, matching their compact text size. Patient cards link directly to the patient report.

## Patient reports

Patient demographics load first. Clinical sections are discovered progressively from `Patient/{id}/$everything`, including supporting resources returned by the server. Up to **50 records per section** are initially available; loading pauses when a section reaches its limit. **Find more categories and records** resumes discovery, and **Load more (up to 50)** increases a section's available records. Sections display **ten records per page**.

Counts describe records available so far and can be partial while loading is active or paused, or after a failure. Previously loaded content remains visible with an explicit incomplete-results message if loading fails; **Retry report** restarts loading. After discovery completes, **Show types with no returned records** makes empty patient-compartment categories available. Zero records returned is not proof of clinical absence.

Controls let users find sections, choose visible sections, expand/collapse sections, and page through records. Sections start collapsed; choices are local to the current report view. Populated sections appear before empty ones. Within the clinical reading order, **Related persons** is followed by:

1. **Observations and measurements**
2. **Documents**
3. Medication sections: reported medications, medication orders, administrations, dispensed medications, and the medication catalog

Records are sorted newest first using relevant clinical dates, with update timestamps as a fallback and undated records last. Each record retains **All record details**, including nested fields, extensions, contained resources, and references. Supported references link to patient, encounter, report, or referenced-record views; unsupported references remain readable.

### Observations, measurements, and documents

Tabular observations and component measurements show **Test, Result, Units, Flag, Reference range, Date, Provider, and Status**, with notes and full record details beneath the result. Quantity comparators are retained. The Units column prefers the recorded unit label, falls back to its code, and shows `—` when neither is supplied. Units are not inferred or converted. Abnormal and critical flags are visually distinguished. Other report templates may show units inline with their results.

Observations containing multiline text, including literal `\n` separators, are presented in **Documents**. This is display grouping only; the underlying FHIR resource remains an Observation. Report text is **collapsed by default**: click **View report text** to reveal formatted headings, labeled lines, spacing, and separators. Original record details remain available separately.

**Print visible pages** opens the browser print dialog for expanded patient sections and their currently displayed record pages. Expand any document text you want to view before printing and check the browser preview. Printing does not fetch remaining pages or records.

## Diagnostic and encounter reports

**Reports** accepts a report identifier and displays recent diagnostic reports when no report is selected. Recent-report cards include patient demographics and provider associations when available, dates, IDs, and links to the report, with ten records per page and refresh controls.

A diagnostic report includes patient demographics when available, links to its patient and encounter, a branded report body, and a Print button. Failed loads offer Retry. Encounter reports display encounter details and associated observations with a Print button.

Diagnostic report searches request included results and iterative provider references:

```text
_include=DiagnosticReport:result
_include:iterate=Observation:performer
_include:iterate=PractitionerRole:practitioner
```

Provider presentation uses display names and resolves PractitionerRole, Practitioner, and supported Organization references where available. Unresolved references remain visible as a fallback. Searches follow continuation pages and deduplicate included resources. A server that rejects required includes returns a search error; verify support against the target deployment.

## Census

**Census** groups current encounters by location and links to encounter and patient reports. Locations are ordered by newest current encounter start date; encounters within each location are newest first. Undated encounters follow dated encounters, with alphabetical location tie-breaking. Encounters lacking a current location assignment appear under **No current location recorded**.

The census loads `Location` and `Encounter?status=arrived,triaged,in-progress,onleave&_include=Encounter:patient`. Future and ended encounter periods are excluded. Current location assignments must be active or have no status, and fall within their recorded period. Completed, planned, and reserved assignments are excluded. An encounter with multiple current assignments appears at each location; the overall encounter count is distinct. Search failures display errors rather than an empty or partial census.

Search by location name or ID, patient name, or patient identifier / MRN. Location matches retain all encounters at that location; patient matches retain only matching patients. **Hide empty locations is checked by default.** Uncheck it to include empty locations, which sort last. **Clear filters** clears the search and unchecks Hide empty locations. Refresh retains the current filters, reloads the census, and updates its load time. Counts compare filtered results with the full loaded census. Signed-in users can save patients to their lists directly from Census.

## Saved patient lists

**My lists** supports creating, renaming, and deleting named private lists such as Follow-up or Chart review. List selection and creation are grouped side by side; rename/delete controls appear below for the selected list. Fields and buttons use consistent sizing and stack on small screens. Deleting a list requires confirmation in the page.

Use **Save to patient list** from the directory, Census, or a patient report to select an existing list or create one and save. When opening My lists or the save-patient controls, the first list in alphabetical order is selected automatically if no valid selection exists. Refreshing or reopening the controls preserves a valid current selection. Saving still requires clicking Save patient. Duplicate saves retain one membership. Remove individual patients from a list at any time. Removing patients or deleting lists never changes FHIR records.

Lists require a named authenticated account, even if application authentication is disabled. They are scoped to the account (Windows SID where available) and configured FHIR server URL. Administrators do not gain access to another user's lists through this feature. Reload the page after changing servers; lists for other servers remain stored and reappear when switching back.

Lists load ten patients at a time, fetching current demographics and the latest encounter using `Encounter?patient=...&_sort=-date&_count=1`. Refresh retrieves details again. Each patient links to their report, and a returned latest encounter links to its encounter report. Unavailable details display an error and can be retried with Refresh; affected patients can still be removed.

List names, owners, server URLs, and patient IDs are stored in tables within `App_Data/audit.sqlite`. Demographics and clinical records are fetched from FHIR rather than copied into list storage. List mutations and their audit events commit together in one SQLite transaction. Audit events identify lists and affected patient references without storing list names.

## System and reference information

Expand **System & reference information** in the footer to access:

- **Server information:** the connected server's capability statement.
- **Terminology services:** terminology capabilities or available terminology information.
- **Reference library:** searchable shared categories advertised by the server, such as directories, terminology, forms, and definitions.

Views load on demand and offer refresh and continuation-based paging where applicable. Reference categories exclude ordinary patient records, which remain in patient reports. A footer lookup failure is displayed locally and does not replace the patient view.

## Settings and branding

Settings requires administrator access. Configure:

- Product and facility names.
- FHIR base URL.
- Facility logo: PNG, JPG, or WebP, up to 5 MB; upload or remove it.
- Primary and secondary colors, with branding preview.
- Authentication mode and optional Windows domain restriction.
- Whether all users are administrators, or a list of administrator account names.

Settings persist in `App_Data/application-settings.json`. Example:

```json
{
  "ProductName": "Clinical Data Explorer",
  "FacilityName": "Your Facility",
  "FhirBaseUrl": "http://summittest:8080/",
  "AuthenticationMode": "Windows",
  "WindowsDomain": "",
  "AllUsersAreAdministrators": true,
  "AdministratorUsers": "",
  "LogoPath": null,
  "PrimaryColor": "#1F618D",
  "SecondaryColor": "#17202A"
}
```

Names, colors, and the endpoint are read at runtime. Changes do not require rebuilding the application; endpoint changes affect subsequent requests. Uploaded logos are stored in `wwwroot/uploads/`. Branding is applied to the shared header and report templates. The interface includes keyboard focus indicators, a skip-to-content link, responsive layouts, and high-contrast styling.

## Windows authentication and administration

The application uses Negotiate with the Windows identity supplied by IIS, IIS Express, or Kestrel on Windows. It does not collect AD passwords or require a domain-controller address or service-account credentials. The header shows the short and full authenticated identity.

`AuthenticationMode` supports `Windows` and `Disabled`; Disabled is intended for development or troubleshooting. `WindowsDomain` optionally restricts accepted identities. Leave it blank initially to verify the identity reported by the host. Windows sign-in to this application does **not** automatically authenticate outbound FHIR requests.

For IIS, enable the Windows Authentication role service, enable Windows Authentication on the application/site, and disable Anonymous Authentication. Browse from a domain-connected workstation. Friendly DNS names or more complex deployments may require host-specific Kerberos/SPN setup. IIS Express Windows authentication is enabled in the launch settings.

**All users are administrators** defaults to enabled for initial setup, including anonymous users when authentication is disabled. To restrict access, enter full Windows account names, one per line or separated by semicolons, and disable that option. Include your own account. Names are matched case-insensitively; these are account names, not AD groups. Settings and Audit enforce administrator checks even though their navigation links remain visible.

## Audit and local storage

The administrator Audit page displays events newest first with user/action filters and 50-event paging. Events cover application access, interactive sessions and navigation, FHIR reads, report controls and paging, settings changes, printing requests, audit review, and saved-list mutations. Client-observed interface events supplement server events; they do not prove a user read a record or completed an action.

Saved-list reads, denied access, failed mutations, and validation failures are also audited. FHIR 401/403 responses are marked Denied with their HTTP status. Generated document toggles include the Observation ID and expanded state without the report body. Validated resource IDs are retained in supported page/print targets while search values remain excluded. See the [audit coverage review and remaining deployment work](docs/audit.md#coverage-review-and-remaining-deployment-work) for host logging, retention, monitoring, and tamper-resistant storage requirements that are not supplied by application logging alone.

| Location | Contents |
| --- | --- |
| `App_Data/application-settings.json` | Runtime settings |
| `App_Data/audit.sqlite` and SQLite sidecar files | Audit events and private saved-list tables |
| `wwwroot/uploads/` | Uploaded facility logos |

The application identity needs write access to `App_Data` and the uploads directory. Audit storage initializes before requests are accepted. Audit events are append-only through the application, with database triggers rejecting their update/delete; those restrictions do not prevent supported saved-list edits. The application supplies no automatic audit expiry or purge job. Protect and back up the database, including its WAL state, as described in [audit setup, coverage, and deployment requirements](docs/audit.md).

## FHIR behavior and compatibility

The application expects **FHIR R4 XML**. The intended production target is the open-source Microsoft FHIR Server with SQL Server persistence; HAPI R4 is a development integration target. A JSON-only service is not supported by the current XML/XSLT rendering path. Endpoints requiring outbound authorization need that configured separately.

Core queries include:

- `Encounter?_sort=-date&_include=Encounter:patient` for recent-patient discovery.
- `Patient?identifier=...` and `Patient?family=...&given=...` for patient searches.
- `Patient/{id}/$everything` for patient sections and supporting records.
- `Encounter?patient=...&_sort=-date` for patient encounter history.
- `Observation?encounter:Encounter=...&_summary=count` for observation counts.
- `Observation?encounter:Encounter=...&_sort=-date` for encounter observations.

Requests use strict handling for required search features. Patient search values escape literal FHIR delimiters. Continuation links must remain within the configured server/base path; automatic HTTP redirects are disabled. Resource identity deduplication is case-sensitive. Invalid XML, unexpected responses, error/fatal OperationOutcomes, and exceeded safety limits produce explicit errors.

The progressive patient-report stream requests 50 records per server page and enforces a maximum of 200 pages / 10,000 returned resources, rejecting repeated continuation links. Other queries have their own bounded paging limits. The HTTP request timeout is 30 seconds. Unsupported `$everything` leaves demographics available while clinical-section loading reports an error. Validate required sorts, includes, and operations on the actual target server; a capability statement alone is insufficient.

## XSLT customization

Eight stylesheets in `XSLT/` control XML presentation:

- `PatientList.xslt`
- `PatientDetails.xslt`
- `PatientResources.xslt`
- `EncounterList.xslt`
- `EncounterDetails.xslt`
- `ObservationList.xslt`
- `DiagnosticReportBundle.xslt`
- `ServerInformation.xslt`

Razor components handle interactive workflow. Stylesheets receive `facilityName`, `logoPath`, `primaryColor`, and `secondaryColor`; resource views also use view-specific parameters such as `resourceLabel` and `expandDetails`. Stylesheets compile on first use and reload when their timestamps change, allowing presentation edits without recompilation or restart.

Scripts, DTDs, external resolvers, and the XSLT `document()` function are disabled. Server-provided narratives and clinical text are rendered as text rather than executable HTML. Supported record links are resolved within the application's FHIR reference rules.
