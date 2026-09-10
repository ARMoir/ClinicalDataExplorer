# Audit activity and administration

The Audit page (`/audit`) provides a read-only, newest-first activity log with user and action filters and 50-event cursor paging. Opening, filtering, and paging the log are themselves audited. The top navigation includes Patients, Reports, Census, My lists, Settings, Audit, and the signed-in user.

## Administrator setup

Settings → Administrators defaults **All users are administrators** to on, as requested for initial setup. This also applies to anonymous sessions when authentication is disabled. To restrict access, first enable Windows authentication, enter full Windows account names (one `DOMAIN\username` or UPN per line; semicolons are also accepted), and then turn off the option. Names are compared case-insensitively; they are not AD group names. Include your own account to retain access. The application does not create Windows accounts or change AD permissions.

The Settings form checks administrator access on load and before saves, uploads, and logo removal. Audit reads check current administrator settings on every request, including previously opened sessions. Links remain visible to everyone; non-administrators receive an access-denied message. Windows/domain application authorization still applies independently.

## Recorded activity

Each event stores a sequence ID, UTC timestamp, server-resolved Windows username (or explicitly Anonymous), correlation/session ID, action, target, outcome, and details.

| Activity | Evidence |
| --- | --- |
| HTTP application access | Attempt and status, including application 401/403 responses; remote socket IP on the attempt. Framework transport and static assets served by static-file middleware are excluded. |
| Interactive session and navigation | Connected session and page location, using the Blazor authentication state rather than an identity supplied by JavaScript. |
| All FHIR requests | Attempt, success/failure/cancellation, endpoint and query parameter names; success includes returned resource type/IDs. HTTP 401/403 failures are classified as Denied; HTTP failure events include the numeric status without the response body. Related attempts and outcomes include an operation ID. Includes searches, continuation pages, demographics, references, and report reads. A successful FHIR read means XML was received and parsed, not that subsequent report rendering succeeded. |
| Cached clinical paging and sections | Server-recorded requests for patient/recent-report paging, section expansion/collapse, visibility, and section record paging. |
| Settings | Attempt and completion/failure; attempted changes include before/after values. Connection URL values are omitted to avoid retaining credentials or tokens. Administrator changes are included. Logo uploads/removal are recorded without file content. |
| Printing | The application Print button records a request before opening the dialog. Browser print-dialog observations are separate. Neither event proves that paper was printed or a PDF was saved. |
| Audit review and denied admin access | Server-side events. |
| Saved patient lists | List-directory and membership reads record attempts and successes; membership successes identify returned patient references. Mutations record attempts, with successful changes and their success events committed in one SQLite transaction. Access denials, invalid submissions, stale-server failures, and database failures are recorded with exception types, not messages or submitted values. Unavailable or foreign-list membership reads are denied explicitly. List names are omitted. |
| Document report text | Opening/closing generated Observation document text includes the Observation ID and expanded state in a Client-observed toggle event. Text content is not sent to the audit observer. |
| Other interface actions | Delegated click/change/submit/toggle observations, labeled `Client-observed`, with control labels/IDs and current page. No typed values, rendered text, clipboard contents, passwords, or clinical document bodies are collected. |

Browser observations are supplemental and untrusted: extensions, disabled JavaScript, disconnects, or a modified client can omit or falsify them. They are not evidence of server authorization or successful completion. Page/section visibility does not prove a person read the content. OS actions, screenshots, browser copy/save, and authentication rejected by IIS/AD before reaching the app require host-level controls/logs. This implementation is not a keylogger or a guarantee to observe every possible user action.

Page-view, interface, and PrintRequested targets preserve validated relative `ResourceType/id` references on `/record` and validated report resource IDs on `/reports/view`. Search identifiers, arbitrary external reference URLs, tokens, and other query values remain excluded. Reports opened by a search identifier are correlated to resource IDs through the FhirRead events in the same session; the print event does not independently enumerate the rendered resources.

## Storage and failure behavior

