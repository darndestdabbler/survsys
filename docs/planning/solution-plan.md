# Solution plan — `survsys`

> Made from `brief.md`, which is the operator's own answer. Where this plan and
> the brief disagree, the brief is what was asked for and this plan is wrong.
>
> Session 2 challenges every cut below and decides each deferred cut, then
> breaks these phases into numbered sessions.

---

## Objective

Replace the three legacy disease-surveillance systems under `../nedss` —
EpiTrax, EMSA and NMI — with one .NET 10 / MudBlazor application over SQL
Server, in which an ASP.NET Core API tier mediates every database call. The
objective is met when public-health staff can run the whole surveillance cycle
in `survsys` without opening a legacy system: create and investigate a case
morbidity report, enter and review laboratory results, move a case through its
workflow across agencies, review staged inbound electronic laboratory reports,
and send a notification onward to CDC. At every release along the way the
operator can open `docs/walkthroughs/`, follow a walkthrough step by step
without help, and have it work exactly as written — because a Playwright test
performed those same steps, in that order, before the release shipped.

## Production split

The operator took the default.

| Part | Runs | May talk to the database |
|---|---|---|
| **Application tier** — `Survsys.Web`, MudBlazor | separately, as its own IIS site | **No** |
| **API tier** — `Survsys.Api`, ASP.NET Core | separately, as its own IIS site | **Yes — only this tier** |
| **Database** — SQL Server | separately | — |

The application tier calls the API tier over HTTP. The API tier is the only
thing that opens a connection to SQL Server. The application tier never holds a
connection string.

**This is a rule, not a convention, and the skeleton session proves it
mechanically.** The architecture test asserts that the `Survsys.Web` assembly
references neither `Survsys.Data` nor `Microsoft.EntityFrameworkCore`. A later
session that wires the UI straight to the DbContext — the easiest wrong turn in
a Blazor Server application, because the UI runs on the server and *could* —
fails that test instead of shipping. The rule is enforced at the point where it
would be broken: the compile-time reference graph, not a review note.

## Handoff artifacts

What each separately running part is handed over as. **The real artifacts are
produced later, by a packaging session of its own** that writes the
`packaging:` block in `dabbler.yaml` and does nothing else; what follows is the
form each takes and the standard command for that form.

| Part | Form | Standard command |
|---|---|---|
| `Survsys.Web` | IIS site package | `dotnet publish` |
| `Survsys.Api` | IIS site package | `dotnet publish` |
| Database | idempotent SQL migration script | `dotnet ef migrations script --idempotent` |

The migration script is idempotent deliberately: the database is handed to
whoever administers the target server, and they must be able to run it against
an instance whose migration level they do not know without having to find out
first.

## Modules

Four modules and three test projects. **The cut is deliberately small.**
Over-engineering is forbidden, and the operator asked for simplicity twice —
once as a requirement and once as a measure of the work.

**Every module is a `.csproj` listed in the root solution file, and reaches a
sibling only by a `<ProjectReference>`.** No module reaches another by any other
means. **The skeleton session writes all seven projects**, their references and
the solution file; later phases fill them in and never re-create them.

### `Survsys.Contracts`

The request and response shapes both tiers share. Records and enums, no
behaviour.

- **Going in:** nothing. It references no sibling module and no infrastructure package.
- **Coming out:** one set of wire shapes that both tiers compile against, so the wire contract is a thing in the repository rather than an agreement between two codebases.
- **How it fails:** it cannot fail at runtime, having no behaviour. A breaking change fails the build of *both* tiers at once — which is the reason it exists.

### `Survsys.Data`

The entities, the `DbContext`, the entity configuration, the migrations, and the
rules that belong with them.

- **Going in:** a SQL Server connection string, for a database at or above the migration level this assembly carries.
- **Coming out:** entities read and written through the `DbContext` within a transaction the caller controls. The schema is whatever the migrations in this module say, and nothing outside this module creates or alters it.
- **How it fails:** EF Core and SQL Server exceptions surface to the caller unwrapped — this module never translates a failure into an HTTP concern. A database behind the assembly's migrations fails at startup, not at the first query that happens to touch a missing column.

