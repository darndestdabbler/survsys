# Gap analysis — survsys at 0.8.0 against the legacy systems

> Made on 2026-10-18, after session 57, by auditing the code itself (entities,
> endpoints, hosted services, screens) against
> `../nedss/docs/functional/phase-1.6-feature-matrix.md`, the twelve documents
> in `../nedss/docs/business-logic/`, the four EpiTrax workflow documents and
> `../nedss/docs/specs/`. It is the source of sessions 59 to 101 in
> `docs/sessions/session-plan.md`. Each finding names the session that closes
> it. eCR has its own document, [`ecr-requirements.md`](ecr-requirements.md).

**The headline.** Phase 7 was planned as "the first release that claims parity",
and it cannot claim it. Several pipelines are built as tested libraries that
nothing in the running application calls, and core case work is missing:
a user cannot find an existing case at all. The operator decided on
2026-10-18 that session 58 ships as **0.8.0**, the gaps below are planned as
phases 8 to 13, and **1.0.0** moves to the end.

## Case management (feature matrix §1–§8) — phase 8, sessions 59–66

| Gap | Today | Session |
|---|---|---|
| No case search or events list; record numbers in the work queues, task list and People results are not links | A case can be opened only right after creating it, or from an outbreak or a staged message | 59 |
| No standalone person screen with the person's cases | Person search only | 59 |
| Labs, tests, results, exposures and contacts cannot be edited or deleted; contact disposition cannot be updated; attachments cannot be deleted | Create-only routes | 60, 65 |
| MMWR week and year are worked out from the creation date and never recalculated when the onset date changes | `PersonConditionEndpoints.cs` | 60 |
| Workflow is four forward-only states; no route to another agency, assign to an investigator, do not investigate, reopen, approve or reject by the state, or transition history | `WorkflowRules.cs` | 61 |
| No event types (morbidity, contact, assessment); a contact cannot become a case of their own; contacts are added by typed person id with no search or "+ New" | `ExposureContact` | 62 |
| Batch CSV contact import checks the file and saves nothing, and has no screen | `ContactImportEndpoints.cs` | 62 |
| No per-case treatments, encounters, occupation or pregnancy; vaccine manufacturer and information source left out; no investigator, brief note, or separate state and local status in the header | — | 63 |
| Every coded field is a free-text box: no specimen source, test type, organism, disposition, task category, race, ethnicity, language, county or attachment category lists | — | 64 |
| No case delete (soft) and "show deleted", print or PDF, record locking or view and edit mode, or note privacy; no outbreak attachments | — | 65 |

## Master person record — phase 9, sessions 68–71

| Gap | Today | Session |
|---|---|---|
| No person identifiers (MRN with assigning authority, CLIA or OID), no name history, no current-versus-historic address rule, no address normalisation; aliases are not searched | `Person`, `PersonAddress` (county only, no geocode) | 68 |
| `PersonMatchRules` is never called; person search shows no match score; the ELR reviewer picks every person by hand | `Survsys.Messaging/PersonMatchRules.cs` | 69 |
| No duplicate-person finder and no person merge | `PersonSearch.cs` says dedup is "later" | 70 |

## Inbound ELR, EMSA (§9) — phase 10, sessions 73–79

| Gap | Today | Session |
|---|---|---|
| Intake is a file-drop poller only: no MLLP listener, no ACK or NAK, no sending-facility (MSH-4) check, one staged row per file rather than per OBR | `ElrIntakeService.cs` | 73 |
| The parse result is thrown away (`_ = new OruR01Parser().Parse(raw)`); no parsed fields are stored; no message type or format column; no parser abstraction for CDA later | `StagedMessage` holds raw text | 73 |
| `VocabularyMatcher` and `ExpressionEvaluator` are never called; vocabulary tables are never read at runtime and have no screens or import; no zip-to-agency routing on intake | library only | 74 |
| `GraylistRules` is never called; no graylist states, spooler, release or pruner; only 4 of 14 queue states, `pending` never written, `QaFlagged` never set; no move, delete, retry or bulk actions | library only | 75 |
| Promotion opens a bare case: the message's labs and person data are not written; no update-existing-case path (whitelist rules, lab comparison, lab knitting, susceptibility); promotion is two saves and can leave a duplicate case | `MessageQueueEndpoints.cs` | 76 |
| No exception types with allow-retry, no reprocessing, no batch notification e-mail, no intake statistics, no EMSA message audit trail | — | 77 |
| None of the Connecticut ELR rules: CT OIDs, CLIA-or-OID in PID-3, ORC-2/3, OBR-2/3 and SPM-2, OBR-16, OBR-17, ORC-24 and OBX-2 required, 169 CT towns, blood-lead NK1, usage codes R, RE, C(R/X), C(RE/X), C(R/RE), C(R/O), I, O | parser enforces no conformance | 78 |

