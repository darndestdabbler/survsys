# Session plan

> **Purpose:** the numbered sessions this repository runs, in order. The
> first two set the project up; everything after them is the work.
> **Workflow:** Full

---

## Sessions

### Session 1: Author or import the solution plan

1. Register.
2. The brief is the prompt this session was started with, or
   `docs/planning/brief.md`: what the solution is — its purpose, who uses
   it, what it must do and what is out of scope, what success looks like,
   and whether a plan or notes already exist. With neither, ask the person
   who started the session for it and write their answer to that path
   before any work. The plan's substance is theirs: do not search
   neighbouring directories for one, and do not draft one from the folder
   name. Before any module is proposed, ask them how production is split:
   what runs separately in production, and which part may talk to the
   database? Offer the default -- an application tier that calls an API
   tier, and only the API tier talks to the database. Then create — or
   import — `docs/planning/solution-plan.md`: the
   objective a reader can act on; the *Production split* they answered;
   the *Handoff artifacts*, what each separately running part is handed
   over as, with the standard command for its form -- for .NET an IIS site
   package (`dotnet publish`) or a Windows Service (a Worker Service
   published for a service host), for Java a runnable jar or a war (`mvn
   package` writing into `{output}`, which `packaging.pack` requires --
   say through a property the POM's jar or war plugin takes as its
   output directory, `-Dhandoff.dir={output}`) or an image (Spring
   Boot's `build-image`), for the database a
   SQL migration script (`dotnet ef migrations script --idempotent`); the
   real form is produced later, and a tutorial produces the easiest one to
   test; the modules, with what each is
   responsible for, the contract each exposes (what must be true going
   in, what is guaranteed coming out, how it fails), the dependency
   direction, the reason for each cut and the cuts deferred — one module
   is a fine answer, and most small solutions are one; and the phases or
   feature areas with their key deliverables, each scoped to a handful of
   focused AI sessions. A repository that already has
   `docs/planning/project-plan.md` amends that file instead.
3. Where the plan has several modules, say in it how each is a project:
   a `.csproj` listed in the root solution file for .NET, or a Maven module
   listed in the parent `pom.xml`, each reaching a sibling by project
   reference -- a `<ProjectReference>`, or a dependency at
   `${project.version}`. The first numbered session, the skeleton, writes
   the projects.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

This session writes the plan and nothing else: it creates, edits or
deletes no code, project, build file, test, `dabbler.yaml` suite or
packaging.

**Creates:** `docs/planning/solution-plan.md`. A later revision is just
another plan session that amends the same file.

### Session 2: Challenge the plan, then break it into numbered sessions

1. Register.
2. Read `docs/planning/solution-plan.md` (or `docs/planning/project-plan.md`
   where that is the file this repository keeps its plan in) and challenge
   its cuts: for each
   module, whether it should exist, whether its contract is a promise
   something can prove, and whether the dependency direction hides the
   decisions most likely to change; decide each deferred cut or say why it
   stays deferred. Record the answers in the plan's rationale.
3. Break the plan into numbered sessions appended to this file. Each
   session is a focused unit of
   work one AI coding session can complete: one
   `### Session <N>: <title>` heading, and its steps as a top-level
   ordered list. Step 1 registers the session; the last steps are
   cross-provider verification, the complete suite once against the
   verified tree, and close-out; the middle steps are the work. Never
   write a step that says "run the tests" without saying which run it
   means. Order sessions so earlier ones unblock later ones — a module
   before the modules that depend on it — and keep at most ~3 work steps
   per session.

   The first numbered session is the skeleton. For .NET it writes the
   solution file and every project the plan names, with their
   references; for Maven or Gradle, the parent build and every module.
   Either way it declares the test suite in `dabbler.yaml` under
   `testing.suites`, even before any tests exist, with `expensive: true`
   where its run is the run of record and a hang limit in its command --
   `dotnet test --blame-hang-timeout 5m`, or Surefire's
   `forkedProcessTimeoutInSeconds` -- so every later run inherits it,
   and adds the architecture test the plan requires -- where only the
   API tier may reach the database, that rule. Later sessions fill the
   modules in and never re-create what the skeleton made.

   Packaging and releases are sessions of their own. Where the plan
   names *Handoff artifacts*, plan one packaging session -- early, with
   project files only, or later -- that writes the `packaging:` block in
   `dabbler.yaml` and does nothing else. A release is a session with no
   steps, headed `### Session <N>: Release <version> (release:
   <version>)`; propose its version -- patch, minor or major by what the
   sessions it releases change -- for the person to approve with the plan.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

This session writes the plan and nothing else: it creates, edits or
deletes no code, project, build file, test, `dabbler.yaml` suite or
packaging.

**Creates:** the numbered session list the rest of this repository runs,
starting with the skeleton session.

> Do NOT hand-author `sessions.json`. The first `session start`
> bootstraps it from this plan — state files are the writers' job.

---

## The work

Sessions 3 onward, broken out of `docs/planning/solution-plan.md` by session 2.
Each is one focused session: it registers, does at most three work steps, then
verifies cross-provider, runs the complete suite against the verified tree, and
closes out. They are ordered so a module lands before the modules that depend
on it, and so the earliest put a MudBlazor screen over real SQL Server data in
front of the operator. A release is a session with no steps; the versions below
are the ones this plan proposes for the operator to approve with it.

*Phase 0 — skeleton and walkthrough harness → release 0.1.0. The harness comes
before the features, because the walkthrough contract has to exist before there
is a walkthrough to hold to it.*

### Session 3: Skeleton — the solution, its seven projects and the architecture tests

1. Register.
2. Write `global.json` at the repository root pinning the .NET 10 SDK — this
   host also carries SDK `11.0.100-preview.2`, which is newer and would
   otherwise be selected — and `Survsys.sln` at the root listing all seven
   projects, each a `.csproj`, with their `<ProjectReference>`s and nothing
   else: `Survsys.Contracts` referencing no sibling; `Survsys.Data` referencing
   no sibling and carrying `Microsoft.EntityFrameworkCore.SqlServer`;
   `Survsys.Api` referencing `Survsys.Data` and `Survsys.Contracts`;
   `Survsys.Web` referencing `Survsys.Contracts` alone;
   `Survsys.UnitTests` referencing every module, because it is the project that
   reads their reference graphs; `Survsys.IntegrationTests` referencing
   `Survsys.Api` and `Survsys.Data`; and `Survsys.E2ETests` referencing
   `Survsys.Contracts` only, since it launches the two tiers as processes. No
   feature code in any of them.
3. Declare `testing.suites` in `dabbler.yaml` — the unit, integration and
   end-to-end suites, each with its `covers` and `test_roots`, `expensive: true`
   on the suite that is the run of record, and a hang limit in every command
   (`dotnet test --blame-hang-timeout 5m`) so every later run inherits it.
4. Write the two architecture tests the plan requires, in
   `Survsys.UnitTests/ArchitectureTests.cs`: that the `Survsys.Web` assembly
   references neither `Survsys.Data` nor `Microsoft.EntityFrameworkCore`, and
   that no endpoint signature in `Survsys.Api` — parameter or return type,
   unwrapped through `Task<>`, `ActionResult<>` and collections — names a type
   from the `Survsys.Data` assembly.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 4: Packaging — write the packaging block and nothing else

1. Register.
2. Write the `packaging:` block in `dabbler.yaml` and change no other file:
   `pack` as argv that produces the handoff artifacts the plan names — an IIS
   site package for each tier by `dotnet publish` and the database's idempotent
   script by `dotnet ef migrations script --idempotent` — written into
   `{output}`; and `push` as argv naming the feed and naming its credential by
   environment variable rather than holding it. Rehearse the block with
   `dabbler packaging --dry-run`, which exits 0 when it loads.
3. Cross-provider verification.
4. Full test suite, recorded as the run of record.
5. Close-out.

### Session 5: First vertical slice — one real table through the API

1. Register.
2. `Survsys.Data`: the `conditions` entity, the `DbContext`, its entity
   configuration and the initial EF Core migration — the thinnest real table in
   `../nedss/docs/data-model/epitrax-table-catalog.md` that a screen can show.
   `Survsys.Api`: the startup assertion the challenge added, reading the
   pending-migration list and refusing to serve while it is non-empty.
3. `Survsys.Contracts`: the paged-result record and the condition response
   shape. `Survsys.Api`: `GET /api/conditions`, paged, returning
   `Survsys.Contracts` types, with Problem Details on every failure and the
   entity-to-wire mapping living here.
4. `Survsys.IntegrationTests`: the suite against a real SQL Server — failing
   loudly when none is reachable rather than skipping — applying the migration
   and loading rows from `../nedss/docs/db-snapshots/` as fixtures.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 6: First MudBlazor screen and the Playwright harness

1. Register.
2. `Survsys.Web`: the conditions list screen over `GET /api/conditions`, with
   stable element identifiers that a walkthrough's wording points at, and an
   API failure shown as a message on the screen rather than an unhandled
   circuit exception. A readiness endpoint on each tier for the harness to wait
   on.
3. `Survsys.E2ETests`: the Playwright configuration with video recording on for
   every test, launching `Survsys.Web` and `Survsys.Api` as processes under a
   startup timeout, waiting on their readiness endpoints, and seeding the
   database — the harness the walkthrough contract rests on, with no prerelease
   package in it.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 7: The first walkthrough and the documentation it lives in

1. Register.
2. `docs/walkthroughs/`: the first walkthrough document — one user story
   inferred from the legacy condition screens under `../nedss/docs/`, written
   as numbered steps the reader performs, naming each control by what the
   screen shows.
3. `Survsys.E2ETests`: the one Playwright test named after that walkthrough,
   performing its written steps in their written order, asserting the outcome
   the document promises, and recording video of the clicks and text entry.
4. `docs/`: the walkthrough index, and the user and technical documentation
   skeleton the later phases add to.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 8: Release 0.1.0 (release: 0.1.0)

*Cancelled, and left here as the record of why. The packaging run stopped with
`EPERM` on its own `{output}` directory: the job ran with `TEMP` pointed at
that directory, and .NET's persistent build servers — Roslyn's `VBCSCompiler`,
MSBuild node reuse and NuGet's scratch lock — wrote into it and held it open
after `dotnet publish` returned. Nothing was pushed, so 0.1.0 was never spent.
Session 9 repairs the pack block; session 10 is the release.*

### Session 9: Packaging — repair the pack block and nothing else

1. Register.
2. Repair the `packaging:` block in `dabbler.yaml` and change no other file.
   Session 8's packaging run stopped with `EPERM` on `{output}`: the job runs
   with `TEMP` pointed at that same directory, so .NET's persistent build
   servers — Roslyn's `VBCSCompiler`, MSBuild node reuse and NuGet's scratch
   lock — write into it and keep it open after `dotnet publish` returns, and
   the framework cannot finish with a directory another process holds. The
   same files would also be collected as handoff artifacts, because the
   framework pushes every file it finds under `{output}`. Make `pack` immune
   to both: stage each tier's publish outside `{output}`, so only the three
   named artifacts ever land there; stop a compiler server and MSBuild node
   reuse being started at all (`-p:UseSharedCompilation=false`,
   `-nodeReuse:false`, or the environment variables that mean the same); and
   end the script with `dotnet build-server shutdown`, so nothing the pack did
   start outlives it. The three artifacts, the feed and the credential by name
   stay exactly as session 4 declared them.
3. Prove it under the condition that broke session 8, not only under a dry run:
   pack into a scratch directory with `TEMP` pointed at that same directory,
   and assert that exactly the three artifacts are there when it exits and that
   the directory can then be removed. `dabbler packaging --dry-run` exits 0
   when the block loads, which is the weaker half of the check.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 10: Release 0.1.0 (release: 0.1.0)

*Cancelled, and left here as the record of why. Session 9's repair held: the
pack produced exactly the three artifacts, with no `EPERM` and no staging left
behind. The push is what failed — `curl` exit 6, could not resolve host, on
`https://artifacts.example.org/survsys-handoff/`, the placeholder feed session
4 declared and no later session replaced. `DABBLER_FEED_PAT` was set, so this
was never a credential: the declared feed simply does not exist. Nothing was
pushed, so 0.1.0 was never spent. Session 11 points the feed at the folder the
handoff will actually use; session 12 is the release.*

### Session 11: Packaging — point the feed at the handoff folder

1. Register.
2. Repair the `packaging:` block in `dabbler.yaml` and change no other file.
   The feed becomes `D:/handoff/survsys`, a folder on disk: the operator wants
   the handoff artifacts dropped in a directory, not continuous deployment to
   a remote feed, and the `artifacts.example.org` URL session 4 declared was
   always a placeholder. A feed that is a folder authenticates nothing, so
   `secret` and the `--user` argument go together — `DABBLER_FEED_PAT` stops
   being named anywhere — and `push` becomes a copy of `{artifact}` into
   `{feed}` rather than an HTTPS upload, creating the folder when it is not
   there yet. `pack` is session 9's and is not touched: the three artifacts
   keep their names and their contents.
3. Prove the push, not only that the declaration loads: run the declared push
   argv for each of three stand-in artifacts into a scratch folder feed, and
   assert all three arrive with their bytes intact and that a second run over
   the same folder overwrites rather than failing. `dabbler packaging
   --dry-run` exits 0 when the block loads, which is the weaker half of the
   check.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 12: Release 0.1.0 (release: 0.1.0)

---

*Phase 1 — persons and case records → release 0.2.0. The first phase a user can
do real work in. Specified by `epitrax-core-workflows.md`,
`epitrax-cmr-workflow.md`, `phase-1.6-cross-system-data-model.md` and
`db-snapshots/session-010/`.*

### Session 13: Person demographics — schema and endpoints

1. Register.
2. `Survsys.Data`: the person entities and their migration — name, DOB, gender,
   ethnicity, language and country of birth; addresses with begin and end dates
   and county; phones and emails; multi-select race; aliases; parent or
   guardian.
3. `Survsys.Contracts` and `Survsys.Api`: the person wire shapes and the
   `/api/persons` endpoints, paged, with Problem Details and the mapping in the
   API tier.
4. `Survsys.IntegrationTests`: a person round-trip against a real SQL Server,
   over `db-snapshots/session-010/` fixtures.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 14: Person search and case morbidity report creation

1. Register.
2. `Survsys.Data` and `Survsys.Api`: person search, and the record number
   generator in the legacy `YYYYNNNNN` format as a pure rule class with its
   tests in `Survsys.UnitTests/RecordNumberTests.cs`.
3. `Survsys.Data` and `Survsys.Api`: the case morbidity report entity and its
   migration, and the legacy two-step creation flow — person search, then
   disease and agency selection.
4. `Survsys.Web`: the person search screen and the two creation screens, with
   stable element identifiers.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 15: The case morbidity report shell and its Demographics tab

1. Register.
2. `Survsys.Web`: the report shell with its tab strip, carrying the
   Demographics tab alone — later tabs are added by the sessions that own them.
3. `Survsys.Web`: Demographics editing against the person endpoints —
   addresses, phones, emails, race, aliases and guardian.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 16: Phase 1 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents, one user story each — find a person,
   create a case morbidity report, record demographics.
3. `Survsys.E2ETests`: the three Playwright tests named after them, each
   performing its document's written steps in their written order and recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 17: Release 0.2.0 (release: 0.2.0)

---

*Phase 2 — clinical, laboratory and workflow → release 0.3.0. Specified by
`epitrax-cmr-deep-editing.md`, `CLASSIFICATION_RULES.md` and
`db-snapshots/session-010/` and `session-011/`.*

### Session 18: Clinical and laboratory data — schema and endpoints

1. Register.
2. `Survsys.Data`: the clinical entities, and the laboratory chain of specimen
   to test to result with antimicrobial susceptibility, and their migration.
3. `Survsys.Contracts` and `Survsys.Api`: the clinical and laboratory wire
   shapes and their endpoints, paged, with Problem Details.
4. `Survsys.IntegrationTests`: a laboratory result round-trip over
   `db-snapshots/session-011/` fixtures.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 19: Clinical and laboratory screens

1. Register.
2. `Survsys.Web`: the Clinical tab on the report shell.
3. `Survsys.Web`: manual laboratory entry — specimen, then test, then result —
   and antimicrobial susceptibility.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 20: Case classification, status and MMWR week

1. Register.
2. `Survsys.Data`: the case status rules — Confirmed, Probable, Suspected, Not
   a Case, Unknown — and the MMWR week and year calculated from the event onset
   date, both as pure rule classes taking no `DbContext`, from
   `CLASSIFICATION_RULES.md`.
3. `Survsys.UnitTests`: `CaseStatusRulesTests.cs` and `MmwrWeekTests.cs`, named
   after the rule classes they test, covering the legacy boundary cases
   including the week that straddles a year end.
4. `Survsys.Api` and `Survsys.Web`: status and MMWR fields on the case
   endpoints and on the screen.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 21: Workflow state machine, work queues and agency sharing

1. Register.
2. `Survsys.Data` and `Survsys.UnitTests`: the workflow state machine —
   `assigned_to_lhd` to `accepted` to `under_investigation` to `closed` — as a
   pure transition table with the tests named after it, including every refused
   transition.
3. `Survsys.Api`: assignment, acceptance and closure endpoints, and multi-agency
   read-only sharing.
4. `Survsys.Web`: the work queue screens, by agency and by assignee.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 22: Phase 2 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents — enter a laboratory result, assign and
   accept a case, close an investigation.
3. `Survsys.E2ETests`: the three Playwright tests named after them, recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 23: Release 0.3.0 (release: 0.3.0)

---

*Phase 3 — investigation, administration, contacts, notes and tasks → release
0.4.0. Specified by `session-012-investigation-admin-contacts-notes.md` and
`db-snapshots/session-012/`.*

### Session 24: The Investigation and Administrative tabs

1. Register.
2. `Survsys.Data` and `Survsys.Api`: the investigation and administrative
   fields, their migration and their endpoints.
3. `Survsys.Web`: the Investigation and Administrative tabs on the report
   shell.
4. `Survsys.IntegrationTests`: a round-trip of both over
   `db-snapshots/session-012/` fixtures.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 25: Contacts and exposures

1. Register.
2. `Survsys.Data` and `Survsys.Api`: exposure events with place and time range,
   exposure contacts and sources, and contact disposition, with their migration
   and endpoints.
3. `Survsys.Web`: the Contacts tab, with exposure events and dispositions.
4. `Survsys.Api` and `Survsys.UnitTests`: batch CSV contact import, with the
   parser's tests named after it covering a malformed row and a duplicate.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 26: Notes and tasks

1. Register.
2. `Survsys.Data` and `Survsys.Api`: clinical and administrative notes with
   strikethrough as soft delete, and tasks including recurring tasks, with
   their migration and endpoints.
3. `Survsys.Web`: the Notes tab and the task list screen.
4. `Survsys.UnitTests`: the recurring-task schedule rules, named after the rule
   class.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 27: File attachments

1. Register.
2. `Survsys.Data` and `Survsys.Api`: attachments with their categories, the
   migration, and upload and download endpoints with a size limit and a
   content-type check.
3. `Survsys.Web`: attachment upload and listing on the case.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 28: Phase 3 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — add a contact to a case, log an
   exposure event, attach a document, work a task list.
3. `Survsys.E2ETests`: the four Playwright tests named after them, recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 29: Release 0.4.0 (release: 0.4.0)

---

*Phase 4 — reference data, administration and reporting → release 0.5.0.
Specified by `CT_SITE_REQUIREMENTS.md`, `phase-1.6-cross-cutting-patterns.md`
and `db-snapshots/session-011/05-after-admin-explore.txt`.*

### Session 30: Agencies, jurisdictions, facilities and conditions

1. Register.
2. `Survsys.Data` and `Survsys.Api`: agencies and jurisdictions with
   zip-to-agency mapping, facilities, and the conditions table filled out, with
   their migration and endpoints.
3. `Survsys.Web`: the agency, facility and condition administration screens.
4. `Survsys.UnitTests`: the zip-to-agency resolution rules, named after the
   rule class, including an unmapped zip.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 31: Core element configuration

1. Register.
2. `Survsys.Data` and `Survsys.Api`: core element configuration — field
   visibility per condition and per agency — with its migration and endpoints.
3. `Survsys.Web`: the configuration screen, and the report tabs honouring what
   it says.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 32: Users, roles, privileges and access audit

1. Register.
2. `Survsys.Data` and `Survsys.Api`: users, roles and privileges with their
   migration; authorisation on every endpoint; and the access audit record.
3. `Survsys.Web`: sign-in, the user and role administration screens, and
   privilege-gated navigation.
4. `Survsys.UnitTests` and `Survsys.IntegrationTests`: the privilege rules
   named after their rule class, and an integration test that an unprivileged
   caller is refused rather than served.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 33: Saved views and custom exports

1. Register.
2. `Survsys.Data` and `Survsys.Api`: saved views, and custom exports with
   user-defined columns and filters, with their migration and endpoints.
3. `Survsys.Web`: the saved-view and export screens.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 34: Phase 4 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — configure an agency, add a facility,
   save a view, export a dataset.
3. `Survsys.E2ETests`: the four Playwright tests named after them, recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 35: Release 0.5.0 (release: 0.5.0)

---

*Phase 5 — inbound ELR processing → release 0.6.0. The EMSA half, and where the
deferred cuts session 2 decided come due. Specified by
`EMSA_MESSAGE_PIPELINE.md`, `EMSA_VOCABULARY_SYSTEM.md`,
`EMSA_RULES_ENGINE.md`, `EMSA_PERSON_MATCHING.md`, `HL7_FIELD_MAPPINGS.md`,
`MIRTH_REPLACEMENT_SPEC.md` and `db-snapshots/session-013/` and
`session-015/`.*

### Session 36: `Survsys.Messaging` and HL7 intake parsing

1. Register.
2. Create `Survsys.Messaging`, the cut session 2 decided and left to the
   session that first needs it: a new `.csproj` added to `Survsys.sln`,
   referencing nHapi and `Survsys.Contracts` and never EF Core, referenced by
   `Survsys.Api`; and a third architecture test in `Survsys.UnitTests`
   asserting that fence.
3. `Survsys.Messaging`: `ORU^R01` parsing and preprocessing with nHapi, Mirth
   absorbed rather than kept.
4. `Survsys.UnitTests`: the parser's tests named after it, over legacy message
   fixtures from `../nedss/docs/`, including a message that fails to parse.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 37: Vocabulary matching

1. Register.
2. `Survsys.Data`: the vocabulary tables and their migration — LOINC to
   condition, SNOMED to organism, master and child LOINC with lab-specific
   overrides.
3. `Survsys.Messaging`: the matcher over them, taking the vocabulary as data
   rather than reaching for a `DbContext`.
4. `Survsys.UnitTests`: the matcher's tests named after it, including
   lab-specific override precedence and an unmatched code.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 38: Duplicate detection and person matching

1. Register.
2. `Survsys.Data` and `Survsys.Messaging`: graylist time-windowed duplicate
   detection, with its migration and its rule.
3. `Survsys.Messaging`: person matching, fuzzy and exact.
4. `Survsys.UnitTests`: the tests for both, named after their rule classes,
   including a match just inside and just outside the graylist window.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 39: The message queue and the intake service

1. Register.
2. `Survsys.Data` and `Survsys.Api`: the message queue with its review states,
   QA flags and exception handling, with its migration and endpoints.
3. `Survsys.Api`: inbound ELR intake as an `IHostedService` inside the API tier
   — the cut session 2 decided — with its own timeout and no Mirth dependency.
4. `Survsys.IntegrationTests`: a message carried from file to
   queued-for-review, over `db-snapshots/session-013/` fixtures.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 40: ELR review screens and promotion to a case

1. Register.
2. `Survsys.Web`: the staged-message queue and the message review screen, with
   QA flags and exceptions visible.
3. `Survsys.Api` and `Survsys.Web`: semi-automated case creation that requires
   human review, and batch notifications.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 41: Phase 5 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents — review a staged message, resolve a
   duplicate, promote a message to a case.
3. `Survsys.E2ETests`: the three Playwright tests named after them, recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 42: Release 0.6.0 (release: 0.6.0)

---

*Phase 6 — outbound CDC notification → release 0.7.0. The NMI half. Specified
by `NMI_EXPORT_PIPELINE.md`, `NMI_RULESETS.md`, `NMI_XML_GENERATION.md` and
`db-snapshots/session-015/nmi-*`.*

### Session 43: Notification guides and PHIN variable mapping

1. Register.
2. `Survsys.Data`: notification guides per condition and the PHIN variable
   table across the 588 CDC variables, with their migration.
3. `Survsys.Messaging`: the PHIN variable mapping over that table.
4. `Survsys.UnitTests`: the mapping's tests named after it, including a
   condition with no guide.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 44: The seven rule set types and repeating groups

1. Register.
2. `Survsys.Data` and `Survsys.Messaging`: the seven rule set types, with their
   migration and their evaluation.
3. `Survsys.Messaging`: repeating groups for laboratories, vaccines, treatments
   and travel.
4. `Survsys.UnitTests`: one test class per rule set type, each named after the
   rule it covers.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 45: Message generation and direct transmission

1. Register.
2. `Survsys.Messaging`: `ORU^R01` generation with nHapi.
3. `Survsys.Api`: direct transmission in place of the legacy Mirth export
   queue, under a timeout, with the outcome recorded.
4. `Survsys.UnitTests` and `Survsys.IntegrationTests`: generation tests against
   the legacy generated messages, and a transmission test against a local stub
   endpoint rather than CDC.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 46: Auto-queue, reconciliation and the export log

1. Register.
2. `Survsys.Api` and `Survsys.Data`: auto-queue on case change as a domain
   event rather than a database trigger; the reconciliation cutoff by MMWR week
   and year; and the CDC export log with its migration.
3. `docs/planning/`: put the NETSS question to the operator in writing — whether
   CDC still requires the legacy fixed-width export. The brief leaves it open
   deliberately, this is the phase where it has to be settled, and the recorded
   default until they answer is that it is not built.
4. `Survsys.UnitTests`: the reconciliation cutoff rules, named after the rule
   class, including a case on the cutoff week.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 47: Notification screens

1. Register.
2. `Survsys.Web`: the notification queue, the generated-message review screen
   and the export log screen.
3. `Survsys.Web`: queueing a case for notification from the case screen.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 48: Phase 6 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents — queue a case for notification,
   review a generated message, inspect the export log.
3. `Survsys.E2ETests`: the three Playwright tests named after them, recording
   video.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 49: Release 0.7.0 (release: 0.7.0)

---

*Phase 7 — outbreaks, forms, treatments and audit → release 1.0.0, the first
release that claims parity. Specified by `epitrax-core-workflows.md`,
`phase-1.6-feature-matrix.md` sections 5, 7, 8 and 14, and
`epitrax-table-catalog.md`.*

### Session 50: Outbreak management

1. Register.
2. `Survsys.Data` and `Survsys.Api`: outbreaks with multi-agency access,
   section-based outbreak reports and the outbreak activity log, with their
   migration and endpoints.
3. `Survsys.Web`: the outbreak screens, and linking cases to an outbreak.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 51: The dynamic form builder

1. Register.
2. `Survsys.Data` and `Survsys.Api`: condition-specific supplemental forms —
   nested sections and questions, text and coded answers — with their migration
   and endpoints.
3. `Survsys.Api`: auto-assignment of a form when a condition is selected.
4. `Survsys.Web`: the form builder screen and the form-completion screen.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 52: Treatments and vaccines

1. Register.
2. `Survsys.Data` and `Survsys.Api`: treatments with condition-specific
   treatment lists, and vaccine records, with their migration and endpoints.
3. `Survsys.Web`: the Treatments and Vaccines screens.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 53: Audit by EF Core interceptors

1. Register.
2. `Survsys.Data`: audit as EF Core `SaveChanges` interceptors, in place of the
   legacy PostgreSQL triggers.
3. `Survsys.IntegrationTests`: an audit row written for each of insert, update
   and delete, and none written when a save is rolled back.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 54: IBIS integration and ad-hoc reporting

1. Register.
2. `Survsys.Api`: the IBIS integration, under a timeout.
3. `Survsys.Api` and `Survsys.Web`: ad-hoc reporting, privilege-gated.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 55: Phase 7 walkthroughs and the full walkthrough regression

1. Register.
2. `docs/walkthroughs/`: three documents — open an outbreak and link cases,
   complete a supplemental form, record a treatment.
3. `Survsys.E2ETests`: the three Playwright tests named after them, and a pass
   over every walkthrough document written since session 7 to confirm each
   still reads exactly as its own test performs it.
4. `docs/`: the user and technical documentation completed for 1.0.0.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 56: Release 1.0.0 (release: 1.0.0)