### `Survsys.Api`

The ASP.NET Core API tier. **The only module that reaches the database.**
References `Survsys.Data` and `Survsys.Contracts`.

- **Going in:** a configured connection string, and a caller authorised for the resource (once authorisation arrives in its phase).
- **Coming out:** resource-oriented endpoints — `/api/cases`, `/api/persons`, `/api/labs` — returning `Survsys.Contracts` types and never an entity type. Collections are paged. Errors are Problem Details (RFC 7807).
- **How it fails:** a malformed request is `400` with Problem Details; a missing resource is `404`; a `Survsys.Data` failure is `500` with Problem Details carrying a correlation id and no SQL text, server path or connection detail.

### `Survsys.Web`

The MudBlazor application tier — every screen the operator walks through.
References `Survsys.Contracts` **alone**.

- **Going in:** a reachable API base address. Never a connection string.
- **Coming out:** screens driven only by data the API returned, carrying stable element identifiers that the walkthrough tests select by — so a walkthrough's wording and its test select the same control.
- **How it fails:** an API error becomes a message visible on the screen, never an unhandled exception in a Blazor circuit. This tier cannot fail *at* the database, because it cannot reach it.

### Test projects

| Project | Covers | Going in | How it fails |
|---|---|---|---|
| `Survsys.UnitTests` | `Survsys.Contracts`, and the pure rules in `Survsys.Data` | nothing external — no database, no HTTP, no browser | ordinary xUnit failure; these stay fast and deterministic |
| `Survsys.IntegrationTests` | `Survsys.Data` and `Survsys.Api` together, against a real SQL Server | a reachable SQL Server; the legacy `../nedss/docs/db-snapshots/` dumps as fixtures | a missing SQL Server fails loudly — never silently skips, because a skipped integration suite reads as a green run |
| `Survsys.E2ETests` | the walkthroughs, through a browser | `Survsys.Web` and `Survsys.Api` running, a seeded database, Playwright browsers installed | a failure names the walkthrough document and the step number that could not be performed |

## Dependency direction

One acyclic chain:

```
                    Survsys.Contracts         (references nothing)
                     ▲               ▲
                     │               │
              Survsys.Web      Survsys.Api ──▶ Survsys.Data ──▶ EF Core ──▶ SQL Server
             (MudBlazor)        (the only tier that may reach the database)
```

Forbidden, and each for a reason:

| Forbidden | Why |
|---|---|
| `Survsys.Web` → `Survsys.Data` | would put the database inside the application tier and void the production split — **this is the one the architecture test asserts** |
| `Survsys.Web` → `Microsoft.EntityFrameworkCore` | the same breach, arrived at without naming `Survsys.Data` — asserted too |
| `Survsys.Web` → `Survsys.Api` | the tiers run separately; a project reference would make a compile-time dependency out of an HTTP one |
| `Survsys.Data` → `Survsys.Contracts` | persistence must not know the wire shapes, or the wire contract starts dictating the schema |
| `Survsys.Data` → `Survsys.Api` | inverts the chain |
| `Survsys.Contracts` → anything | it is the shared leaf; a reference out of it is a cycle waiting to happen |

## Why these cuts

**`Survsys.Contracts` exists so that `Survsys.Web` can be kept off
`Survsys.Data`.** That is its whole justification. Without it, the application
tier needs the request and response types from somewhere, the shortest path is a
reference to `Survsys.Data`, and the production split degrades into a rule
people remember. With it, the split is a property of the reference graph that a
test reads in one assertion. One extra project buys a mechanically provable
architecture; that is a cut that pays for itself.

**`Survsys.Data` holds entities and persistence together** because splitting a
pure domain module out of it today would be structure without content. There is
no domain rule written yet. The classification rules, the HL7 mappings and the
notification rulesets — the logic that would justify a pure module — arrive in
phases 2, 5 and 6. Cutting the seam now would mean guessing where it goes
before anything is there to put on either side of it, and a guessed seam is
harder to move than an absent one. The cut is listed below as deferred, to be
made when the content exists to shape it.

