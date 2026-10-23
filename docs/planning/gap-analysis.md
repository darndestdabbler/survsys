# Gap analysis — survsys against the legacy systems

> First made on 2026-10-18, after session 57, by auditing the code itself
> (entities, endpoints, hosted services, screens) against
> `../nedss/docs/functional/phase-1.6-feature-matrix.md`, the twelve documents
> in `../nedss/docs/business-logic/`, the four EpiTrax workflow documents and
> `../nedss/docs/specs/`. It was the source of sessions 59 to 106 in
> `docs/sessions/session-plan.md`. Checked again on 2026-10-22, in session 109,
> for **1.0.0**: each gap below names the session that closed it, and
> [the feature matrix](#the-feature-matrix-at-100) gives every legacy feature
> its survsys status. eCR has its own document,
> [`ecr-requirements.md`](ecr-requirements.md).

**The headline at 1.0.0.** At 0.8.0 survsys could not claim parity: several
pipelines were tested libraries that nothing in the running application
called, and a user could not find an existing case. The operator decided on
2026-10-18 that session 58 would ship as 0.8.0, that the gaps would be planned
as phases 8 to 13, and that 1.0.0 would move to the end. Every gap found then
is closed. What is left open is named under
[Still open](#still-open), each with whose decision it is, and the legacy
behaviour survsys deliberately departs from is under
[Departures from the legacy classification](#departures-from-the-legacy-classification).

The session numbers planned in 2026-10-18 moved as repair sessions were
inserted; the numbers below are the sessions that did the work.

## Case management (feature matrix §1–§8) — phase 8, sessions 59–67

| Gap at 0.8.0 | Then | Status at 1.0.0 |
|---|---|---|
| No case search or events list; record numbers in the work queues, task list and People results are not links | A case can be opened only right after creating it, or from an outbreak or a staged message | Closed, session 59 |
| No standalone person screen with the person's cases | Person search only | Closed, session 59 |
| Labs, tests, results, exposures and contacts cannot be edited or deleted; contact disposition cannot be updated; attachments cannot be deleted | Create-only routes | Closed, session 60 |
| MMWR week and year are worked out from the creation date and never recalculated when the onset date changes | `PersonConditionEndpoints.cs` | Closed, session 60 |
| Workflow is four forward-only states; no route to another agency, assign to an investigator, do not investigate, reopen, approve or reject by the state, or transition history | `WorkflowRules.cs` | Closed, session 61 |
| No event types (morbidity, contact, assessment); a contact cannot become a case of their own; contacts are added by typed person id with no search or "+ New" | `ExposureContact` | Closed, session 62 (session 66 made the link from one report to another load it) |
| Batch CSV contact import checks the file and saves nothing, and has no screen | `ContactImportEndpoints.cs` | Closed, session 62 |
| No per-case treatments, encounters, occupation or pregnancy; vaccine manufacturer and information source left out; no investigator, brief note, or separate state and local status in the header | — | Closed, session 63 |
| Every coded field is a free-text box: no specimen source, test type, organism, disposition, task category, race, ethnicity, language, county or attachment category lists | — | Closed, session 64; the lists are maintained, imported and exported on `/codes` (session 83) |
| No case delete (soft) and "show deleted", print or PDF, record locking or view and edit mode, or note privacy; no outbreak attachments | — | Closed, session 65 |

## Master person record — phase 9, sessions 69–73

| Gap at 0.8.0 | Then | Status at 1.0.0 |
|---|---|---|
| No person identifiers (MRN with assigning authority, CLIA or OID), no name history, no current-versus-historic address rule, no address normalisation; aliases are not searched | `Person`, `PersonAddress` (county only, no geocode) | Closed, session 69, except geocoding: see [Still open](#still-open) |
| `PersonMatchRules` is never called; person search shows no match score; the ELR reviewer picks every person by hand | `Survsys.Messaging/PersonMatchRules.cs` | Closed, session 70 |
| No duplicate-person finder and no person merge | `PersonSearch.cs` says dedup is "later" | Closed, session 71 |

## Inbound ELR, EMSA (§9) — phase 10, sessions 75–86

| Gap at 0.8.0 | Then | Status at 1.0.0 |
|---|---|---|
| Intake is a file-drop poller only: no MLLP listener, no ACK or NAK, no sending-facility (MSH-4) check, one staged row per file rather than per OBR | `ElrIntakeService.cs` | Closed, session 75 |
| The parse result is thrown away (`_ = new OruR01Parser().Parse(raw)`); no parsed fields are stored; no message type or format column; no parser abstraction for CDA later | `StagedMessage` holds raw text | Closed, session 75; the CDA parser came in session 101 |
| `VocabularyMatcher` and `ExpressionEvaluator` are never called; vocabulary tables are never read at runtime and have no screens or import; no zip-to-agency routing on intake | library only | Closed, session 76 |
| `GraylistRules` is never called; no graylist states, spooler, release or pruner; only 4 of 14 queue states, `pending` never written, `QaFlagged` never set; no move, delete, retry or bulk actions | library only | Closed, session 77 |
| Promotion opens a bare case: the message's labs and person data are not written; no update-existing-case path (whitelist rules, lab comparison, lab knitting, susceptibility); promotion is two saves and can leave a duplicate case | `MessageQueueEndpoints.cs` | Closed, session 78; session 82 repaired its address and result-value faults |
| No exception types with allow-retry, no reprocessing, no batch notification e-mail, no intake statistics, no EMSA message audit trail | — | Closed, session 79 |
| None of the Connecticut ELR rules: CT OIDs, CLIA-or-OID in PID-3, ORC-2/3, OBR-2/3 and SPM-2, OBR-16, OBR-17, ORC-24 and OBX-2 required, 169 CT towns, blood-lead NK1, usage codes R, RE, C(R/X), C(RE/X), C(R/RE), C(R/O), I, O | parser enforces no conformance | Closed, session 80; session 81 added the planning regions as county equivalents |

Sessions 83 and 84, inserted in this phase, put every reference table on an
administration screen with view, edit, CSV import and export, as the operator
asked.

## Outbound CDC notification, NMI (§10) — phase 11, sessions 88–92

| Gap at 0.8.0 | Then | Status at 1.0.0 |
|---|---|---|
| Nothing takes a case off `nmi_export_queue`; `OruR01Generator`, `PhinVariableMapper`, `RepeatingGroupProcessor` and the rule-set formatters have no caller; `CdcTransmitter` is registered and never used; nothing writes `nmi_log` | library only | Closed, session 88 |
| Auto-queue fires only on add, status, assignment and workflow, never on lab, clinical, investigation, administrative or demographic edits; no condition print criteria; no remove, retraction (NOT118 = X), resend, specific-records or MMWR-range export; the reconciliation cutoff is a struct, not a stored setting | `CaseNotificationQueue` | Closed, session 89 |
| No guide, guide-variable-default, rule-set, PHIN value set or unit-of-measure administration or import; no hepatitis lab grouping or organism and test-type overrides; arbovirus two-OBR and NK1 mother segment deferred | entities only | Closed, session 90 |
| Transport is a plain HTTP POST: no PHINMS or file-out handoff, no batch-file tracking, no log severities or detail, no retention | `CdcTransmitter.cs` | Closed, session 91; log severities and detail came in session 88 |
| NETSS fixed-width export | deliberately not built; see [`netss-export-question.md`](netss-export-question.md) | Open: not planned until the operator records CDC's answer |

## Administration, audit, reporting, classification (§11–§14) — phase 12, sessions 94–99

| Gap at 0.8.0 | Then | Status at 1.0.0 |
|---|---|---|
| The audit records neither the user nor the values before and after; no audit screen; no access-audit screen; logins, exports and searches are not audited; no vocabulary change audit | `AuditInterceptor` | Closed, session 94 |
| No jurisdiction scoping of case access (privileges are method and path only); no account lockout; data protection keys not persisted, so sign-in breaks on an app-pool recycle or a second server; the Forms and Treatments buttons need `manage_reference_data` while their API writes need only `edit_cases` | `PrivilegeRules.cs`, both `Program.cs` | Closed, session 95 |
| No classification on save: no disease-specific rules (chlamydia, gonorrhea, hepatitis C, influenza, syphilis, measles, COVID), no automatic out-of-state status, no automatic close of CT, GC and syphilis contacts, no jurisdiction from the patient's address | `CaseStatusRules.cs` | Closed, sessions 96 and 97, with the three departures below |
| IBIS has no screen; ad-hoc reporting is one case-count report; no system configuration table or screen; condition configuration lacks guide, print criteria and EMSA flags | `IbisEndpoints.cs`, `/api/reports/case-counts` | Closed, session 98 |

## eCR — phase 13, sessions 101–106

At 0.8.0 survsys had no eCR support: nothing parsed CDA, eICR or RR. Legacy
EMSA took eICRs as `CLINICAL_DOCUMENT` messages through Mirth and per-sender
XSLT into its master XML, put the clinical content into case notes, and never
processed the Reportability Response. What a 2026 receiver needs is in
[`ecr-requirements.md`](ecr-requirements.md). Closed: eICR and RR parsing and
document intake (session 101), validation and the eICR viewer (102),
reportability and condition mapping (103), cases from eCR (104), senders and
data quality (105), and their walkthroughs (106).

## Departures from the legacy classification

Session 96 built the rules of `CLASSIFICATION_RULES.md` with three departures,
which session 97 left for this analysis to decide. Each stands at 1.0.0, for
the reason given; changing one is the operator's to plan.

- **An out-of-state case keeps its agency.** The legacy moves a case whose
  patient lives in another state to its "Out of State" agency. survsys has no
  such agency, so `ClassificationRules` gives the case the Out of State status
  and leaves its investigating agency as it was.
- **A facility encounter stands in for an influenza hospitalization.** The
  legacy reads a hospitalization field on the case. survsys has no
  hospitalization field, so the influenza-associated hospitalization rule reads
  an encounter at a facility as the hospitalization on record, and an
  encounter's save runs the classifier (session 97).
- **Contacts close at the LHD's acceptance.** The legacy closes a chlamydia,
  gonorrhea or syphilis contact at `approved_by_lhd`. survsys's workflow
  (`WorkflowRules`) has no such state, so the contact closes when the LHD
  accepts it.

## The feature matrix at 1.0.0

Every row of `../nedss/docs/functional/phase-1.6-feature-matrix.md`, with its
legacy disposition (**Port**, **Simplify** or **Drop**) and what survsys does.
**Built** means the feature runs in the application; a session number names
where it was built and, after a semicolon, where it was finished.

### 1. Case management

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| CMR creation (manual) | Port | Built, session 14 |
| CMR creation (from ELR) | Port | Built, session 40; 78 |
| CMR editing (all tabs) | Port | Built, sessions 15, 19, 24–27; 60, 63, 65 |
| Record number generation | Port | Built, session 14 |
| Person search | Port | Built, session 14; 69, 70 |
| Person merge/dedup | Port | Built, session 71 |
| Workflow state machine | Port | Built, session 21; 61 |
| Case status assignment | Port | Built, session 20; 63, 96 |
| Multi-agency access | Port | Built, session 21; 95 |
| Work queues | Port | Built, session 21; 59 |
| MMWR week/year | Port | Built, session 20; 60 |

### 2. Demographics and people

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Person demographics | Port | Built, sessions 13, 15; 64 |
| Address management | Port | Built, sessions 13, 15; 69, 81, 83, without latitude and longitude: see [Still open](#still-open) |
| Phone/email | Port | Built, sessions 13, 15 |
| Race (multi-select) | Port | Built, sessions 13, 15; 64 |
| Aliases | Port | Built, session 13; 69 |
| Parent/guardian | Port | Built, session 13 |
| Occupation | Port | Built, session 63 |
| Pregnancy status | Port | Built, session 63 |

### 3. Laboratory

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Manual lab entry | Port | Built, sessions 18, 19; 60 |
| ELR lab creation | Port | Built, session 78 |
| Lab result interpretation | Port | Built, sessions 18, 37; 76 |
| Antimicrobial susceptibility | Port | Built, session 18; 60, 78 |
| LOINC vocabulary | Port | Built, session 37; 76 |
| Specimen source codes | Port | Built, session 64; 76 |
| Lab test types | Port | Built, session 64; 76 |

### 4. Contacts and exposures

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Contact creation | Port | Built, session 25; 62 |
| Exposure events | Port | Built, session 25; 60 |
| Exposure contacts | Port | Built, session 25; 60, 62 |
| Exposure sources | Port | Built, session 25; 60 |
| Contact disposition | Port | Built, session 25; 60, 64 |
| Contact import (batch) | Port | Built, session 62 |

### 5. Outbreaks

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Outbreak management | Port | Built, session 51; 65 |
| Multi-agency access | Port | Built, session 51 |
| Outbreak reports | Port | Built, session 51 |
| Outbreak updates | Port | Built, session 51 |

### 6. Tasks and notes

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Task management | Port | Built, session 26; 64 |
| Recurring tasks | Port | Built, session 26 |
| Clinical notes | Port | Built, session 26; 65 |
| Note strikethrough | Port | Built, session 26 |

### 7. Forms and questionnaires

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Dynamic form builder | Port | Built, session 52 |
| Form elements (nested) | Port | Built, session 52 |
| Form answers | Port | Built, session 52 |
| Condition-specific forms | Port | Built, session 52 |

### 8. Treatments and vaccines

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Treatment tracking | Port | Built, session 63 |
| Condition-specific treatments | Port | Built, session 53; 84 |
| Vaccine records | Port | Built, session 53; 63, 84 |

### 9. Inbound ELR processing (EMSA)

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| HL7 message intake | Port | Built, session 39; 75 |
| HL7 preprocessing | Port | Built, session 36; 75, 80 |
| Vocabulary matching | Port | Built, session 37; 76 |
| Graylist (duplicate detection) | Port | Built, session 38; 77 |
| Person matching | Port | Built, session 38; 70 |
| Message queue management | Port | Built, sessions 39, 40; 77. Fifteen states rather than the legacy 24: the rest were Mirth and XML plumbing survsys does not have |
| Semi-automated entry | Port | Built, session 77 |
| Exception handling | Port | Built, session 79. The legacy exception types this tier can raise, each with the legacy retry flag; the rest belong to Mirth, XSLT and SQL agents survsys does not have |
| Batch notifications | Port | Built, session 79 |
| Out-of-state processing | Simplify | Simplified: an out-of-state queue state (session 77) and the Out of State case status (session 96) |
| Interagency messaging | Port | Simplified: a message is routed to the agency of the patient's zip at intake (session 76), and a case is routed to another agency through the workflow (session 61) |
| Mirth channel management | Drop | Dropped: intake runs as hosted services in the API, by file drop and MLLP (sessions 39, 75) |

### 10. Outbound CDC notification (NMI)

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Auto-queue on case change | Port | Built, session 46; 89 |
| Notification guides | Port | Built, session 43; 90 |
| PHIN variable mapping | Port | Built, session 43; 88, 90 |
| 7 rule set types | Port | Built, session 44; 90 |
| Repeating groups | Port | Built, session 44; 88 |
| HL7 ORU_R01 generation | Port | Built, session 45; 88, 90 |
| Export queue → Mirth | Drop | Dropped: the queue worker sends by HTTP or writes files for a PHINMS or AIMS sender (sessions 88, 91) |
| NETSS export (legacy) | Simplify | Not built: open until the operator records CDC's answer in [`netss-export-question.md`](netss-export-question.md) |
| Reconciliation cutoff | Port | Built, session 46; 89 |
| Guide variable defaults | Port | Built, session 90 |
| Condition print criteria | Port | Built, session 89; 98 |
| Hepatitis lab grouping | Port | Built, session 90 |
| Organism/test type overrides | Port | Built, session 90 |

### 11. Administration

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| User management | Port | Built, session 32; 95 |
| Role-based access control | Port | Built, session 32; 95 |
| Agency/jurisdiction management | Port | Built, session 30; 83 |
| Condition configuration | Port | Built, session 30; 84, 98 |
| Facility management | Port | Built, session 30; 84 |
| Core element configuration | Port | Built, session 31 |
| System configuration | Port | Built, session 98 |

### 12. Reporting and export

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Custom data exports | Port | Built, session 33 |
| Saved views | Port | Built, session 33 |
| AVR (ad-hoc reporting) | Port | Built, session 55; 98 |
| IBIS integration | Port | Built, session 55; 98 |
| CDC export log | Port | Built, session 46; 88, 91 |
| EMSA intake statistics | Simplify | Simplified: a statistics screen by sender, state and day (session 79) |

### 13. Attachments and documents

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| File attachments | Port | Built, session 27; 60, 65 |
| Attachment categories | Port | Built, session 27; 64 |

### 14. Audit and logging

| Feature | Legacy | survsys at 1.0.0 |
|---|---|---|
| Database audit triggers | Simplify | Simplified: EF Core interceptors (session 54; 94) |
| Access audit | Port | Built, session 32; 94 |
| EMSA message audit trail | Port | Built, session 79 |
| NMI export log | Port | Built, session 46; 88 |
| Vocabulary change audit | Port | Built, session 94 |

Of the matrix's 92 rows, 85 are built; 4 are simplified (out-of-state
processing, interagency messaging, EMSA intake statistics and database audit
triggers); 2 are dropped, as the matrix asks (Mirth channel management, and
export queue → Mirth); and 1 is open (NETSS).

## Still open

Each is the operator's to decide or to plan.

- **NETSS fixed-width export**: waits on CDC's answer, recorded in
  [`netss-export-question.md`](netss-export-question.md).
- **Address geocoding**: an address keeps no latitude or longitude
  (`PersonAddress` leaves the legacy geocoding columns out), and nothing
  geocodes one. No session plans it; the operator has not decided it.
- **Attachment and note dates are UTC dates.** Found by session 109's
  walkthrough regression: the Attachments table and the notes list show the
  date of a moment stored in UTC without converting it, so in the evening
  they read tomorrow's date. Planned as session 110, before the release.
- **Jurisdiction scoping stops short of some lists.** Found writing the 1.0.0
  guides: `CaseAccess` narrows every case route and the case search, but not
  the work queue and agency board (`/api/work-queue`), the task list
  (`/api/tasks`), saved-view exports or the reports, and a case shared
  read-only is read-only in name only. Not planned; the operator's to
  decide.
- **No screen settles a held eICR, downloads or removes an attachment, or
  reads a system setting.** Each has its API (or, for system settings, its
  store) and no use on a screen. Not planned; the operator's to decide.

## Deliberately not planned

Named so their absence is a decision rather than an oversight. Each is the
operator's to add.

- **Syndromic surveillance** (priority 2 in `../nedss/docs/specs/README.txt`); no legacy system had it.
- **The EpiTrax IIS lookup** (`../nedss/legacy/nedss-vaccine/`, a FHIR query to the immunization registry).
- **Operational hardening** beyond the defects closed in session 95: structured logging and telemetry, health checks beyond readiness, rate limiting, OpenAPI, CI, backup and retention runbooks.
- **Connecticut's Maven and CTEDSS hand-off**: MIF XML export, the 12-hour batching and the HTML scrubbing exist because CT's eCR pipeline feeds Maven; survsys is the surveillance system those feed. Verato UMPI needs an external contract and credentials.
- **NETSS**, as above.
