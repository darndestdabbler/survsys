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

### Session 49: Repair the dropdown steps of five walkthroughs

*Inserted after session 48. A whole end-to-end run before the 0.7.0 release
fails five walkthroughs written in earlier phases — save a view, export a
dataset, review a staged message, promote a message to a case, resolve a
duplicate — and each fails the same way, alone as well as in the suite. Each
opens a dropdown by the identifier the markup puts on it (`saved-view-condition`,
`staged-messages-filter`), and a MudBlazor select that already holds a value,
even the empty **Any** or **All**, renders the input carrying that identifier as
`type="hidden"` with a visible box beside it, so Playwright waits for a hidden
input to become visible until it times out. Work a task list and attach a
document met the same behaviour and open the box by the label their documents
name it by. The release cannot ship a walkthrough whose test fails, so this
comes before it.*

1. Register.
2. `Survsys.E2ETests`: in `SaveAViewTests`, `ExportADatasetTests`,
   `ReviewAStagedMessageTests`, `PromoteAMessageToACaseTests` and
   `ResolveADuplicateTests`, open each dropdown by its accessible label — the
   label its walkthrough document names it by — as `WorkATaskListTests` and
   `AttachADocumentTests` already do, rather than by its identifier. Every
   other `Choose` helper that opens a select by identifier gets the same
   change, so no walkthrough depends on which value a select opens on. No step
   is skipped or performed by another route, and no document's steps change.
   `Survsys.Web` is not touched. The tests changed are the walkthroughs' own,
   so the step's checks perform each of them in a browser, recording video.
3. Cross-provider verification.
4. Full test suite, recorded as the run of record.
5. Close-out.

### Session 50: Release 0.7.0 (release: 0.7.0)

---

*Phase 7 — outbreaks, forms, treatments and audit → release 0.8.0. Planned as
1.0.0, the first release that claims parity; the gap analysis after session 57
(`docs/planning/gap-analysis.md`) showed it cannot claim it, and the operator
moved 1.0.0 to the end of phase 14. Specified by `epitrax-core-workflows.md`,
`phase-1.6-feature-matrix.md` sections 5, 7, 8 and 14, and
`epitrax-table-catalog.md`.*

### Session 51: Outbreak management

1. Register.
2. `Survsys.Data` and `Survsys.Api`: outbreaks with multi-agency access,
   section-based outbreak reports and the outbreak activity log, with their
   migration and endpoints.
3. `Survsys.Web`: the outbreak screens, and linking cases to an outbreak.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 52: The dynamic form builder

1. Register.
2. `Survsys.Data` and `Survsys.Api`: condition-specific supplemental forms —
   nested sections and questions, text and coded answers — with their migration
   and endpoints.
3. `Survsys.Api`: auto-assignment of a form when a condition is selected.
4. `Survsys.Web`: the form builder screen and the form-completion screen.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 53: Treatments and vaccines

1. Register.
2. `Survsys.Data` and `Survsys.Api`: treatments with condition-specific
   treatment lists, and vaccine records, with their migration and endpoints.
3. `Survsys.Web`: the Treatments and Vaccines screens.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 54: Audit by EF Core interceptors

1. Register.
2. `Survsys.Data`: audit as EF Core `SaveChanges` interceptors, in place of the
   legacy PostgreSQL triggers.
3. `Survsys.IntegrationTests`: an audit row written for each of insert, update
   and delete, and none written when a save is rolled back.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 55: IBIS integration and ad-hoc reporting

1. Register.
2. `Survsys.Api`: the IBIS integration, under a timeout.
3. `Survsys.Api` and `Survsys.Web`: ad-hoc reporting, privilege-gated.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 56: Repair the three endpoint queries SQL Server cannot translate

*Inserted after session 55. The integration suite fails 20 of its 174 tests on
the trunk, every one with a 500 from the API. Three queries throw EF Core's
"The LINQ expression … could not be translated", and each has the same shape:
it projects rows into a positional record (`OutbreakAccessResponse`, the
treatment's condition rows, the case's form assignments), then orders by a
property of that record (`AgencyName`, `Name`, `AssignedAt`). EF cannot map a
constructor argument back to a column, so the `OrderBy` after the projection
has nothing to translate. The failing paths are
`OutbreakQueries.AccessRows` (`OutbreakEndpoints.cs`), reached by every
outbreak detail, create, save and access call; `TreatmentQueries.Detail`
(`TreatmentEndpoints.cs`), reached by every treatment create, save and read;
and `FormQueries.Assignments` (`FormEndpoints.cs`), reached by listing a
case's forms and by the auto-assign on case creation. Sessions 51 to 53 shipped
them. Nothing failed at their close because a session's run of record is the
unit suite, which does not reach SQL Server. The Phase 7 walkthroughs open an
outbreak, complete a form and record a treatment through exactly these paths,
so this comes before them.*

1. Register.
2. `Survsys.Api`: in each of the three queries, order by the entity's or the
   joined row's own columns before projecting into the response record, so the
   ordering translates to SQL; the order each endpoint returns is unchanged.
   Look for any other query in `Survsys.Api` that orders by a property of a
   positional-record projection, and give it the same change. No endpoint's
   route, contract or response changes.
3. `Survsys.IntegrationTests`: the existing tests are the proof. The whole
   integration suite passes, 174 of 174, with the 20 that fail today among them
   and no test changed to get there.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 57: Phase 7 walkthroughs and the full walkthrough regression

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

### Session 58: Release 0.8.0 (release: 0.8.0)

---

*Sessions 59 to 101 were planned on 2026-10-18 from
`docs/planning/gap-analysis.md`, an audit of the code against the legacy
feature matrix, the business-logic documents and the specifications under
`../nedss/docs/`. Several pipelines are built as tested libraries that nothing
in the running application calls, and core case work is missing. The operator
chose to ship phase 7 as 0.8.0, to close the gaps in phases 8 to 13, to add
electronic case reporting, the Connecticut ELR rules, full NMI and a master
person record, and to release 1.0.0 last. Every phase ends on its walkthroughs
and a release, as phases 0 to 7 did. A session that reaches SQL Server says so
in its tests step, because a session's run of record is the unit suite, which
does not (the lesson of session 56).*

*Phase 8 — case access and case editing → release 0.9.0. Specified by
`epitrax-core-workflows.md`, `epitrax-cmr-workflow.md`,
`epitrax-cmr-deep-editing.md`, `session-012-investigation-admin-contacts-notes.md`
and `phase-1.6-feature-matrix.md` §1–§8.*

### Session 59: Case search and getting back to a case

1. Register.
2. `Survsys.Api`: a paged case search over the case morbidity reports — by
   record number, patient name, disease, investigating agency, workflow state,
   case status and a date range — returning one row per case with its patient,
   disease, agency, state and MMWR week.
3. `Survsys.Web`: an Events screen in the navigation bar that runs the search
   and opens a case from its row; the record numbers on the work queues, the
   task list and the People results become links to the case; a person screen
   reached from People that shows the person's demographics and lists their
   cases.
4. `Survsys.IntegrationTests`: the search against SQL Server, each filter alone
   and combined, and paging.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 60: Correcting and removing a case's child records

1. Register.
2. `Survsys.Api`: edit and delete routes for specimens, tests, results and
   susceptibility rows, exposures, exposure places, contacts and sources, and
   attachments; a contact's disposition and its date updated after the
   exposure is saved; MMWR week and year recalculated whenever the onset or
   diagnosis date changes, by the rule `MmwrWeek` already implements.
3. `Survsys.Web`: edit and delete on the Laboratory, Contacts and Notes tabs'
   rows, each delete confirmed first.
4. `Survsys.IntegrationTests`: each route's update and delete against SQL
   Server, including the audit row each one writes and a delete of a row
   another row depends on refused with a Problem Details.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 61: The full investigation workflow

1. Register.
2. `Survsys.Data` and `Survsys.Api`: the legacy workflow's remaining
   transitions — route to another agency, assign to an investigator, do not
   investigate, reopen, approve and reject by the state — each gated by its
   privilege, and a transition history row (who, when, from, to, note) for
   every transition, with its migration.
3. `Survsys.Web`: the report's Workflow Options carrying only the transitions
   the case's state and the user's privileges allow, an investigator picker,
   and the transition history on the Administrative tab.
4. `Survsys.UnitTests` and `Survsys.IntegrationTests`: every allowed and every
   refused transition of `WorkflowRules`, and the history row each writes.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 62: Event types, contact cases and the contact import