**Four modules, not the conventional Domain / Infrastructure / Application /
Api / Web five-or-six.** The extra layers in that convention earn their keep on
a solution with several front ends or several databases. This one has one of
each.

## Module challenge

Session 2's work. Every project was held to three questions — **should it
exist**, **is its contract a promise something can prove**, and **does the
dependency direction hide a decision likely to change**. All four modules and
all three test projects stand. Two contracts turned out to be promises nothing
proved; both are now assertions, named below and written by the skeleton
session.

### `Survsys.Contracts` — stands

- **Should it exist:** yes, and it is the only project here whose existence is load-bearing rather than conventional. Delete it and `Survsys.Web` needs its request and response types from somewhere; the shortest path is `Survsys.Data`, and the production split stops being provable. It earns its place by what it makes impossible.
- **Provable:** yes, and without a test — having no behaviour it cannot fail at runtime, and a breaking change fails the build of both tiers at once.
- **Hidden decision:** none. It is the leaf; nothing it references can change under it.

### `Survsys.Data` — stands

- **Should it exist:** yes. The entities, the `DbContext`, the configuration and the migrations are one concern — the schema — and nothing outside this module may create or alter it.
- **Provable — it was not, and now is.** The contract promised that a database behind the assembly's migrations "fails at startup, not at the first query that happens to touch a missing column", and nothing in the plan made that happen. **It becomes a startup assertion in `Survsys.Api`:** on start the API tier reads the pending-migration list from the `DbContext` and refuses to serve while it is non-empty, so a database below the assembly's migration level is one loud startup failure rather than a `500` on whichever request first touches the missing column. `Survsys.Api` asserts it because `Survsys.Api` is the only thing that starts with a connection string.
- **Hidden decision:** the forbidden `Survsys.Data` → `Survsys.Contracts` reference has a consequence the plan left unsaid — **the mapping between entities and wire shapes lives in `Survsys.Api`.** That is the right place, the tier that owns the wire owning the translation to it, but unsaid it is exactly the seam a later session would "tidy" by adding the forbidden reference.

### `Survsys.Api` — stands

- **Should it exist:** yes. It *is* the production split.
- **Provable — it was not, and now is.** "Returning `Survsys.Contracts` types and never an entity type" was a review note, not a test. **It becomes a second architecture test:** no endpoint signature in `Survsys.Api` — parameter or return type, unwrapped through `Task<>`, `ActionResult<>` and collections — names a type from the `Survsys.Data` assembly. The first architecture test fences `Survsys.Web` off the database entirely, so the API tier is the only place an entity can still leak onto the wire, and this is where it is caught.
- **Hidden decision:** authorisation arrives in phase 4 and lands here. Nothing in the direction obstructs it.

### `Survsys.Web` — stands

- **Should it exist:** yes.
- **Provable:** yes — this is the one the first architecture test asserts, and the easiest wrong turn in a Blazor Server application, where the UI runs on the server and therefore *could* open a connection.
- **Hidden decision:** the HTTP client between the tiers is the part most likely to change, and referencing `Survsys.Contracts` alone is what keeps that change inside `Survsys.Web`.

### Test projects — all three stand

- `Survsys.UnitTests` holds both architecture tests, so it is the one project that references every module — it has to read their reference graphs — and that is a consequence of the tests, not a licence for anything else to do the same.
- `Survsys.IntegrationTests` failing loudly on a missing SQL Server, rather than skipping, is the whole of its value: a skipped integration suite reads as a green run.
- `Survsys.E2ETests` **launches `Survsys.Web` and `Survsys.Api` as processes and references neither project.** A project reference from the E2E suite would make the reference graph the architecture tests read no longer the whole truth, and the suite does not need one — it drives a browser against running sites, which is what a walkthrough describes.

## Deferred cuts