The database is `<content root>/App_Data/audit.sqlite`, outside the web root. It is created on startup and uses SQLite WAL and FULL synchronous commits, parameterized SQL, a 30-second busy timeout, and UPDATE/DELETE rejection triggers on audit events. There is no audit edit/delete UI, purge job, or automatic expiry. Private patient-list tables share this database and support owner-authorized edits and deletion. Protect the `.sqlite`, `-wal`, and `-shm` files. Use SQLite's online backup API or stop the application cleanly before taking a consistent filesystem backup; copying the database alone while WAL is active can omit recent events.

Server audit writes are awaited. An initial write failure prevents the protected FHIR request, settings mutation, audit read, or Print button action. If a completion write fails after an external read or settings save, that work may already have occurred; the error propagates and the persisted attempt remains. Browser observation failures display an alert but cannot undo an action the browser already performed. An unavailable audit store prevents normal startup/request handling instead of silently switching to memory.

Saved-list operations also require an initial audit write before proceeding. A failed mutation completion audit rolls back the list transaction; a failure event is then attempted outside that transaction. If the audit database itself cannot accept writes, the operation fails and cannot guarantee a persisted failure event. Monitor host/application errors and storage availability independently.

## Coverage review and remaining deployment work

This review uses HIPAA audit controls and activity review as the baseline, consistent with the [HHS audit protocol](https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/audit/protocol/index.html). It is an application coverage review, not a determination that every requirement for a particular organization or jurisdiction is satisfied.

| Area | Application evidence | Still required outside this implementation |
| --- | --- | --- |
| Identity and access | Named server-derived actor, UTC time, session/request identifiers, HTTP access status and administrator/list denials | IIS/Windows/AD sign-in failures before the app, account lifecycle and permission changes, reliable host/session correlation |
| Clinical access | FHIR endpoint, operation IDs, resource IDs and outcomes; section/paging requests; supplemental document toggles | Review procedures and organization-specific decisions about access purpose, break-glass workflows, and patient-level reporting |
| Session lifecycle | Connected interactive-session event and navigation | Disconnects, reconnects, process crashes and workstation logoff are not a complete app-level session ledger; retain host logs |
| Printing and disclosure | Application print requests and supplemental browser-dialog observations | Confirmed print/PDF destinations, clipboard, screenshots, downloads outside application controls and workstation monitoring |
| Log protection | Local append-only audit interface and SQLite rejection triggers | Storage ACLs/encryption, consistent backups, off-host tamper-resistant collection and alerting; filesystem administrators can alter local storage |
| Operational review | Administrator filters and paging | Assigned reviewers, review frequency, incident response, retention policy, storage monitoring and evidence of completed reviews |
| Failures | Application failures while audit storage is writable; protected operations fail when prerequisite auditing fails | Independent monitoring for audit outages and startup failures; unavailable storage cannot record its own outage reliably |

No off-host audit collector, automatic alerting, automated retention schedule, or immutable remote archive is configured by this review. Confirm these controls with the deployment owner before claiming readiness. Live IIS/AD authentication and browser-event delivery require testing in the actual deployment.

SQLite is not encrypted by this application. SQL triggers prevent ordinary application changes but do not protect against a filesystem administrator replacing/deleting the database, dropping triggers, or altering the clock. Resource IDs, account names, and IP addresses make this sensitive data even though clinical bodies and search values are omitted.

## HIPAA deployment responsibilities

This feature supports audit controls and review; it is not a HIPAA compliance certification. [HHS's audit protocol](https://www.hhs.gov/hipaa/for-professionals/compliance-enforcement/audit/protocol/index.html) describes audit controls under §164.312(b) and activity review under §164.308(a)(1)(ii)(D).

Before production use, establish organization-specific retention and regular review procedures, restrict administrators, secure file ACLs to the service account and designated administrators, use encrypted storage and protected backups, synchronize clocks, monitor storage capacity and audit failures, and centralize/correlate IIS/AD authentication logs. For protection against privileged tampering, ship audit events to a separately administered immutable store. Test restore procedures and restrict access to backups as carefully as the live database. No organization-specific retention period or review schedule is asserted here.

Validation includes SQLite persistence/concurrent writes, UPDATE/DELETE rejection, injection-resistant filters, administrator defaults and revocation, identity attribution, query-value/body omission, and fail-closed FHIR requests when audit insertion fails.
