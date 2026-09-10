# Patient document grouping

Multiline Observation narratives appear under **Documents**, alongside DocumentReference records. The existing classifier recognizes actual line breaks and literal `\n` sequences in Observation values.

This replaces the earlier DiagnosticReport consolidation. Matching names, dates, or result references no longer absorb an Observation into a DiagnosticReport. Actual diagnostic reports stay in Diagnostic reports; short observations and measurements stay in their original section.

Grouping is for display only. Original FHIR resource types, IDs, references, narrative formatting, and full record details are preserved; nothing is written to the FHIR server. Documents use the same progressive loading, 50-record initial limit, and Load more behavior as other sections.

Regression tests cover both line-break representations, matching report references/name/date, existing documents, scalar observations, preserved input, and escaped narrative rendering.