Named by session 1 and **decided here, by session 2**. Each carries its
decision, and none is deferred again.

1. **A pure `Survsys.Messaging` module.** Whether nHapi HL7 parsing and
   generation, the EMSA vocabulary and rules engines, and the NMI rulesets and
   PHIN variable mapping should be cut out of `Survsys.Data` into a module with
   no EF Core dependency when the messaging phases arrive. The argument for:
   this is the one body of logic in the solution that is genuinely pure, is the
   hardest to test through a database, and is the most valuable to test
   directly. The argument against: another project before it is needed.

   **Decision:** **adopted — and created by the session that first needs it.**
   `Survsys.Messaging` will exist. A unit test of a LOINC-to-condition match
   should not need SQL Server to run, and this is the one body of logic in the
   solution large and pure enough for that to matter. It is **not** written by
   the skeleton session: a project standing empty from phase 0 through phase 4
   is dead weight. The first inbound-ELR session creates it, adds it to
   `Survsys.sln` and references it from `Survsys.Api`. It references nHapi and
   `Survsys.Contracts`, never EF Core — asserted the same way `Survsys.Web`'s
   fence is asserted, by an architecture test in `Survsys.UnitTests`.
2. **Where inbound ELR intake runs.** As an `IHostedService` inside the API
   tier — the simple answer, which adds no separately handed-over part — or as
   a Worker Service published for a service host, which adds a third handoff
   artifact and the operational question of what happens when it is down. The
   simple answer is the recommendation; the decision belongs with phase 5.

   **Decision:** **an `IHostedService` inside the API tier.** It adds no third
   handoff artifact, nothing further to install on the target server, and no
   operational question about a service host that is down with nobody watching.
   The API tier is already the only thing holding a connection string, which is
   precisely what intake needs. Should intake later have to be scaled or stopped
   independently of the API, lifting an `IHostedService` into a Worker Service
   is a small change — and a handoff artifact is the thing not to add
   speculatively.
3. **A pure domain module out of `Survsys.Data`**, once the classification
   rules of phase 2 exist and the shape of the seam is visible.

   **Decision:** **no.** The classification rules, the MMWR week and year
   calculation and the `YYYYNNNNN` record-number format are pure functions, and
   `Survsys.Data` carrying EF Core does not stop a static rule class in it from
   being unit-tested without a database — `Survsys.UnitTests` already covers
   exactly that. With `Survsys.Messaging` now taking the one large pure body of
   logic, a third seam would be structure without content, which is what the
   brief forbids twice. This supersedes the sentence in *Why these cuts* that
   left the seam to be made when the content existed: the content arrived, and
   the answer is that `Survsys.Data` keeps it.
