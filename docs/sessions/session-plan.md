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

### Session 73: Release 0.10.0 (release: 0.10.0)

---

*Phase 10 — inbound ELR to parity → release 0.11.0. The EMSA half finished: the
libraries phase 5 built are wired into intake. Specified by
`EMSA_MESSAGE_PIPELINE.md`, `EMSA_RULES_ENGINE.md`, `EMSA_VOCABULARY_SYSTEM.md`,
`EMSA_PERSON_MATCHING.md`, `EMSA_EPITRAX_CONTRACT.md`, `HL7_FIELD_MAPPINGS.md`,
`MIRTH_REPLACEMENT_SPEC.md`, `emsa-ui-overview.md` and
`CT_SITE_REQUIREMENTS.md` §1.*

### Session 74: Parsed messages, the parser seam and MLLP

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

### Session 75: Vocabulary and rules at intake

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

### Session 76: The graylist, automatic assignment and the full queue

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

### Session 77: Labs from messages, and updating an existing case

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

### Session 78: Exceptions, reprocessing, notifications and statistics

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

### Session 79: Connecticut ELR conformance

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

### Session 80: Phase 10 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — find a message assigned
   automatically, update an existing case from a laboratory message, release
   a graylisted message, maintain a LOINC mapping.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 81: Release 0.11.0 (release: 0.11.0)

---

*Phase 11 — outbound CDC notification to parity → release 0.12.0. The NMI half
finished: the generator, mapper and transmitter phase 6 built are put to work.
Specified by `NMI_EXPORT_PIPELINE.md`, `NMI_RULESETS.md`, `NMI_XML_GENERATION.md`
and `nmi-ui-overview.md`. NETSS stays unbuilt until the operator records CDC's
answer in `docs/planning/netss-export-question.md`.*

### Session 82: The notification queue worker and the export log

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

### Session 83: Auto-queue on every change, queue actions and reconciliation

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

### Session 84: Guide, rule set and value set administration

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

### Session 85: Transport to CDC

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

### Session 86: Phase 11 walkthroughs

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

### Session 87: Release 0.12.0 (release: 0.12.0)

---

*Phase 12 — audit, access, classification and reporting → release 0.13.0.
Specified by `phase-1.6-cross-cutting-patterns.md`, `CLASSIFICATION_RULES.md`
and `phase-1.6-feature-matrix.md` §11–§14.*

### Session 88: Audit with its user and values, and its screens

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

### Session 89: Jurisdiction-scoped access and sign-in protection

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

### Session 90: Classification rules on save

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

### Session 91: IBIS, ad-hoc reporting and system configuration

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

### Session 92: Phase 12 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — review a case's audit history, run
   an ad-hoc report, send a case to IBIS, see a case classified on save.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 93: Release 0.13.0 (release: 0.13.0)

---

*Phase 13 — electronic case reporting → release 0.14.0. Specified by
`docs/planning/ecr-requirements.md`, `CT_SITE_REQUIREMENTS.md` §2 and the CT DPH
eCR documents in `../nedss/docs/specs/site-specs/`. Its items marked
**[confirm]** are confirmed with the APHL eCR service desk before the session
that depends on them, and a session that finds one wrong amends this phase.*

### Session 94: eICR and RR parsing and document intake

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

### Session 95: eCR validation and the eICR viewer

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

### Session 96: Reportability and condition mapping

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

### Session 97: Cases from eCR

1. Register.
2. `Survsys.Api`: a reportable eICR's patient matched by phase 9's matcher,
   linked to the open case on that person and condition — including one opened
   from ELR — or opening a new one, by the reviewer where the match is not
   confident.
3. `Survsys.Api`: the eICR's demographics, encounter, problems, medications as
   treatments, pregnancy, travel and occupation written to the case, its labs
   only for senders set to update lab data, each value recording the eICR id
   and version it came from; a later version updating what an earlier one
   wrote and nothing a user has edited.
4. `Survsys.IntegrationTests`: a new case, a linked case, and a second version.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 98: eCR senders and data quality

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

### Session 99: Phase 13 walkthroughs

1. Register.
2. `docs/walkthroughs/`: four documents — review an eICR and its reportability
   response, map a reportable condition, open a case from an eICR, link an
   eICR to an existing case.
3. `Survsys.E2ETests`: the four Playwright tests named after them.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

### Session 100: Release 0.14.0 (release: 0.14.0)

---

*Phase 14 — parity → release 1.0.0, the first release that claims parity.*

### Session 101: The parity check and the documentation for 1.0.0

1. Register.
2. `docs/planning/gap-analysis.md`: every gap marked closed with the session
   that closed it, or left open with the operator's decision; every row of
   `phase-1.6-feature-matrix.md` given its survsys status.
3. `Survsys.E2ETests`: the full walkthrough regression, every document read
   against its own test as session 57 did.
4. `docs/`: the user and technical guides brought to 1.0.0, with every "built
   but not wired in" statement gone or still true.
5. Cross-provider verification.
6. Full test suite, recorded as the run of record.
7. Close-out.

### Session 102: Release 1.0.0 (release: 1.0.0)