1. Register.
2. `Survsys.Data` and `Survsys.Api`: an event type on every case — morbidity,
   contact or assessment — with its migration, existing cases becoming
   morbidity reports; a contact added as a contact event of its own, its person
   found by search or created new; a contact event promoted to a morbidity
   report.
3. `Survsys.Api` and `Survsys.Web`: the batch CSV contact import saving what it
   validates, as contact events under the chosen exposure, behind an upload
   screen on the Contacts tab that shows each refused row and why.
4. `Survsys.IntegrationTests`: a contact event's creation and promotion, and an
   import of good and refused rows.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 63: Treatments, encounters, occupation and pregnancy on a case

1. Register.
2. `Survsys.Data` and `Survsys.Api`: per-case treatments chosen from the
   condition's treatment list (or the whole catalogue) with dates and dose;
   healthcare encounters; occupation; pregnancy with its due date and delivery
   facility; vaccine manufacturer and information source; and the report
   header's investigator, brief note and separate state and local case status;
   with their migration and endpoints.
3. `Survsys.Web`: a Treatments tab and an Encounters tab, the new fields on
   the Clinical, Investigation and Vaccines tabs, and the header fields.
4. `Survsys.IntegrationTests`: each new route against SQL Server.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 64: The reference code lists behind every coded field

1. Register.
2. `Survsys.Data`: code tables, with their migration, for every field typed as
   a free-text code today — specimen source, test type, organism, test result,
   disposition, contact type, task category and priority, race, ethnicity,
   language, county, case status, imported-from, attachment category and the
   rest the gap analysis lists — seeded from the legacy codes in
   `../nedss/docs/db-snapshots/`, existing values left as they are.
3. `Survsys.Api` and `Survsys.Web`: one code-list endpoint, a screen to
   maintain the lists under Administration, and a select box in place of each
   free-text code on every tab, test types filtered by the case's condition.
4. `Survsys.E2ETests`: every walkthrough whose steps type a code chooses it
   from its list instead, document and test changed together.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 65: Delete, print and lock a case, and private notes

1. Register.
2. `Survsys.Data` and `Survsys.Api`: a case deleted softly and restored, a
   "show deleted" filter on the case search, a note's privacy, attachments on
   an outbreak, and an edit lock taken when a user opens a case to edit and
   released on save or timeout, with their migration.
3. `Survsys.Web`: the report opening read-only with an Edit button, the lock's
   holder shown to anyone else, delete and restore, and a printable view of
   the whole case for the browser to print or save as PDF.
4. `Survsys.IntegrationTests`: deletion, restoring, the lock's contention and
   its timeout.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 66: Reload the report when the address names another case

*Inserted after session 65. Writing the Phase 8 walkthroughs, the promote a
contact to a case walkthrough could not be performed: pressing **(contact
event)** on the Contacts tab changes the address from one `/event/edit/{id}` to
another, but the screen keeps showing the index case. `MorbidityReport.razor`
reads its report only in `OnInitializedAsync`, and Blazor reuses the page
component when only its route parameter changes, so a link from one report to
another never loads the second. The walkthroughs cannot ship a step their
tests cannot perform, so this comes before them.*

1. Register.
2. `Survsys.Web`: `MorbidityReport.razor` loads the report, its workflow and
   everything else it reads whenever `ReportId` changes, not only on first
   render, resetting the edit mode, the tab and the messages of the case it
   leaves; an edit lock held on the case it leaves is released first, as
   leaving the page releases it today. No other screen and no API route
   changes.
3. `Survsys.E2ETests`: the existing walkthrough tests still pass; the
   promote a contact to a case walkthrough in session 67 is what follows the
   link, so no walkthrough is written here.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 67: Phase 8 walkthroughs

1. Register.
2. `docs/walkthroughs/`: five documents — find a case, correct a laboratory
   result, route a case to another agency, promote a contact to a case, record
   a case treatment.
3. `Survsys.E2ETests`: the five Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 68: Release 0.9.0 (release: 0.9.0)

---

*Phase 9 — the master person record → release 0.10.0. Specified by
`EMSA_PERSON_MATCHING.md`, `phase-1.6-cross-system-data-model.md` and
`CT_SITE_REQUIREMENTS.md`. Phases 10 and 13 match inbound people against it.*

### Session 69: Person identifiers, name history and address history

1. Register.
2. `Survsys.Data` and `Survsys.Api`: person identifiers (type, value,
   assigning authority by CLIA or OID), a name history kept when a person's
   name changes, and an address rule of one current address with the rest
   historic by their dates, with their migration; addresses normalised on save
   (case, whitespace, zip plus four, state code) and the county taken from the
   zip where it is blank.
3. `Survsys.Web`: identifiers and name history on the Demographics tab and the
   person screen; person search finding a person by alias, former name or
   identifier.
4. `Survsys.UnitTests` and `Survsys.IntegrationTests`: the normalisation rules,
   and search by each new route.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 70: Person matching in the running application

1. Register.
2. `Survsys.Api`: a person-match endpoint that scores candidates with
   `PersonMatchRules` from name, birth date, sex, identifiers and address, the
   thresholds for an exact match, a probable match and no match set in
   configuration.
3. `Survsys.Web`: person search showing each candidate's score and why, and
   the staged-message review proposing its best match, with "create new
   person" beside it.
4. `Survsys.IntegrationTests`: the endpoint's verdict on the legacy match
   cases in `EMSA_PERSON_MATCHING.md`.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 71: Duplicate persons and merging them

1. Register.
2. `Survsys.Api`: candidate duplicate pairs found by the matcher, and a merge
   that keeps one person, moves the other's cases, labs, contacts, vaccines,
   identifiers, names and addresses onto them, keeps the merged-away record as
   a merged tombstone, and audits the merge, in one transaction.
3. `Survsys.Web`: a duplicates screen and a side-by-side merge screen,
   privilege-gated.
4. `Survsys.IntegrationTests`: a merge's every moved row, and a failed merge
   leaving both persons unchanged.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 72: Phase 9 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents — find a person by a former name,
   record a person's address history, merge duplicate persons.
3. `Survsys.E2ETests`: the three Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 73: Repair the three walkthroughs Phase 9 broke

*Inserted after session 72. The whole end-to-end suite, run after session 72
closed, fails three of its 40 walkthroughs every time. The run of record of
sessions 69 to 72 covered the unit suite only, so nothing caught them. The
release runs the whole walkthrough regression, so this repair comes before
the release.*

- *`find-a-person.md` step 6 and `FindAPersonTests` expect only the four
  columns ID, Last, First, Birth date and Birth sex. Session 70 added a
  **Score** and a **Why** column to every person search made with criteria.*
- *`record-a-treatment.md` and `record-a-case-treatment.md` fail at step 1,
  pressing **Treatments**. The application bar has grown past the width of
  the window, most likely when session 71 added **Duplicate persons**, and
  it runs off the right-hand edge rather than wrapping. **Treatments** is
  rendered outside the viewport, so it cannot be pressed.*

*`log-an-exposure-event.md` also failed once at step 13, where "The exposure
has been recorded." never appeared. It passed on two later full runs, so it
is intermittent and is watched here rather than repaired.*

1. Register.
2. `docs/walkthroughs/find-a-person.md` and
   `Survsys.E2ETests/FindAPersonTests.cs`: after a search with criteria, the
   results carry the **Score** and **Why** columns. The document says what
   those read on the two Rodriguez rows, and the test asserts the six column
   headings and those values. Nothing else in either file changes.
3. `Survsys.Web`: `MainLayout.razor` lets the application bar's buttons wrap
   onto further lines rather than run past the window, so every button a
   user holds the privilege for stays visible and pressable at the suite's
   window size. No button is renamed, reordered or removed, and every
   element id stays unchanged. The two treatment walkthroughs and their
   tests pass unchanged.
4. Cross-provider verification.
5. Full test suite, the end-to-end walkthrough suite included, recorded as the
   run of record.
6. Close-out.

### Session 74: Release 0.10.0 (release: 0.10.0)

---

*Phase 10 — inbound ELR to parity → release 0.11.0. The EMSA half finished: the
libraries phase 5 built are wired into intake. Specified by
`EMSA_MESSAGE_PIPELINE.md`, `EMSA_RULES_ENGINE.md`, `EMSA_VOCABULARY_SYSTEM.md`,
`EMSA_PERSON_MATCHING.md`, `EMSA_EPITRAX_CONTRACT.md`, `HL7_FIELD_MAPPINGS.md`,
`MIRTH_REPLACEMENT_SPEC.md`, `emsa-ui-overview.md` and
`CT_SITE_REQUIREMENTS.md` §1.*