4. **Which `Dabbler.*` packages are adopted.** The operator offered them as
   helpful-if-helpful, not as a requirement. Three would plainly remove code
   from the early phases:

   | Package | What it gives | Where it would land |
   |---|---|---|
   | `Dabbler.Components` | MudBlazor CRUD primitives — a query-by-example filter panel, edit-session tracking, a master/detail shell | phases 1–4; case lists and the CMR tab shell are exactly a master/detail with QBE filtering |
   | `Dabbler.Api.Querying` | grid-query DTOs and `IQueryable` sorting, filtering and paging | phase 1 onward; every list endpoint needs paging, and this is the paging |
   | `Dabbler.Testing.Blazor` | readiness probes and Playwright render-wait synchronisation | phase 0; **directly relevant to the walkthrough contract**, because render-wait synchronisation is what stops walkthrough tests being flaky |

   `Dabbler.Security` and `Dabbler.Security.Blazor` (restriction-based
   authorisation with field masking) are left to the phase that does
   authorisation; the `Dabbler.Reports.*` family is left to the reporting phase.

   **The risk to weigh:** every one of these is at `0.1.0-preview.1`. Adopting a
   prerelease package into the walkthrough harness means a breaking change
   upstream can break the harness that guarantees the releases.

   **Decision:** **none adopted now**, each for its own reason.

   - `Dabbler.Testing.Blazor` — **refused, not deferred.** The walkthrough
     harness is the operator's non-negotiable: a walkthrough that cannot be
     completed as written is a failed release. Putting a `0.1.0-preview.1`
     dependency inside the gate that guarantees that means an upstream breaking
     change breaks the guarantee itself. What it offers is replaced by two
     things this repository owns: a readiness endpoint on each tier that the
     E2E suite waits on before its first click, and Playwright's own web-first
     assertions, which retry until an element is actionable and are the
     supported answer to render-wait flakiness.
   - `Dabbler.Api.Querying` — **not now.** Paging is one paged-result record in
     `Survsys.Contracts` and a `Skip`/`Take` on the query: a dozen lines,
     against a preview dependency in the tier every screen reads through.
   - `Dabbler.Components` — **not now.** Phase 1's screens are what will say
     what a master/detail shell and a query-by-example panel actually have to
     do here; adopting the primitives before writing one screen shapes the
     screens around the package rather than the other way round. Revisit at
     phase 4, where the admin screens are most alike and the saving would be
     real.
   - `Dabbler.Security`, `Dabbler.Security.Blazor` and the `Dabbler.Reports.*`
     family — unchanged, left to the phases that do authorisation and
     reporting.

   **Standing rule.** A `Dabbler.*` package is adopted only by a session that
   names, in its own plan, the code the package removes — and never into the
   walkthrough harness while it is a prerelease. The operator offered these as
   helpful-if-helpful; that is the test of helpful.

## Walkthrough contract

The operator's non-negotiable, stated as a rule this repository keeps. Their
words: it is *extremely important*, and **unacceptable to have a walkthrough
that the user cannot complete exactly as it is written**.

1. **A walkthrough is a markdown document under `docs/walkthroughs/`,**
   organised around **one user story** that demonstrates a capability. The user
   stories are **inferred from the legacy code and documentation** under
   `../nedss/`, not invented here.
2. **Each walkthrough has exactly one Playwright test named after it** — one
   document, one test, a name that matches — which **performs its written steps
   in their written order** and asserts the outcome the document promises.
3. **A step the test cannot perform is a defect in the application or in the
   document.** It is *never* a step the test skips, works around, or performs by
   a route the document does not describe. If the test needs a selector the
   walkthrough's wording does not point at, the walkthrough is ambiguous and the
   document is the thing to fix.
4. **Every end-to-end run records video** showing clicks and text entry, through
   **Playwright's own video recording**, which is per-test, deterministic and
   survives CI. The host's **OBS Studio is held in reserve** for narrated
   recordings, should Playwright's output prove insufficient for the
   documentation the operator wants.
5. **No release ships a walkthrough whose test does not pass.** The walkthrough
   suite is a release gate, not a report.

The point of the contract is that the operator is the one who performs the
walkthroughs, having been promised they will work. The tests are how that
promise is kept without the operator being the one who discovers it was broken.

## Phases

Ordered so the earliest phases put a working MudBlazor screen over real SQL
Server data in front of the operator, as the brief requires. Each phase is
scoped to a handful of focused AI sessions and ends on a release; **each release
is a session of its own, which the framework publishes** — no other session
publishes. Feature content is drawn from
`../nedss/docs/functional/phase-1.6-feature-matrix.md`, which inventories 92
features as 85 port, 4 simplify, 3 drop. **The proposed versions below are for
the operator to approve together with this plan.**

### Phase 0 — Skeleton and walkthrough harness → **0.1.0**

The harness before the features, because the walkthrough contract has to exist
before there is a walkthrough to hold to it.

- Solution file; all seven projects with their references; `global.json` pinning **.NET 10** (this host also carries SDK `11.0.100-preview.2`, which is newer and would otherwise be selected).
- `testing.suites` declared in `dabbler.yaml`, with `expensive: true` on the run of record and a hang limit in the command (`dotnet test --blame-hang-timeout 5m`).
- **The architecture test**: `Survsys.Web` references neither `Survsys.Data` nor `Microsoft.EntityFrameworkCore`.
- Playwright configured with video recording on; the E2E project able to start Web and Api and seed a database.
- **One walkthrough, end to end, over one real table** — the thinnest vertical slice that proves the whole chain: migration → API endpoint → MudBlazor screen → walkthrough document → passing Playwright test → recorded video.
- A packaging session writes the `packaging:` block.

