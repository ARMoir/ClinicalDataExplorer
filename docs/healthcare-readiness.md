# Healthcare readiness — Massachusetts, staff-facing application

Status: **not certified or verified as fully compliant**. Scope supplied by the owner: Massachusetts, USA; internal staff use; possible substance-use treatment records. Assessment started September 7, 2026. Organization type, HHS funding, actual data classifications, infrastructure, and operational controls remain to be established.

## Technical work and verification

- Added a keyboard skip link and main landmark target, a visible underline for automatically focused headings, focus indicators, small-screen layout adjustments, and forced-colors styles.
- Added labels to logo/color controls, settings validation/status semantics, table column scopes, and keyboard-focusable table scrolling regions. Collapsed patient sections retain their referenced DOM IDs.
- Darkened low-contrast secondary text. Rendered brand colors are adjusted to at least 7:1 against white without changing saved settings. This does not establish contrast conformance for every state or combination.
- Added no-store, no-referrer, nosniff, and same-origin frame response headers.
- Existing SQLite auditing records server operations and separately labels supplemental browser observations. See [audit coverage and limitations](audit.md).
- Build and .NET regression tests passed after these changes. No complete WCAG assessment has been performed. The proposed axe browser scan did **not run**: automatic approval review rejected the isolated application launch due to a usage-limit error. Keyboard, screen-reader, zoom/reflow, forced-colors, real browser print/PDF output, and assistive-technology testing remain outstanding. No zero-violation or accessibility-conformance claim is made.

## Required decisions and evidence before production

| Area | Current gap / required action | Owner |
| --- | --- | --- |
| Accessibility | Target WCAG 2.2 AA; determine legal applicability of HHS Section 504, employment accessibility, and public-entity requirements. Test complete workflows with keyboard and screen readers, including loading/error/empty states, 200% text sizing, 400% zoom, high contrast, and generated documents. Record results and remediate findings. | Accessibility lead / HR / legal |
| Administrator and clinical access | All users are administrators by the user's setup preference. Disable this for production after naming authorized accounts. Define clinical access roles and upstream patient-level authorization; current broad browsing and `$everything` cannot enforce an unspecified access policy. | Privacy/security officer |
| Part 2 and sensitive records | No implemented consent, provenance classification, restricted-disclosure policy, or segregation workflow. A substance-use mention alone does not determine Part 2 status. Establish source-program coverage, permitted use/consent, restrictions, and enforcement at the FHIR server/app before enabling access to covered records. | Privacy/legal and data owners |
| Massachusetts information security | Determine whether held data meets the statutory personal-information definition. Establish a WISP, assigned security ownership, training, vendor oversight, access reviews, secure disposal, and required encryption under applicable MA law. HIPAA PHI and MA personal information are not identical definitions. | Security/legal |
| Encryption and endpoint security | Verify TLS browser-to-app and app-to-FHIR, host/disk encryption, file ACLs, key protection, patching, endpoint controls, and protected backups. SQLite audit data is not application-encrypted. | Infrastructure/security |
| Audit protection and operations | Define retention, routine review, capacity/failure alerting, consistent backups and restore tests, time synchronization, external tamper-resistant storage, and IIS/AD log correlation. Browser observations and print requests are not proof of every user action or completed print. | Security/operations |
| Breach response | Adopt coordinated HIPAA and Massachusetts incident assessment/notification procedures; determine applicable recipients, deadlines, and responsibilities. | Privacy/legal |
| Risk analysis and continuity | Document the actual systems/data flows, risk assessment, remediation decisions, contingency plan, downtime process, and tested recovery. | Organization management |
| Contracts and disclosures | Determine covered-entity/business-associate roles, BAAs, vendor/subprocessor responsibilities, permitted disclosures, and record-retention obligations. | Legal/privacy |

## Primary references

- [HHS Security Rule summary](https://www.hhs.gov/hipaa/for-professionals/security/laws-regulations/index.html)
- [HHS risk analysis guidance](https://www.hhs.gov/hipaa/for-professionals/security/guidance/final-guidance-risk-analysis/index.html)
- [Massachusetts 201 CMR 17.00](https://www.mass.gov/regulations/201-CMR-1700-standards-for-the-protection-of-personal-information-of-ma-residents)
- [Massachusetts breach notification requirements](https://www.mass.gov/info-details/requirements-for-data-breach-notifications)
- [HHS Part 2 guidance](https://www.hhs.gov/hipaa/part-2/index.html)
- [HHS accessibility compliance-date announcement](https://www.hhs.gov/press-room/hhs-extends-mobile-and-web-accessibility-deadline.html)
- [EEOC visual disabilities in the workplace](https://www.eeoc.gov/laws/guidance/visual-disabilities-workplace-and-americans-disabilities-act)
- [WCAG 2.2](https://www.w3.org/TR/WCAG22/)