## Outbound CDC notification, NMI (§10) — phase 11, sessions 81–85

| Gap | Today | Session |
|---|---|---|
| Nothing takes a case off `nmi_export_queue`; `OruR01Generator`, `PhinVariableMapper`, `RepeatingGroupProcessor` and the rule-set formatters have no caller; `CdcTransmitter` is registered and never used; nothing writes `nmi_log` | library only | 81 |
| Auto-queue fires only on add, status, assignment and workflow, never on lab, clinical, investigation, administrative or demographic edits; no condition print criteria; no remove, retraction (NOT118 = X), resend, specific-records or MMWR-range export; the reconciliation cutoff is a struct, not a stored setting | `CaseNotificationQueue` | 82 |
| No guide, guide-variable-default, rule-set, PHIN value set or unit-of-measure administration or import; no hepatitis lab grouping or organism and test-type overrides; arbovirus two-OBR and NK1 mother segment deferred | entities only | 83 |
| Transport is a plain HTTP POST: no PHINMS or file-out handoff, no batch-file tracking, no log severities or detail, no retention | `CdcTransmitter.cs` | 84 |
| NETSS fixed-width export | deliberately not built; see [`netss-export-question.md`](netss-export-question.md) | not planned until the operator records CDC's answer |

## Administration, audit, reporting, classification (§11–§14) — phase 12, sessions 87–91

| Gap | Today | Session |
|---|---|---|
| The audit records neither the user nor the values before and after; no audit screen; no access-audit screen; logins, exports and searches are not audited; no vocabulary change audit | `AuditInterceptor` | 87 |
| No jurisdiction scoping of case access (privileges are method and path only); no account lockout; data protection keys not persisted, so sign-in breaks on an app-pool recycle or a second server; the Forms and Treatments buttons need `manage_reference_data` while their API writes need only `edit_cases` | `PrivilegeRules.cs`, both `Program.cs` | 88 |
| No classification on save: no disease-specific rules (chlamydia, gonorrhea, hepatitis C, influenza, syphilis, measles, COVID), no automatic out-of-state status, no automatic close of CT, GC and syphilis contacts, no jurisdiction from the patient's address | `CaseStatusRules.cs` | 89 |
| IBIS has no screen; ad-hoc reporting is one case-count report; no system configuration table or screen; condition configuration lacks guide, print criteria and EMSA flags | `IbisEndpoints.cs`, `/api/reports/case-counts` | 90 |

## eCR — phase 13, sessions 93–98

survsys has no eCR support: nothing parses CDA, eICR or RR. Legacy EMSA took
eICRs as `CLINICAL_DOCUMENT` messages through Mirth and per-sender XSLT into
its master XML, put the clinical content into case notes, and never processed
the Reportability Response. What a 2026 receiver needs is in
[`ecr-requirements.md`](ecr-requirements.md).

## Deliberately not planned

Named so their absence is a decision rather than an oversight. Each is the
operator's to add.

- **Syndromic surveillance** (priority 2 in `../nedss/docs/specs/README.txt`); no legacy system had it.
- **The EpiTrax IIS lookup** (`../nedss/legacy/nedss-vaccine/`, a FHIR query to the immunization registry).
- **Operational hardening** beyond the defects in session 88: structured logging and telemetry, health checks beyond readiness, rate limiting, OpenAPI, CI, backup and retention runbooks.
- **Connecticut's Maven and CTEDSS hand-off**: MIF XML export, the 12-hour batching and the HTML scrubbing exist because CT's eCR pipeline feeds Maven; survsys is the surveillance system those feed. Verato UMPI needs an external contract and credentials.
- **NETSS**, as above.