*Specified by:* `phase-1.6-cross-cutting-patterns.md`, `epitrax-table-catalog.md`.

### Phase 1 — Persons and case records → **0.2.0**

The first phase a user can do real work in.

- Person demographics: name, DOB, gender, ethnicity, language, country of birth; addresses with begin/end dates and county; phones and emails; multi-select race; aliases; parent/guardian.
- Person search, and CMR creation by the legacy two-step flow (person search → disease and agency selection).
- Record number generation, format `YYYYNNNNN`.
- The CMR shell with its Demographics tab.
- *Walkthroughs:* find a person, create a case morbidity report, record demographics.

*Specified by:* `epitrax-core-workflows.md`, `epitrax-cmr-workflow.md`, `phase-1.6-cross-system-data-model.md`, `epitrax-table-catalog.md`, `db-snapshots/session-010/`.

### Phase 2 — Clinical, laboratory and workflow → **0.3.0**

- Clinical tab; manual laboratory entry (specimen → test → result); antimicrobial susceptibility.
- The workflow state machine: `assigned_to_lhd → accepted → under_investigation → closed`.
- Case status (Confirmed, Probable, Suspected, Not a Case, Unknown); MMWR week and year auto-calculated from event onset date.
- Work queues and multi-agency read-only sharing.
- *Walkthroughs:* enter a laboratory result, assign and accept a case, close an investigation.

*Specified by:* `epitrax-cmr-deep-editing.md`, `CLASSIFICATION_RULES.md`, `db-snapshots/session-010/`, `db-snapshots/session-011/`.

### Phase 3 — Investigation, administration, contacts, notes, tasks → **0.4.0**

- The remaining CMR tabs: Investigation, Administrative, Contacts, Notes.
- Contacts and exposures: exposure events with place and time range, exposure contacts and sources, contact disposition, batch CSV contact import.
- Tasks, including recurring tasks; clinical and administrative notes with strikethrough as soft delete.
- File attachments with categories.
- *Walkthroughs:* add a contact to a case, log an exposure event, attach a document, work a task list.

*Specified by:* `session-012-investigation-admin-contacts-notes.md`, `db-snapshots/session-012/`.

### Phase 4 — Reference data, administration, reporting → **0.5.0**

- Agencies and jurisdictions with zip-to-agency mapping; facilities; conditions; core element configuration (field visibility per condition and agency).
- Users, roles and privileges; access audit.
- Saved views, custom data exports with user-defined columns and filters.
- *Walkthroughs:* configure an agency, add a facility, save a view, export a dataset.

*Specified by:* `CT_SITE_REQUIREMENTS.md`, `phase-1.6-cross-cutting-patterns.md`, `db-snapshots/session-011/05-after-admin-explore.txt`.

### Phase 5 — Inbound ELR processing → **0.6.0**

The EMSA half. This is where the first genuinely pure logic appears, and where
deferred cuts 1 and 2 come due.

- HL7 v2 `ORU^R01` intake and preprocessing with **nHapi** — Mirth is absorbed, not kept.
- Vocabulary matching: LOINC → condition, SNOMED → organism, master/child LOINC with lab-specific overrides.
- Graylist time-windowed duplicate detection; person matching, fuzzy and exact.
- The message queue with its review states, QA flags and exception handling; semi-automated CMR creation requiring human review; batch notifications.
- *Walkthroughs:* review a staged message, resolve a duplicate, promote a message to a case.

