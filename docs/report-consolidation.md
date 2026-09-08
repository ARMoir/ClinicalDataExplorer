# Patient diagnostic-report consolidation

Observations already moved to Diagnostic reports because they contain multiline text can be displayed inside an existing DiagnosticReport rather than as another top-level entry.

- An explicit `DiagnosticReport.result` reference takes priority, including relative references, same-server absolute references, and bundle fullUrl references. Version-specific references require the observation's matching `meta.versionId`.
- Otherwise, a unique report with the same normalized display name and recorded clinical calendar day is used. Whitespace and casing in the name are ignored. The date comes from `effectiveDateTime`, `effectivePeriod.start`, or `issued`, in that order; partial dates and administrative `meta.lastUpdated` do not establish a match. This is a day-level heuristic, not proof of resource identity.
- Conflicting populated patient or encounter references prevent grouping. Multiple name/date candidates remain separate. If several reports explicitly reference the same observation, it is included in each applicable report rather than merging those reports together.
- The original observation, its ID, narrative, values, and record details remain available inside the report. The display identifies whether the match was by result reference or name/date.
- Grouping changes the number of displayed report entries and their paging. It does not delete, edit, or write any FHIR server record. Scalar observations not already moved remain in Observations and measurements.

Regression tests cover explicit-reference precedence, absolute/URN references, unique and ambiguous name/date matches, context conflicts, version mismatch, original-input preservation, and escaped narrative rendering.