### Session 75: Parsed messages, the parser seam and MLLP

1. Register.
2. `Survsys.Messaging` and `Survsys.Data`: a parser seam that takes raw
   message text to the parsed fields intake keeps, with `OruR01Parser` its HL7
   v2 implementation and room for a CDA one in phase 13; a message type and
   format on every staged message; the parsed demographics, sending facility,
   orders, results and specimens stored, one staged message per OBR; with the
   migration.
3. `Survsys.Api`: an MLLP listener beside the file drop, off unless
   configured, answering each message with an ACK or a NAK; a message from a
   sending facility (MSH-4) that is not on file held as an exception.
4. `Survsys.UnitTests` and `Survsys.IntegrationTests`: the OBR split, ACK and
   NAK framing, and an unknown sender.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 76: Vocabulary and rules at intake

1. Register.
2. `Survsys.Api`: intake translating each result through `VocabularyMatcher`'s
   child, master and application tiers to its condition, organism and
   interpretation; the message filter and case-management rules evaluated by
   `ExpressionEvaluator`; the investigating agency chosen from the patient's
   zip.
3. `Survsys.Api` and `Survsys.Web`: vocabulary screens for master and child
   LOINC, organisms, specimens and results, with lab-specific overrides, CSV
   import and export, and the rules on a condition's screen.
4. `Survsys.IntegrationTests`: the legacy vocabulary and rule cases from the
   EMSA documents.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 77: The graylist, automatic assignment and the full queue

1. Register.
2. `Survsys.Data` and `Survsys.Api`: the remaining legacy queue states —
   pending, locked, QA, out of state, semi-automatic, unprocessed and the gray
   cluster — with their migration; a message person-matched and rule-matched
   with confidence assigned without a reviewer, and one without confidence
   sent to pending; the graylist holding a result with no case to attach to,
   a hosted service releasing it when a case appears and pruning it after its
   window.
3. `Survsys.Web`: the queue filtered by every state, and move, delete, retry
   and bulk actions.
4. `Survsys.IntegrationTests`: an automatic assignment, a graylisted message
   released and one pruned.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 78: Labs from messages, and updating an existing case

1. Register.
2. `Survsys.Api`: promotion writing the message's specimen, tests, results and
   susceptibilities and its person data onto the case, in one transaction with
   the message's assignment, so a failure leaves neither and a second
   promotion is impossible.
3. `Survsys.Api`: a message for a person with an open case on the condition
   updates that case — by the whitelist rules, the "not a case" check, and lab
   comparison as add, update, duplicate or exception, with test-status upgrade
   and lab knitting — and a person mismatch sends the message to pending.
4. `Survsys.IntegrationTests`: each lab comparison outcome, and a promotion
   that fails part-way.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 79: Exceptions, reprocessing, notifications and statistics

1. Register.
2. `Survsys.Data` and `Survsys.Api`: typed exceptions with the legacy
   categories and whether each may be retried, reprocessing of retryable ones
   by a hosted service and by hand, and the message audit trail of every state
   a message passes through, with their migration.
3. `Survsys.Api` and `Survsys.Web`: batch notification e-mail of assigned
   messages to the agency's contacts over configured SMTP, and an intake
   statistics screen by sender, state and day.
4. `Survsys.IntegrationTests`: a retried exception and its audit trail.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 80: Connecticut ELR conformance

1. Register.
2. `Survsys.Messaging`: a conformance profile engine for the usage codes R,
   RE, O and X and the conditional C(R/X), C(RE/X), C(R/RE) and C(R/O), with a
   failed check recorded on the message as an exception or a warning by its
   usage.
3. `Survsys.Messaging` and `Survsys.Api`: the Connecticut profile from
   `CT-ELR-HL7-251_Implementation_Guide_v1.0.pdf` — the CT OIDs per
   environment in configuration, CLIA or OID in PID-3, ORC-2/3, OBR-2/3 and
   SPM-2, OBR-16, OBR-17, ORC-24 and OBX-2 required, a patient town checked
   against the 169 CT towns, and NK1 required on a child's blood-lead result —
   chosen per sender.
4. `Survsys.UnitTests`: a passing and a failing message for every CT rule.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 81: Connecticut planning regions as county equivalents

*Inserted after session 80. Since 2022 the Census Bureau has counted
Connecticut's nine planning regions as its county equivalents (FIPS 09110 to
09190), in place of its eight historical counties (09001 to 09015), and CDC
expects the planning region's FIPS code for a Connecticut address. Today the
`county` code list holds only Utah's legacy two-letter codes and "Out-of-state",
an address's county is one of those, and `OruR01Generator` writes that code
unchanged into PID-11 for `DEM165`. A Connecticut zip code can also cross a
planning region's boundary, so the zip-to-county table session 69 added cannot
fill a region reliably on its own. This follows session 80 because it uses that
session's list of the 169 CT towns.*

*Amended before it ran: not every program has moved to the planning regions.
ELR and eCR still derive the historical county, not the county equivalent. So a
Connecticut address keeps both, and each program is configured to use one or
the other. The historical counties stay choosable rather than being refused.*

1. Register.
2. `Survsys.Data`: the `county` code list carries each value's state, its
   FIPS code, and whether it is a county or a county equivalent.
   Connecticut's eight historical counties are seeded as counties and its
   nine planning regions as county equivalents, each with its FIPS code, and
   both stay choosable. An address gains a county equivalent beside its
   county. A `ct_towns` table maps each of the 169 towns to its historical
   county and its planning region. One migration adds all of these, changes
   no existing address, and keeps every Utah code as it is.
3. `Survsys.Api`: where the state is CT, a save that leaves an address's
   county or county equivalent blank fills each one from the town, and fills
   the county from the zip only when the town gives none.
4. `Survsys.Api`: a setting per program — `elr`, `ecr` and `cdc-notification`
   — names which of the two that program uses, `county` or
   `county-equivalent`. The defaults are `county` for ELR and eCR and
   `county-equivalent` for CDC notification. ELR intake stores the county
   code a message carries in whichever of the two it names, by the code
   list, and fills the other from the town. `OruR01Generator` writes the
   FIPS code of the one `cdc-notification` names to PID-11, and the legacy
   code where the list has no FIPS code. Session 104 reads `ecr` the same way
   when it writes an eICR's address to a case.