*Specified by:* `EMSA_MESSAGE_PIPELINE.md`, `EMSA_VOCABULARY_SYSTEM.md`, `EMSA_RULES_ENGINE.md`, `EMSA_PERSON_MATCHING.md`, `EMSA_EPITRAX_CONTRACT.md`, `HL7_FIELD_MAPPINGS.md`, `MIRTH_REPLACEMENT_SPEC.md`, `emsa-ui-overview.md`, `emsa-pipeline-testing.md`, `db-snapshots/session-013/`, `db-snapshots/session-015/`.

### Phase 6 — Outbound CDC notification → **0.7.0**

The NMI half.

- Notification guides per condition; PHIN variable mapping across 588 CDC variables; the seven rule set types; repeating groups for labs, vaccines, treatments and travel.
- `ORU^R01` generation with nHapi; **direct transmission in place of the Mirth export queue**.
- Auto-queue on case change as a domain event rather than a database trigger; reconciliation cutoff by MMWR week and year; the CDC export log.
- **NETSS** fixed-width export: confirm with CDC whether it is still required before dropping it — the brief leaves this open deliberately.
- *Walkthroughs:* queue a case for notification, review a generated message, inspect the export log.

*Specified by:* `NMI_EXPORT_PIPELINE.md`, `NMI_RULESETS.md`, `NMI_XML_GENERATION.md`, `nmi-ui-overview.md`, `db-snapshots/session-015/nmi-*`.

### Phase 7 — Outbreaks, forms, treatments, audit → **0.8.0**

Planned as the remainder and the first release that claims parity, at 1.0.0.
The gap analysis after it ([`gap-analysis.md`](gap-analysis.md)) found that it
cannot claim parity, and the operator moved 1.0.0 to the end of phase 14.

- Outbreak management with multi-agency access, section-based outbreak reports, activity log.
- The dynamic form builder: condition-specific supplemental forms, nested sections and questions, text and coded answers, auto-assignment on condition selection.
- Treatments and condition-specific treatment lists; vaccine records.
- Audit as EF Core interceptors in place of PostgreSQL triggers.
- IBIS integration; ad-hoc reporting, privilege-gated.
- *Walkthroughs:* open an outbreak and link cases, complete a supplemental form, record a treatment.

*Specified by:* `epitrax-core-workflows.md`, `phase-1.6-feature-matrix.md` §5, §7, §8, §14, `epitrax-table-catalog.md`.

### Phases 8 to 14 — closing the gaps → **0.9.0 to 1.0.0**

Added on 2026-10-18 from [`gap-analysis.md`](gap-analysis.md). The operator
chose to close the gaps before claiming parity, and to add electronic case
reporting, the Connecticut ELR rules, full NMI and a master person record.

| Phase | What it closes | Release |
|---|---|---|
| 8 — Case access and editing | case search, editing and deleting child records, the full workflow, event types and contact cases, per-case treatments, encounters, occupation and pregnancy, reference code lists, delete, print and lock | 0.9.0 |
| 9 — Master person record | identifiers, name and address history, person matching in the application, duplicate merge | 0.10.0 |
| 10 — Inbound ELR to parity | parsed and stored messages, MLLP and ACKs, vocabulary and rules at intake, the graylist and full queue, labs from messages and case update, retry and statistics, Connecticut conformance | 0.11.0 |
| 11 — Outbound CDC to parity | the queue worker, auto-queue on every change, queue actions and reconciliation, guide and value-set administration, transport | 0.12.0 |
| 12 — Audit, access, classification, reporting | audit with user and values and its screens, jurisdiction-scoped access, lockout, persisted keys, classification on save, IBIS, ad-hoc reporting, system configuration | 0.13.0 |
| 13 — Electronic case reporting | eICR and RR intake, validation and viewing, reportability and condition mapping, cases from eCR, senders and data quality; specified by [`ecr-requirements.md`](ecr-requirements.md) | 0.14.0 |
| 14 — Parity | the parity check, the full walkthrough regression and the guides | **1.0.0** |

Deferred cut 2 holds for MLLP and eCR intake: both are hosted services in the
API tier. The parser seam phase 10 adds is the one new abstraction, and it is
justified by a second format arriving in phase 13, not speculatively.
