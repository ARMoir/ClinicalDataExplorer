# Testing and FHIR server compatibility

The production target is the open-source [Microsoft FHIR Server](https://github.com/microsoft/fhir-server) with **SQL Server persistence**. The public [HAPI R4 endpoint](https://hapi.fhir.org/baseR4/) is a development integration-test target. Passing HAPI tests does not certify a particular Microsoft deployment, server version, or search-index configuration.

## Offline tests

From the repository root:

```powershell
dotnet test ClinicalDataExplorer.sln
```

The compatibility regressions use scripted HTTP responses and need no running FHIR server. Live tests are skipped by default.

## Live tests

```powershell
$env:FHIR_LIVE_TESTS = '1'
$env:FHIR_TEST_BASE_URL = 'https://hapi.fhir.org/baseR4/'
try {
    dotnet test ClinicalDataExplorer.sln --filter 'Category=LiveFhir'
} finally {
    Remove-Item Env:FHIR_LIVE_TESTS
    Remove-Item Env:FHIR_TEST_BASE_URL
}
```

For Microsoft FHIR Server, set `FHIR_TEST_BASE_URL` to that deployment's FHIR R4 base URL and run the same suite. Use a representative Microsoft SQL-backed test deployment before release. Omitting the URL defaults to HAPI.

Tests make only GET requests, discover existing records instead of using fixed IDs, and store settings in a disposable temporary directory. They never change the application's saved endpoint or create/update/delete server resources. No authentication token is configured. Windows sign-in to the web application does not automatically authenticate outbound FHIR requests; an endpoint requiring authorization will fail with 401/403 until outbound authentication is configured separately.

The suite checks R4/XML capabilities and required search parameters, patient identifier searches and XML reads, recent-patient discovery, encounter history, observation counts and bundles, and diagnostic reports with iterative provider includes. It needs existing patients with identifiers, encounters referencing patients, observations referencing encounters, and diagnostic reports with identifiers.

Live tests run sequentially with a 30-second request timeout and a two-minute limit per test. Shared public data can change between requests. Missing sample data, throttling, outages, or very large patient histories can cause failures; failures are not silently skipped or retried. These are compatibility checks, not load tests.

## Differences covered

| Concern | Application behavior and regression coverage |
| --- | --- |
| Unsupported filters, sort, or includes | Send `Prefer: handling=strict`, preventing servers that honor it from silently ignoring required parameters. Preserve HTTP errors and status codes. |
| Pagination | Follow opaque `next` URLs, including HAPI paging parameters and Microsoft continuation tokens; continue across empty pages. Restrict links to the configured server/base path and disable automatic HTTP redirects. |
| Included resources | Read a patient directly if an encounter search omits it. Merge diagnostic-report pages, deduplicate repeated resources, and exclude includes from `Bundle.total`. |
| Resource IDs | Preserve case-sensitive IDs when deduplicating patients and included resources. |
| Observation totals | Require a valid nonnegative `_summary=count` total; do not present a missing/invalid count as zero. |
| Serialization | Require FHIR R4 XML for the application and XSLT reports. Return an explicit compatibility error for JSON responses. |

Microsoft's [search documentation](https://learn.microsoft.com/en-us/azure/healthcare-apis/fhir/overview-of-search) describes backend-dependent behavior. SQL and Cosmos DB capabilities must not be treated as interchangeable. The offline tests model protocol differences, not an actual Microsoft database. The capability statement alone cannot prove sort or iterative-include support, so the live suite also executes the application's queries with strict handling. Successful execution does not prove that every referenced performer exists or is returned; the report retains its raw-reference fallback.

The open-source Microsoft server is distinct from managed Azure offerings: Microsoft's [FHIR FAQ](https://github.com/MicrosoftDocs/azure-docs/blob/main/articles/healthcare-apis/azure-api-for-fhir/fhir-faq.yml) documents XML support in the open-source server and JSON-only behavior in Azure API for FHIR. A JSON-only service would require a proper FHIR serialization adapter before use with these XML reports.

## Patient explorer regression coverage

`PatientExplorerTests` adds offline coverage for recent-patient discovery beyond the first 10, cross-page deduplication, name and identifier escaping, continuation-based search, `$everything` grouping including supporting types, failures that must not become zero counts, server-boundary enforcement, record pagination, branding, and safe recursive XSLT rendering.

Browser smoke testing can use synthetic data with 23 patients and 12 observations to exercise 5/5/5/5/3 patient pages, name search and page reset, direct navigation, record sections, 10/2 observation pages, filtering and section selection. Use more than 50 records in a section to exercise progressive discovery and Load more. Check the Units column, component values, collapsed document report text, and section order beneath Related persons. Census should initially hide empty locations; Clear filters should reveal them. Use a separate temporary content root/settings file for such fixtures; do not change deployed authentication or server settings.

`PatientListTests` covers private-list persistence, account and server isolation, anonymous denial, duplicate saves, case-sensitive patient IDs, mutation auditing and rollback on audit failure, and current patient/latest-encounter retrieval. Browser verification should use a named authenticated test account to exercise save controls in the directory, Census, and patient report, plus list creation, selection, rename, deletion confirmation, removal, refresh, and paging. Check control layouts at desktop and narrow widths. These offline tests do not replace authenticated browser or live FHIR verification.