5. `Survsys.Web`: the Demographics tab, the person screen and the case search
   show the county equivalent beside the county for a CT address. Its label
   comes from configuration ("Planning region" by default, or "County
   equivalent"), and every existing element id stays unchanged.
6. `Survsys.UnitTests` and `Survsys.IntegrationTests`: both values filled
   from a CT town, the zip used when the town is unknown, ELR intake storing
   a historical county and a planning region each in its own field, PID-11
   carrying the configured one's FIPS code under each setting, and the
   configured label.
7. Cross-provider verification.
8. Full test suite, recorded as the run of record.
9. Close-out.

### Session 82: Repair ELR promotion's address and result value

*Inserted after session 81. Two faults found in phase 10 make a real laboratory
message fail promotion. The transaction rolls back each time, leaving no case,
no lab and the message still waiting. They come before the Phase 10
walkthroughs because "update an existing case from a laboratory message"
crosses both.*

- *`MessageCaseWriter.AddAddress` adds the message's patient address with no
  ending whenever the person has no address with that street and zip. For a
  person who already has a different current address, the unique index
  `IX_person_addresses_person_id_current`, which allows one address with no
  ending, refuses the second. Session 81's integration test deletes the seeded
  address to step around it.*
- *`staged_message_results.result_value` holds 2000 characters and
  `lab_test_results.result_value` holds 200, so a result longer than 200
  characters throws on the write. `MessageCaseWriterTests.APromotionThatFailsPartWayLeavesNothing`
  uses exactly that gap to fail part-way.*

1. Register.
2. `Survsys.Api`: where `MessageCaseWriter` adds a message's address, the
   person's current address is closed as a save closes it, by
   `AddressRules.CloseHistoric`, so the message's address becomes the one
   current address. An address the person already has with that street and
   zip is still not added again.
3. `Survsys.Data`: `lab_test_results.result_value` widened to 2000 characters
   to match the staged result it is written from, with its migration. No
   other column changes.
4. `Survsys.IntegrationTests`: a promotion for a person with a different
   current address, which leaves the old address closed and the message's
   current; a result of 2000 characters promoted whole;
   `APromotionThatFailsPartWayLeavesNothing` given a new way to fail part-way
   now that the result fits; and session 81's test no longer deleting the
   seeded address. These reach SQL Server.
5. Cross-provider verification.
6. Full test suite, the integration suite included, recorded as the run of
   record.
7. Close-out.

### Session 83: Reference data administration — code lists and addresses

*Inserted after session 81. Reference data must be viewable on an
administration screen and kept current by an administrator, by editing a row
or uploading a file, rather than by a release. Today only the five vocabulary
tables can be uploaded or downloaded (`/vocabulary`, by `VocabularyCsv`). The
code lists can be edited on `/codes` but not uploaded. `ct_towns` and
`zip_codes` have no endpoint and no screen at all, and `AddressRules` reads the
town seed in code, so a corrected row could not take effect. Zip codes reach an
agency only by typing them into its form. The operator has decided the shipped
rows need not be current or checked against an official list, such as the
Census Bureau's, so long as an administrator can correct them. `CtTownSeed`
stays as the starting data.*

*Session 81 also owes two things this session covers. The County and planning
region columns on the person screen and the case search show a raw code such
as 09003 rather than its name. And the end-to-end tests were not run, so its
screens were never seen rendered.*

1. Register.
2. `Survsys.Api`: one CSV import and export for reference tables, built from
   what `VocabularyCsv` does. Export writes every row with a header row.
   Import first returns a preview of the rows it would add, the rows it would
   change, and the rows it refuses with each one's reason. Nothing is written
   until the administrator confirms. A row is matched to an existing one by
   the table's natural key, and an existing row the file leaves out is kept
   as it is. The vocabulary tables move onto it with their CSV columns
   unchanged.
3. `Survsys.Api` and `Survsys.Web`: import and export for each code list on
   `/codes`, with the county list's state, FIPS code and kind, and with test
   types' conditions. A code is still retired by clearing Active, never
   deleted.
4. `Survsys.Api` and `Survsys.Web`: endpoints and an administration screen
   for the Connecticut towns (town, county, planning region) and for the
   zip-to-county table, each with view, add, edit, import and export.
   `AddressRules` and ELR intake read the towns from the database, so an
   upload takes effect on the next save. Import and export of an agency's
   jurisdiction zip codes on `/agencies`. Each screen and action requires the
   administration privilege that `/codes` requires.
5. `Survsys.Web`: the person screen and the case search show a county's and
   a county equivalent's name from the county list, not its code. Every
   element id stays unchanged.
6. `Survsys.UnitTests` and `Survsys.IntegrationTests`: a preview that adds,
   changes and refuses rows; a confirmed import; an export read back in
   unchanged; a town corrected by upload and then used by the next address
   save. These reach SQL Server.
7. `Survsys.E2ETests`: a Playwright test that uploads a CT town file and
   saves a Connecticut address with its town alone, then finds its county
   and planning region filled in by name on the Demographics tab, the person
   screen and the case search.
8. Cross-provider verification.
9. Full test suite, the integration and end-to-end suites included, recorded
   as the run of record.
10. Close-out.

### Session 84: Reference data administration — facilities, conditions, treatments and vaccines

*Inserted after session 81, and the second half of session 83's work. These
tables have screens but no upload or download. Vaccines have no edit screen at
all, only the catalogue a case's vaccine field reads. Notification guides,
PHIN variables and value sets are left to session 90, which already imports
them.*

1. Register.
2. `Survsys.Api` and `Survsys.Web`: import and export, by session 83's CSV
   and its preview, for facilities (sending facilities among them), for
   conditions, and for treatments with the conditions each one is offered
   for, each on its existing screen.
3. `Survsys.Api` and `Survsys.Web`: a vaccines administration screen with
   view, add, edit, import and export. A vaccine is retired, never deleted,
   so a case's vaccine record still shows it.
4. `Survsys.IntegrationTests`: an import and an export for each table, and a
   refused row for each. These reach SQL Server.
5. `Survsys.E2ETests`: a Playwright test that uploads a facility file and
   finds the new facility on `/facilities`.
6. Cross-provider verification.
7. Full test suite, the integration and end-to-end suites included, recorded
   as the run of record.
8. Close-out.

### Session 85: Editor settings ignored, and the integration sign-in flake

*Inserted after session 81. Two faults outside the product keep stopping the
framework. Choosing the authoring model in VS Code writes
`.vscode/settings.json`, which `.gitignore` does not cover, and the untracked
file refuses a release session's working-tree and verification gates, as it
did at 0.4.0. And since session 79 the full integration run has now and then
failed `PersonConditionEndpointsTests.TheOptionsAreTheStatesMovesNarrowedToTheCallersPrivileges`
on a 401 from `SurvsysApi.SignIn`, though the class passes alone. Most likely
another test changes a user, its password or its lockout and does not put it
back. Both come before release 0.11.0.*

1. Register.
2. `.gitignore`: `.vscode/` ignored as `.dabbler/` is, with a comment saying
   why. Nothing else in the file changes.
3. `Survsys.IntegrationTests`: the test that leaves the sign-in state behind
   is found, and is made to sign in with a user of its own or to put back
   what it changed, so no test depends on the order the suite runs in. The
   session's notes name the test and the cause.
4. Cross-provider verification.
5. Full test suite, the integration suite included, recorded as the run of
   record.
6. Close-out.

### Session 86: Phase 10 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — find a message assigned
   automatically, update an existing case from a laboratory message, release
   a graylisted message, maintain a LOINC mapping.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 87: Release 0.11.0 (release: 0.11.0)

---

*Phase 11 — outbound CDC notification to parity → release 0.12.0. The NMI half
finished: the generator, mapper and transmitter phase 6 built are put to work.
Specified by `NMI_EXPORT_PIPELINE.md`, `NMI_RULESETS.md`, `NMI_XML_GENERATION.md`
and `nmi-ui-overview.md`. NETSS stays unbuilt until the operator records CDC's
answer in `docs/planning/netss-export-question.md`.*

### Session 88: The notification queue worker and the export log

1. Register.
2. `Survsys.Api`: a hosted service that takes each released entry off
   `nmi_export_queue`, builds its message with `PhinVariableMapper`, the rule
   sets, `RepeatingGroupProcessor` and `OruR01Generator`, sends it with
   `CdcTransmitter`, and writes the outcome to `nmi_log` with its detail and
   severities; the processing id (production, training, debug) and the queue
   type (production, onboarding, batch) in configuration; and a "process
   queue now" action.
3. `Survsys.Web`: the export log showing each run's detail, and the generated
   message of an export the worker made.
4. `Survsys.IntegrationTests`: a queued case through to its log row against a
   stub CDC endpoint, and a refused send logged as a failure.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 89: Auto-queue on every change, queue actions and reconciliation

1. Register.
2. `Survsys.Api`: a case queued for notification on every change that reaches
   a notified variable — labs, clinical, investigation, administrative and
   demographics — when the condition's print criteria pass; the print
   criteria per condition, with their migration.
3. `Survsys.Api` and `Survsys.Web`: remove from the queue, retract a sent
   notification (NOT118 = X), resend, export specific records or an MMWR
   range, and the reconciliation cutoff stored and set on screen.
4. `Survsys.IntegrationTests`: each queue action and the cutoff's effect.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 90: Guide, rule set and value set administration

1. Register.
2. `Survsys.Api` and `Survsys.Web`: screens for notification guides, their
   PHIN variables and variable defaults, and rule sets; guide JSON import and
   export; PHIN value sets and units of measure, imported from CSV with a
   preview.
3. `Survsys.Messaging`: hepatitis lab grouping, organism and test-type
   overrides, the arbovirus two-OBR message and the NK1 mother segment.
4. `Survsys.UnitTests`: each generator change, against the legacy examples in
   `NMI_XML_GENERATION.md`.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 91: Transport to CDC

1. Register.
2. `Survsys.Api`: a transport chosen in configuration — the HTTP post that
   exists, or files written to a folder that a PHINMS or AIMS sender picks up
   — and batch-file tracking of what each file carried.
3. `Survsys.Api`: retention of the export log by a configured age, and the
   transport's credentials read from configuration, never stored.
4. `Survsys.IntegrationTests`: the file transport's output and its batch
   record.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

**Non-goals.** No live transmission to CDC: nothing in this session — code,
test or configuration — sends to a real CDC, PHINMS or AIMS endpoint. The HTTP
transport is exercised only against a local stub, the file transport writes
only to a test folder, and the shipped configuration leaves the endpoint empty
so transmission stays off.

### Session 92: Phase 11 walkthroughs

1. Register.
2. `docs/walkthroughs/`: three documents — send a case to CDC, retract a
   notification, configure a notification guide. Review a generated message
   and inspect the export log are rewritten to read rows the worker made,
   not seeded ones.
3. `Survsys.E2ETests`: the three Playwright tests named after them, and the
   two rewritten tests.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 93: Release 0.12.0 (release: 0.12.0)

---

*Phase 12 — audit, access, classification and reporting → release 0.13.0.
Specified by `phase-1.6-cross-cutting-patterns.md`, `CLASSIFICATION_RULES.md`
and `phase-1.6-feature-matrix.md` §11–§14.*

### Session 94: Audit with its user and values, and its screens

1. Register.
2. `Survsys.Data`: the audit interceptor recording the signed-in user and the
   values before and after as JSON, with its migration; sign-ins, exports and
   searches audited, and vocabulary changes among the audited tables.
3. `Survsys.Web`: an audit screen of a case's history and of a table's, and an
   access-audit screen, both privilege-gated.
4. `Survsys.IntegrationTests`: an insert, update and delete each recorded with
   user and values, and a rolled-back save recording nothing.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 95: Jurisdiction-scoped access and sign-in protection

1. Register.
2. `Survsys.Api`: a user's case access scoped to their agencies' jurisdictions
   and to cases shared with them, every case route and the case search
   refusing the rest; the Forms and Treatments writes needing the privilege
   the navigation bar already asks for.
3. `Survsys.Api` and `Survsys.Web`: account lockout after failed sign-ins, and
   data protection keys persisted to a configured folder on both tiers, so a
   sign-in survives an app-pool recycle and a second server.
4. `Survsys.IntegrationTests`: a case out of the user's jurisdiction refused
   on every route, and lockout.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 96: Classification rules on save

1. Register.
2. `Survsys.Data`: the validation and classification rules of
   `CLASSIFICATION_RULES.md` run on every case save — chlamydia, gonorrhea,
   hepatitis C, influenza, syphilis, measles and COVID — with out-of-state
   status from the patient's address and the jurisdiction taken from it.
3. `Survsys.Api`: contacts of chlamydia, gonorrhea and syphilis cases closed
   when the rule says, and each rule's outcome shown to the user on save.
4. `Survsys.UnitTests`: every rule's cases from `CLASSIFICATION_RULES.md`.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 97: Classification shown on save, and on encounter saves

*Inserted after session 96. Session 96 left two things owed. Its step 3 asked
for each rule's outcome to be shown to the user on save. The outcome is
written as an administrative note, which the user finds only by opening the
Notes tab. The Clinical, Laboratory, Investigation and Administrative tabs
tell the report page nothing when they save, so the case status in its header
stays as it was until the page is reloaded. And an encounter's save does not
run the rules, though the influenza rule reads a facility encounter as the
case's recorded hospitalization, so its finding stays stale until something
else saves the case. Both come before the Phase 12 walkthroughs because "see
a case classified on save" crosses them.*

*Non-goals: no hospitalization field and no "Out of State" agency. Both are
decisions for session 109's gap analysis.*

1. Register.
2. `Survsys.Api`: every route that classifies a case answers with the
   classifier's outcome — the case status after it and that save's findings.
   The encounter add, edit and delete routes run the classifier too.
3. `Survsys.Api`: when a case is classified again, a classification note
   whose finding no longer holds is struck, so a case does not gather
   findings that are no longer true. The note for a finding that still
   holds is still written once.
4. `Survsys.Web`: each tab that saves a case tells the report page. The page
   re-reads the case's status and shows that save's findings beside the
   save. Every element id stays unchanged.
5. `Survsys.UnitTests` and `Survsys.IntegrationTests`: the outcome each
   classifying route answers with, an encounter's save reclassifying an
   influenza case, and a stale classification note struck. These reach SQL
   Server.
6. `Survsys.E2ETests`: a Playwright test that adds a positive lab to a case
   with no status and, without a reload, sees the header show Confirmed and
   the findings shown beside the save.
7. Cross-provider verification.
8. Full test suite, the integration suite included, recorded as the run of
   record.
9. Close-out.

### Session 98: IBIS, ad-hoc reporting and system configuration

1. Register.
2. `Survsys.Web`: a Send to IBIS action on the report, privilege-gated, over
   the endpoint session 55 built.
3. `Survsys.Api` and `Survsys.Web`: ad-hoc reporting over the saved views —
   counts grouped by any of a view's columns, shown on screen and exported —
   and condition configuration's notification guide, print criteria and
   intake flags.
4. `Survsys.Data`, `Survsys.Api` and `Survsys.Web`: system configuration
   settings stored in the database and edited on an Administration screen.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 99: Phase 12 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — review a case's audit history, run
   an ad-hoc report, send a case to IBIS, see a case classified on save.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 100: Release 0.13.0 (release: 0.13.0)

---

*Phase 13 — electronic case reporting → release 0.14.0. Specified by
`docs/planning/ecr-requirements.md`, `CT_SITE_REQUIREMENTS.md` §2 and the CT DPH
eCR documents in `../nedss/docs/specs/site-specs/`. Its items marked
**[confirm]** are confirmed with the APHL eCR service desk before the session
that depends on them, and a session that finds one wrong amends this phase.*

### Session 101: eICR and RR parsing and document intake

1. Register.
2. `Survsys.Messaging`: a CDA parser behind phase 10's parser seam, reading
   eICR R1.1 and R3.1/3.1.1 and RR 1.1 by templateId with LINQ to XML, into
   the patient, encounter, problems, results, medications, pregnancy, travel,
   occupation, and the RR's conditions with their determinations and
   responsible agencies.
3. `Survsys.Api`: intake of eICR and RR documents from configured folders, a
   test and a production feed kept apart, the raw XML stored unchanged, each
   document taken once by its id, an RR paired with its eICR, and an eICR's
   versions grouped by setId and ordered by version number.
4. `Survsys.UnitTests`: the HL7 IG examples of each version parsed, and a
   re-sent version grouped with its first.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 102: eCR validation and the eICR viewer

1. Register.
2. `Survsys.Messaging`: validation against the CDA schema and the HL7
   schematron, its findings stored on the document without blocking it.
3. `Survsys.Web`: an eCR queue and a document screen showing the eICR through
   the HL7 CDA stylesheet beside the RR's conditions, determinations and
   validation findings.
4. `Survsys.UnitTests`: a valid and an invalid document of each version.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 103: Reportability and condition mapping

1. Register.
2. `Survsys.Data` and `Survsys.Api`: a mapping from RCTC and SNOMED condition
   codes to the conditions catalogue, with its migration and an import from an
   RCTC release; each RR condition kept where this jurisdiction is the
   responsible agency and the determination is reportable.
3. `Survsys.Web`: the mapping screen, and "may be reportable" and unmapped
   conditions sent to the eCR queue for review.
4. `Survsys.IntegrationTests`: each determination code's routing.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 104: Cases from eCR

1. Register.
2. `Survsys.Api`: a reportable eICR's patient matched by phase 9's matcher,
   linked to the open case on that person and condition — including one opened
   from ELR — or opening a new one, by the reviewer where the match is not
   confident.
3. `Survsys.Api`: the eICR's demographics, encounter, problems, medications as
   treatments, pregnancy, travel and occupation written to the case, its labs
   only for senders set to update lab data, each value recording the eICR id
   and version it came from; a later version updating what an earlier one
   wrote and nothing a user has edited. The eICR's county is stored as a
   county or a county equivalent according to the `ecr` setting from session 81,
   with the other filled from the town.
4. `Survsys.IntegrationTests`: a new case, a linked case, and a second version.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 105: eCR senders and data quality

1. Register.
2. `Survsys.Data` and `Survsys.Api`: eCR senders with their onboarding stage —
   test, validation, production — and their settings, with their migration.
3. `Survsys.Web`: a senders screen and a data quality screen by sender and
   condition: volumes, validation findings, receipt latency, and an RR with no
   eICR or an eICR with no RR.
4. `Survsys.IntegrationTests`: the quality figures over seeded documents.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 106: Phase 13 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — review an eICR and its reportability
   response, map a reportable condition, open a case from an eICR, link an
   eICR to an existing case.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 107: The integration suite before every release, on SQL Server in a container

*Inserted after session 106. The framework's run of record is the unit suite
alone, and it has never run the integration suite. Sessions whose plans say
"the integration suite included" still recorded a unit-only run. So an
integration failure goes unseen until someone runs the suite by hand: session
95 found `PersonMergeEndpointsTests.AMergeThatFailsPartwayLeavesBothPeopleUnchanged`
broken since session 94. The suite needs a reachable SQL Server, so it should
bring its own. Testcontainers for .NET (`Testcontainers.MsSql`) is the usual
way to do that: the fixture starts a SQL Server 2022 container, waits until it
is ready, and hands back its connection string. `dotnet test` then needs
nothing started first. Nothing in the schema uses a Windows-only feature. This
comes before release 0.14.0 so that release is the first one the integration
suite gates.*

*Non-goals: ordinary sessions keep the unit suite as their run of record, and
no CI pipeline runs the integration suite. No script, no compose file, and no
password anyone has to keep: Testcontainers generates one for each run. The
end-to-end harness keeps its own server for now. No product code changes. If
the first run in a container fails because of the product rather than the
container, the failure is recorded in the session's notes and a repair session
is planned before the release.*

1. Register.
2. `Survsys.IntegrationTests`: a reference to `Testcontainers.MsSql`, and
   `SqlServerFixture` starting a SQL Server 2022 container for the run when
   `SURVSYS_SQLSERVER` is not set. When it is set, the fixture uses that
   server as it does today, so the suite can still run against a local SQL
   Server. The container is removed when the run ends. If no container
   engine is reachable, the fixture fails loudly, naming the engine it tried,
   `DOCKER_HOST` and `SURVSYS_SQLSERVER`, the same way an unreachable server
   fails it today. It never skips.
3. Podman on the operator's machine: Testcontainers reaches Podman through its
   Docker-compatible API, with `DOCKER_HOST` pointing at it where Docker
   Desktop would otherwise answer. Whether Testcontainers' cleanup container
   (Ryuk) has to be turned off with `TESTCONTAINERS_RYUK_DISABLED` is settled
   by trying it. The session's notes record the settings that worked, and none
   of them is written to a file the repository tracks.
4. `dabbler.yaml`: the integration suite becomes one a release session must
   run whole and green, while an ordinary session's run of record stays the
   unit suite. The session first finds out what the installed router can
   express: a suite only a release runs, or one the release selects. Its
   notes say which was used. If the router can express neither, the release
   gate becomes a documented step of the release session, and the notes say
   so.
5. `docs/technical/README.md`: the suites table and its paragraph describe
   how the integration suite gets its server. It starts a container unless
   `SURVSYS_SQLSERVER` names one. The paragraph also gives the Podman
   settings and the machine memory the suite needs (4 GiB, where 2 GiB is
   SQL Server's bare minimum).
6. The whole integration suite run in a container. Failures caused by the
   container (sign-in, timing, collation) are fixed here.
7. Cross-provider verification.
8. Full test suite, the integration suite included and run in a container,
   recorded as the run of record.
9. Close-out.

### Session 108: Release 0.14.0 (release: 0.14.0)

*The first release the integration suite gates (session 107). The router
cannot declare a suite only a release runs, so the gate is this step.*

1. Before anything is published, the integration suite run whole in a
   container, with `SURVSYS_SQLSERVER` unset:
   `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj --blame-hang-timeout 5m`.
   It must be green. A red run stops the release: the step is reported
   blocked with the failures, and a repair session is planned before a new
   release session.

---

*Phase 14 — parity → release 1.0.0, the first release that claims parity.*

### Session 109: The parity check and the documentation for 1.0.0

1. Register.
2. `docs/planning/gap-analysis.md`: every gap marked closed with the session
   that closed it, or left open with the operator's decision; every row of
   `phase-1.6-feature-matrix.md` given its survsys status. Among them are
   session 96's three departures from the legacy classification: an
   out-of-state case keeps its agency, since there is no "Out of State"
   agency; a facility encounter stands in for an influenza hospitalization,
   since there is no hospitalization field; and contacts close at the LHD's
   acceptance, since this workflow has no `approved_by_lhd`.
3. `Survsys.E2ETests`: the full walkthrough regression, every document read
   against its own test as session 57 did.
4. `docs/`: the user and technical guides brought to 1.0.0, with every "built
   but not wired in" statement gone or still true.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 110: Attachment and note dates in local time

*Inserted after session 109. Its walkthrough regression, run at 20:55 in
Connecticut, failed `attach-a-document.md` step 13: the **Attachments** table
read tomorrow's date. `AttachmentEndpoints` stores `CreatedAt` as
`DateTimeOffset.UtcNow`, and `AttachmentsPanel.razor`'s `Attached` writes it
as `yyyy-MM-dd` without converting it, so from 20:00 (19:00 in winter) until
midnight every attachment is dated the next day. `NotesTab.razor`'s `Written`
does the same with a note's date. The Administrative tab, the edit lock and
the print view already convert with `ToLocalTime()`. A walkthrough that fails
by the clock is a defect of the release, so this comes before it.*

1. Register.
2. `Survsys.Web`: `AttachmentsPanel.razor`'s `Attached` and `NotesTab.razor`'s
   `Written` show the moment's date in local time, by `ToLocalTime()` as the
   Administrative tab does. Nothing stored changes, and every element id
   stays unchanged.
3. `Survsys.UnitTests`: the two dates, for a moment late in the evening
   local time whose UTC date is the next day.
4. Cross-provider verification.
5. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
6. Close-out.

### Session 111: Jurisdiction on every list, and read-only sharing

*Inserted after session 109, whose gap analysis found that `CaseAccess`
narrows every case route and the case search but not the work queue and
agency board (`/api/work-queue`), the task list (`/api/tasks`), saved-view
exports and counts (`SavedViewQueries.Rows` takes no caller), or the case-count
report (`/api/reports/case-counts`); and that a case shared read-only
(`person_condition_shares`) can be changed by the agency it is shared with —
"read-only in intent and not in enforcement" (`WorkQueueEndpoints.cs`). The
operator decided on 2026-10-23 that sharing is read-only in fact.*

1. Register.
2. `Survsys.Api`: the work queue, the agency board, the task list, saved-view
   exports and counts and the case-count report list only the cases
   `CaseAccess.Visible` lets the caller see. A work queue asked for an agency
   that is not the caller's, without `all_jurisdictions`, answers no rows.
3. `Survsys.Api`: a case the caller sees only through a share is read-only to
   them: `CaseAccess` refuses every write to it, and to every record under it,
   with a 403 saying the case is shared with their agency read-only. Reading
   it is unchanged. `all_jurisdictions` and the investigating agency's users
   write as before.
4. `Survsys.Web`: the report opens a shared case without **Edit**, saying it is
   shared with the user's agency read-only. Every element id stays unchanged.
5. `Survsys.IntegrationTests`: each list narrowed, a work queue for another
   agency empty, and a write to a shared case and to a record under it
   refused. These reach SQL Server.
6. `docs/`: the user and technical guides' jurisdiction paragraphs, and the
   gap analysis's open item marked closed.
7. Cross-provider verification.
8. Full test suite, the integration and end-to-end suites included, recorded
   as the run of record.
9. Close-out.

### Session 112: Address geocoding

*Inserted after session 109. An address keeps no latitude or longitude
(`PersonAddress` left the legacy geocoding columns out), and nothing geocodes
one. The operator decided on 2026-10-23 that an address is geocoded by the US
Census Geocoder, falling back to Google's Geocoding API when the Census
Geocoder fails or finds no match.*

**Non-goals.** No test, check or shipped configuration calls either real
service: both are exercised against local stubs, and the shipped
configuration leaves geocoding off. No map, and no geocoding of exposure
places, which already carry their own coordinates.

1. Register.
2. `Survsys.Data`: `person_addresses` gains `latitude`, `longitude` and
   `geocoded_by` (`census`, `google` or `manual`), with their migration; no
   existing address changes.
3. `Survsys.Api`: a geocoder behind one interface — the Census Geocoder's
   one-line address lookup first, Google's Geocoding API when it fails, times
   out or finds no match — run where an address is saved with a street and no
   coordinates: the Demographic tab, ELR promotion and eCR intake. A failure
   of both leaves the coordinates empty and never stops the save. Settings
   `Geocoding:Enabled` (off by default), each service's endpoint and timeout,
   and `Geocoding:GoogleApiKey`, read from the environment only, never a file;
   with no key, Google is skipped. Coordinates typed by hand are kept, as
   `manual`, and an address whose street changes is geocoded again.
4. `Survsys.Web`: **Latitude** and **Longitude** on the Demographic tab's
   address rows and the person screen, with where they came from. Every
   existing element id stays unchanged.
5. `Survsys.UnitTests` and `Survsys.IntegrationTests`: Census found, Census
   failing over to Google, both failing, no Google key, a hand-typed pair
   kept, and a changed street geocoded again, against stub services.
6. `docs/`: the user guide's address paragraph, the technical guide's
   settings table (with what the services are sent: the street address, and
   nothing else about the person), and the gap analysis's open item marked
   closed.
7. Cross-provider verification.
8. Full test suite, the integration suite included, recorded as the run of
   record.
9. Close-out.

### Session 113: Held eICRs, attachments, and no system settings

*Inserted after session 109, whose gap analysis found three things with an
API and no use on a screen: an eICR held for review can be settled only by
`POST /api/ecr-documents/{id}/case`; an attachment cannot be downloaded or
removed though `AttachmentEndpoints` has both routes; and the System settings
screen keeps name-value pairs that nothing reads. The operator decided on
2026-10-23 that the settings screen is removed and every setting stays in
configuration.*

1. Register.
2. `Survsys.Web`: on a held eICR's screen, the proposed people from
   `GET /api/ecr-documents/{id}/person-matches`, each with **Use this
   person**, beside **Create new person**; choosing one settles the document
   through `POST /api/ecr-documents/{id}/case` and shows the cases it opened
   or added to. Gated as the eCR writes are.
3. `Survsys.Web`: each attachment's file name downloads it, on a case and on
   an outbreak, and **Delete** removes it after asking first, with
   `edit_cases`.
4. `Survsys.Data`, `Survsys.Api` and `Survsys.Web`: the System settings
   screen, its navigation button, `SystemSettingEndpoints`, its privilege
   rule and the `system_setting` table removed, with a migration that drops
   the table.
5. `Survsys.UnitTests` and `Survsys.IntegrationTests`: a held eICR settled by
   person and by a new person, an attachment downloaded and deleted, and the
   settings routes gone.
6. `docs/`: the user and technical guides, the eCR walkthrough documents that
   say there is no screen for a held document, and the gap analysis — the
   held eICR and attachment items marked closed, and the feature matrix's
   *System configuration* row marked simplified to configuration files.
7. Cross-provider verification.
8. Full test suite, the integration suite included, recorded as the run of
   record.
9. Close-out.

### Session 114: Walkthroughs for the open items

1. Register.
2. `docs/walkthroughs/`: three documents — settle a held eICR, remove an
   attachment, see a case shared read-only — each with its row in the index.
3. `Survsys.E2ETests`: the three Playwright tests named after them, and the
   harness clearing what they create.
4. Cross-provider verification.
5. Full test suite, the end-to-end walkthrough suite included, recorded as the
   run of record.
6. Close-out.

### Session 115: Release 1.0.0 (release: 1.0.0)

*The first release that claims parity: everything session 109's gap analysis
left open is closed by sessions 110 to 114, except NETSS, which waits on
CDC's answer.*

1. Before anything is published, the integration suite run whole in a
   container, with `SURVSYS_SQLSERVER` unset:
   `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj --blame-hang-timeout 5m`,
   and the whole end-to-end walkthrough suite. Both must be green. A red run
   stops the release: the step is reported blocked with the failures, and a
   repair session is planned before a new release session.

### Session 116: A kit for testing by hand

*Inserted after the 1.0.0 release, at the operator's request on 2026-10-24,
so that a person can perform the walkthroughs against a running Survsys
without assembling it from each document's "Before you start". Today the
clearing a walkthrough needs lives only in the end-to-end harness, the
fixtures sit in two test projects and must run in order, and the tiers need
a handful of settings to start with the intakes turned on. Nothing here
changes the application, so no release follows it.*

1. Register.
2. `Survsys.E2ETests` and a new `tools/uat/`: the harness's clearing and
   seeding — the tables it clears, the accounts it clears but the
   administrator, and its fixtures in order — moved into one place that both
   the harness and the kit call, so the two cannot drift. A reset command that
   drops and recreates a database of its own (`survsys_uat` by default, never
   `survsys_e2e`), brings it to this build's migration level and seeds it with
   everything the walkthroughs read, the intake fixtures included.
3. `tools/uat/`: a start command that creates the ELR and eCR drop
   directories and starts `Survsys.Api` and `Survsys.Web` over that database
   with every setting the walkthroughs name — migrations, the administrator
   from environment variables, both intakes' test directories and poll
   intervals, and the responsible agency — and prints the address to open
   and the drop directories.
4. `docs/walkthroughs/`: a testing-by-hand guide — the reset and start
   commands, and every walkthrough in an order a person can perform them in,
   grouped by area, each marked with whether it can be performed again over
   the same database and where a reset is due — and its row in the index.
   The README's "Before you start" pointer names the kit.
5. `Survsys.E2ETests`: the harness tests updated for the moved seeding, and
   a test that the reset leaves the same database the harness seeds.
6. Cross-provider verification.
7. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
8. Close-out.

### Session 117: A grouped navigation menu

*Inserted at the operator's request on 2026-10-28, before the walkthroughs
are performed by hand. Since session 73 the app bar has wrapped its buttons
onto further lines; with 38 screens an administrator now sees three rows of
undifferentiated links above every page. The links move into a side drawer
grouped by area, and the app bar keeps only the application's name, the
drawer's toggle, the signed-in person and Sign out.*

1. Register.
2. `Survsys.Web/Components/Layout/MainLayout.razor`: a `MudDrawer`, open by
   default and toggled from the app bar, holding a `MudNavMenu` whose
   `MudNavGroup`s are expanded by default — **Cases** (People, Events, New
   CMR, Work queue, Agency board, Outbreaks, Tasks, Saved views, Reports,
   Duplicate persons), **Intake** (Staged messages, Intake statistics, eCR,
   eCR quality), **Notifications** (Notifications, Export log,
   Reconciliation), **Reference data** (Conditions and the fifteen
   reference screens) and **Administration** (Users, Roles, Audit, Access
   audit). Each link keeps its `nav-…` identifier, its address and the
   privilege that shows it; a group none of whose links a person may see is
   not shown. The app bar returns to one line, and the layout comment says
   why. The privilege gating is unchanged; the API tier still enforces it.
3. `Survsys.E2ETests`: the tests that click a `#nav-…` link pass against the
   drawer unchanged where they can; any that cannot are given one harness
   helper rather than edited one by one. A test that the app bar holds no
   screen link, that each group shows only to a person holding a privilege
   one of its links needs, and that the drawer's toggle hides and shows it.
4. `docs/walkthroughs/` and `docs/user-guide/`: "In the application bar,
   press **X**" becomes "In the side menu, under **Group**, choose **X**",
   and the user guide describes the menu and its groups.
5. Cross-provider verification.
6. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
7. Close-out.

### Session 118: Release 1.0.1 (release: 1.0.1)

*A patch release: session 117 changes the navigation and nothing the
application does, so the build the walkthroughs are performed against by hand
carries the new menu.*

1. Before anything is published, the integration suite run whole in a
   container, with `SURVSYS_SQLSERVER` unset:
   `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj --blame-hang-timeout 5m`,
   and the whole end-to-end walkthrough suite. Both must be green. A red run
   stops the release: the step is reported blocked with the failures, and a
   repair session is planned before a new release session.

### Session 119: The side menu's group headings in the primary colour

*Inserted at the operator's request on 2026-10-28, after a preview they
approved. The group headings of session 117's side menu read as plain text
among the links under them; they take the application bar's purple with
white text, with a small gap above the first and between groups so that they
run neither into the bar nor into each other.*

1. Register.
2. `Survsys.Web/Components/Layout/MainLayout.razor`: each `MudNavGroup`'s
   heading takes the theme's primary colour as its background and the
   primary text colour for its words and its expand arrow, through the
   theme's own variables so that it follows the app bar; a small gap above
   the first heading and between groups. The links under the headings, and
   the highlight on the open screen's link, are unchanged.
3. `Survsys.E2ETests/MainLayoutTests.cs`: a test that a group heading's
   background is the app bar's.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 120: Release 1.0.2 (release: 1.0.2)

*A patch release: session 119 colours the side menu's group headings and
changes nothing the application does, so the build the walkthroughs are
performed against by hand carries the styled menu.*

1. Before anything is published, the integration suite run whole in a
   container, with `SURVSYS_SQLSERVER` unset:
   `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj --blame-hang-timeout 5m`,
   and the whole end-to-end walkthrough suite. Both must be green. A red run
   stops the release: the step is reported blocked with the failures, and a
   repair session is planned before a new release session.

### Session 121: Choosing related records on a case, not typing their ids

*Inserted at the operator's request on 2026-10-28, while performing the Add
a contact to a case walkthrough by hand. Its step 3 sends the investigator to
the People screen to note a database id for step 14 to type, and users will
not accept that. A sweep of every screen found the same on the case's tabs:
the Contacts entry form's Person and Report boxes, the Notes tab's Creator
and Exposure, the task entry's Assigned to and the Agency board's Report id,
and no screen at all that assigns a supplemental form to a case. The
search-then-create flow the operator asked for already exists, as the Add
contact panel on a recorded exposure's row; the entry form reuses it. Tables
that show a code or an id where the person chose a name show the name.*

1. Register.
2. `Survsys.Web/Components/ContactsTab.razor`: the entry form's contact
   **Person** and source **Person** become the person search the Add contact
   panel already uses (`PersonSearchPanel`), opened from the row, whose
   result rows choose the person and whose empty result creates one from the
   criteria; the row then shows the chosen person's name. The source's
   **Report** becomes a list of that person's reports, by disease and date.
   The Exposures table's contacts and sources show the person's name, the
   contact type's and the disposition's descriptions, and the source report's
   disease, rather than ids and codes. The tab's opening comment loses its
   stale lines on coded fields being plain boxes.
3. `Survsys.Web/Components/NotesTab.razor`: **Creator** goes; the API tier
   records the signed-in user as the note's author, as the attachment panel
   already does. **Exposure** becomes a list of this case's exposures by
   name and date.
4. A read-only user directory for assignment: an endpoint the task entry may
   read with `view_cases` that answers each active user's id and display
   name, and nothing else (`/api/users` stays `manage_users`).
   `Survsys.Web/Components/TasksPanel.razor`: **Assigned to** becomes a list
   over it, defaulting to the signed-in user.
5. `Survsys.Web/Components/Pages/AgencyQueue.razor`: the **Report id** boxes
   go; each row of the board carries **Assign** and **Share** actions that
   open the agency choice for that report.
6. A case's **Forms**: an **Assign form** list of the active forms for the
   case's condition not already assigned, calling the existing
   `POST /api/cases/{id}/forms/{formId}`, which no screen calls today.
7. `docs/walkthroughs/`: *Add a contact to a case* rewritten around the
   search — no step reads an id off the People screen, and the contact is
   found by name — and every other walkthrough that typed one of these ids
   (among them *Promote a contact to a case*, *Assign and accept a case*,
   *Work a task list*, *Close an investigation*, *See a case shared
   read-only*, *Route a case to another agency*) updated to choose instead;
   *Complete a supplemental form* gains assigning a form by hand. Their
   end-to-end tests follow them.
8. Cross-provider verification.
9. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
10. Close-out.

**Non-goals:** the Laboratory tab and the reference-data screens (session
122); providers (sessions 123 and 124); no change to how a contact is stored
or to the API tier's rules for it.

### Session 122: Choosing related records on the Laboratory tab and the reference screens

*Inserted with session 121, from the same sweep. The Laboratory tab takes its
lab, diagnostic and reporting facilities as typed ids and its test's LOINC as
a typed code; the outbreak editor takes its authors as typed user ids; a
notification guide's PHIN variables and a rule set's value set and its
conditions' fields are typed by name. Each is chosen from what the
application already holds.*

1. Register.
2. `Survsys.Web/Components/LaboratoryTab.razor`: **Lab facility**,
   **Diagnostic facility** and **Reporting facility** become searchable
   lists over the facilities, reusing the facility list the Vocabulary
   screen's lab codes already use (moved to a shared component if both need
   it). A test's **LOINC code** becomes a searchable picker over the master
   LOINC vocabulary, showing code and concept name, that still accepts a
   code the vocabulary does not hold. The specimens table shows the
   facilities' names and the specimen source's description. The stale
   comments in `Survsys.Data/Lab.cs` saying the facility table does not exist
   are corrected.
3. `Survsys.Web/Components/Pages/OutbreakEdit.razor`: both **Author (user
   id)** boxes go; the API tier records the signed-in user, as session 121
   does for notes.
4. `Survsys.Web/Components/Pages/GuideEdit.razor`: **PHIN variable** becomes
   a searchable list over the PHIN variables not already on the guide.
   `Survsys.Web/Components/Pages/RuleSetEdit.razor`: **Value set** becomes a
   list over the value sets, and each condition's **Field** a list over the
   fields the rule set names.
5. `docs/walkthroughs/`: *Enter a laboratory result*, *Open an outbreak and
   link cases*, *Configure a notification guide* and any other walkthrough
   that typed one of these updated to choose instead, with their end-to-end
   tests.
6. Cross-provider verification.
7. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
8. Close-out.

**Non-goals:** the **Provider** box, which has no table to choose from
(sessions 123 and 124); an agency's jurisdiction zip codes and a
parent/guardian's name stay free text.

### Session 123: The provider registry

*Inserted at the operator's request on 2026-10-28. A lab's ordering clinician
is a bare `provider_id` into a `provider` table Survsys does not have, so the
Laboratory tab can only offer a number box. The legacy application keeps a
**Providers** registry on its admin menu
(`../nedss/docs/functional/epitrax-core-workflows.md`, screen
`24-admin-providers`): last name, first name, phone, email and provider type,
over the legacy `provider` and `provider_type` tables. Survsys gets that
registry as reference data an administrator maintains, like the facilities.*

1. Register.
2. `Survsys.Data`: a `provider` table with the legacy columns (provider type,
   first, middle and last name, phone, email) and an active flag, by
   migration; the provider types as a `provider_type` code list on the
   existing Code lists mechanism rather than a table of their own, seeded
   with the legacy types the documents name.
3. `Survsys.Api` and `Survsys.Contracts`: list (paged, searched by last name,
   readable with `view_cases`), read, create and update under
   `manage_reference_data`, and CSV export and import previewed before
   saving, as the facilities have.
4. `Survsys.Web`: a **Providers** screen under **Reference data** in the side
   menu — the searchable table, an editor, and the CSV import and export —
   following the Facilities screen.
5. `docs/walkthroughs/`: a *Maintain a provider* walkthrough with its
   end-to-end test, and its row in the README index and the testing-by-hand
   guide.
6. Cross-provider verification.
7. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
8. Close-out.

**Non-goals:** the Laboratory tab is unchanged (session 124); no provider is
matched from an ELR message.

### Session 124: The ordering clinician chosen from the registry

*Inserted with session 123. With providers on file, a lab's **Provider** box
becomes the legacy Add Lab form's **Ordering Clinician** autocomplete
(`../nedss/docs/functional/epitrax-cmr-deep-editing.md`), with a way to add
a clinician who is not yet on file without leaving the case.*

1. Register.
2. The API tier refuses a lab whose `provider_id` names no provider, as it
   refuses an unknown person on a contact; existing labs whose id names no
   provider are reported by the migration's check rather than changed.
3. `Survsys.Web/Components/LaboratoryTab.razor`: **Provider** becomes
   **Ordering clinician**, a search over the registry by name as you type,
   with **New clinician** when nothing matches, which records the provider
   (by the session 123 endpoint, for a user holding the privilege it needs)
   and chooses them. The specimens table shows the clinician's name.
4. `docs/walkthroughs/enter-a-laboratory-result.md` and any other walkthrough
   naming the provider updated, with their end-to-end tests.
5. Cross-provider verification.
6. Full test suite, the end-to-end walkthrough suite included, recorded as
   the run of record.
7. Close-out.

**Non-goals:** the ELR intake's ordering provider (ORC-12, OBR-16) is not
matched to the registry; a case's Facility / Clinician panel is not added.

### Session 125: Release 1.1.0 (release: 1.1.0)

*A minor release: sessions 121 to 124 change how records are chosen across
the case's tabs and the reference screens, add the provider registry and its
screen, and add the ordering clinician's choice. New screens and new
endpoints, nothing removed, so the minor version.*

1. Before anything is published, the integration suite run whole in a
   container, with `SURVSYS_SQLSERVER` unset:
   `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj --blame-hang-timeout 5m`,
   and the whole end-to-end walkthrough suite. Both must be green. A red run
   stops the release: the step is reported blocked with the failures, and a
   repair session is planned before a new release session.
