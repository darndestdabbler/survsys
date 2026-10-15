# Project work plan — sessions

**Written by `dabbler` as a fold of `activity-log.json`.**
Hand edits are overwritten by the next append. The record is the log;
this page is one view of it.

---

## Sessions

The numbered sessions are declared from `session-plan.md`; each one's task is what its own plan step declared.

| # | Session | Releasable | Declared |
| ---: | --- | --- | --- |
| 1 | Author or import the solution plan | no | 2026-10-15 |
| 2 | Challenge the plan, then break it into numbered sessions | no | 2026-10-15 |
| 3 | Skeleton — the solution, its seven projects and the architecture tests | no | 2026-10-15 |
| 4 | Packaging — write the packaging block and nothing else | no | 2026-10-15 |
| 5 | First vertical slice — one real table through the API | no | 2026-10-15 |
| 6 | First MudBlazor screen and the Playwright harness | no | 2026-10-15 |
| 7 | The first walkthrough and the documentation it lives in | no | 2026-10-15 |
| 8 | Release 0.1.0 (release: 0.1.0) | — | not declared |
| 9 | Person demographics — schema and endpoints | — | not declared |
| 10 | Person search and case morbidity report creation | — | not declared |
| 11 | The case morbidity report shell and its Demographics tab | — | not declared |
| 12 | Phase 1 walkthroughs | — | not declared |
| 13 | Release 0.2.0 (release: 0.2.0) | — | not declared |
| 14 | Clinical and laboratory data — schema and endpoints | — | not declared |
| 15 | Clinical and laboratory screens | — | not declared |
| 16 | Case classification, status and MMWR week | — | not declared |
| 17 | Workflow state machine, work queues and agency sharing | — | not declared |
| 18 | Phase 2 walkthroughs | — | not declared |
| 19 | Release 0.3.0 (release: 0.3.0) | — | not declared |
| 20 | The Investigation and Administrative tabs | — | not declared |
| 21 | Contacts and exposures | — | not declared |
| 22 | Notes and tasks | — | not declared |
| 23 | File attachments | — | not declared |
| 24 | Phase 3 walkthroughs | — | not declared |
| 25 | Release 0.4.0 (release: 0.4.0) | — | not declared |
| 26 | Agencies, jurisdictions, facilities and conditions | — | not declared |
| 27 | Core element configuration | — | not declared |
| 28 | Users, roles, privileges and access audit | — | not declared |
| 29 | Saved views and custom exports | — | not declared |
| 30 | Phase 4 walkthroughs | — | not declared |
| 31 | Release 0.5.0 (release: 0.5.0) | — | not declared |
| 32 | `Survsys.Messaging` and HL7 intake parsing | — | not declared |
| 33 | Vocabulary matching | — | not declared |
| 34 | Duplicate detection and person matching | — | not declared |
| 35 | The message queue and the intake service | — | not declared |
| 36 | ELR review screens and promotion to a case | — | not declared |
| 37 | Phase 5 walkthroughs | — | not declared |
| 38 | Release 0.6.0 (release: 0.6.0) | — | not declared |
| 39 | Notification guides and PHIN variable mapping | — | not declared |
| 40 | The seven rule set types and repeating groups | — | not declared |
| 41 | Message generation and direct transmission | — | not declared |
| 42 | Auto-queue, reconciliation and the export log | — | not declared |
| 43 | Notification screens | — | not declared |
| 44 | Phase 6 walkthroughs | — | not declared |
| 45 | Release 0.7.0 (release: 0.7.0) | — | not declared |
| 46 | Outbreak management | — | not declared |
| 47 | The dynamic form builder | — | not declared |
| 48 | Treatments and vaccines | — | not declared |
| 49 | Audit by EF Core interceptors | — | not declared |
| 50 | IBIS integration and ad-hoc reporting | — | not declared |
| 51 | Phase 7 walkthroughs and the full walkthrough regression | — | not declared |
| 52 | Release 1.0.0 (release: 1.0.0) | — | not declared |

### Session 1 — Author or import the solution plan

**Releasable: no.**

Capture the operator's brief for survsys and turn it into the solution plan the rest of this repository is built from. The brief -- port the three legacy disease-surveillance systems under ../nedss (EpiTrax, Java EE case management; EMSA, PHP inbound ELR staging; NMI, PHP outbound CDC notification) into one .NET 10 / MudBlazor / Entity Framework Core / SQL Server application whose API tier mediates every database call -- is written to docs/planning/brief.md as the operator gave it, including the constraints they named: keep it as simple as possible, unit plus integration plus Playwright end-to-end tests, build incrementally so that after the first few sessions a person can see and interact with real database data, ship walkthrough documentation at every release, and keep required operator input to a minimum. docs/planning/solution-plan.md is then written: an objective a reader can act on; the Production split the operator chose, which is the default -- a MudBlazor application tier that calls an API tier, and only the API tier talks to SQL Server; the Handoff artifacts for each separately running part with the standard command for its form, an IIS site package by dotnet publish for the application and API tiers and an idempotent SQL migration script by dotnet ef migrations script --idempotent for the database; the modules, each a .csproj listed in the root solution file and reaching a sibling only by <ProjectReference>, with what each is responsible for, the contract it exposes as preconditions, guarantees and failure mode, the dependency direction, the reason for each cut and the cuts left deferred for session 2 to decide; and the phases with their key deliverables, each scoped to a handful of focused AI sessions and ordered so the earliest put a working MudBlazor screen over real SQL Server data in front of the operator. The plan also fixes the walkthrough contract the operator called non-negotiable: every release ships step-by-step walkthroughs organised around user stories inferred from the legacy code and documentation, and each walkthrough is driven by a Playwright test that performs exactly its written steps and records the run on video, so no walkthrough can ship that a person cannot complete as written.

### Session 2 — Challenge the plan, then break it into numbered sessions

**Releasable: no.**

Challenge the cuts in docs/planning/solution-plan.md and then break that plan into the numbered sessions the rest of this repository runs. Each of the four modules and three test projects is held to three questions -- whether it should exist at all, whether its stated contract is a promise something can mechanically prove, and whether the dependency direction hides the decisions most likely to change -- and the answers are recorded in the plan as a new 'Module challenge' section: Survsys.Contracts, Survsys.Data, Survsys.Api and Survsys.Web all stand, with two contract promises made provable rather than aspirational (the API tier asserting at startup that the database is at or above the migration level its assembly carries, and a second architecture test asserting no endpoint signature in Survsys.Api names a Survsys.Data entity type, since Survsys.Web is already fenced off by the first), and with the mapping between entities and wire shapes recorded as living in Survsys.Api because Survsys.Data may not reference Survsys.Contracts. Each of the four deferred cuts is then decided in the plan rather than deferred again: a pure Survsys.Messaging module is adopted but created by the session that first needs it in the inbound-ELR phase rather than standing empty through five phases; inbound ELR intake runs as an IHostedService inside the API tier, adding no third handoff artifact; no pure domain module is cut out of Survsys.Data, because its rules are already pure classes the unit suite tests without a database and Survsys.Messaging gives the genuinely pure logic a home; and no Dabbler.* package is adopted now -- every candidate is at 0.1.0-preview.1, Dabbler.Testing.Blazor is refused outright because the walkthrough harness is the operator's non-negotiable and must not be hostage to a prerelease dependency (a readiness endpoint plus Playwright's own web-first assertions replace it), and Dabbler.Components and Dabbler.Api.Querying are left to a session that can show the package removes more code than it adds. The numbered sessions are then appended to docs/sessions/session-plan.md, one '### Session <N>: <title>' heading each with its steps as a top-level ordered list that registers first and ends on cross-provider verification, the complete suite against the verified tree, and close-out, at most about three work steps between them, ordered so a module lands before the modules that depend on it: session 3 is the skeleton, writing Survsys.sln, global.json pinning .NET 10, all seven projects with their references, the testing.suites block in dabbler.yaml with expensive: true and dotnet test --blame-hang-timeout 5m, and the architecture tests; session 4 is the packaging session and does nothing else; then the first vertical slice over one real table, the first MudBlazor screen with the Playwright video harness, and the first walkthrough with its test, closing phase 0 on a release; and thereafter the phases of the plan -- persons and case records, clinical and laboratory and workflow, investigation and contacts and notes and tasks, reference data and administration and reporting, inbound ELR processing, outbound CDC notification, and outbreaks and forms and treatments and audit -- each phase's walkthroughs gathered into a session of their own before the release session that ends the phase, every release a session with no steps headed '(release: <version>)' at the version this plan proposes for the operator to approve: 0.1.0, 0.2.0, 0.3.0, 0.4.0, 0.5.0, 0.6.0, 0.7.0 and 1.0.0.

### Session 3 — Skeleton — the solution, its seven projects and the architecture tests

**Releasable: no.**

Lay down the skeleton of the Survsys solution and nothing else. Write `global.json` at the repository root pinning the .NET 10 SDK, because this host also carries SDK 11.0.100-preview.2 which is newer and would otherwise be selected. Write `Survsys.sln` at the root listing all seven projects -- `Survsys.Contracts`, `Survsys.Data`, `Survsys.Api`, `Survsys.Web`, `Survsys.UnitTests`, `Survsys.IntegrationTests`, `Survsys.E2ETests` -- each a `.csproj` carrying its target framework, its `<ProjectReference>`s and nothing more than the packages it cannot exist without, in exactly the reference graph the solution plan fences: `Survsys.Contracts` referencing no sibling; `Survsys.Data` referencing no sibling and carrying `Microsoft.EntityFrameworkCore.SqlServer`; `Survsys.Api` referencing `Survsys.Data` and `Survsys.Contracts`; `Survsys.Web` referencing `Survsys.Contracts` alone; `Survsys.UnitTests` referencing every module, because it is the project that reads their reference graphs; `Survsys.IntegrationTests` referencing `Survsys.Api` and `Survsys.Data`; and `Survsys.E2ETests` referencing `Survsys.Contracts` only, since it launches the two tiers as processes. Declare `testing.suites` in `dabbler.yaml` -- the unit, integration and end-to-end suites, each with its `covers` and `test_roots`, `expensive: true` on the unit suite, which is this repository's run of record, and `--blame-hang-timeout 5m` in every command so every later run inherits the hang limit. Finally write `Survsys.UnitTests/ArchitectureTests.cs`, the two architecture tests the solution plan makes load-bearing: that the `Survsys.Web` assembly references neither `Survsys.Data` nor `Microsoft.EntityFrameworkCore`, and that no endpoint signature in `Survsys.Api` -- parameter or return type, unwrapped through `Task<>`, `ActionResult<>` and collections -- names a type from the `Survsys.Data` assembly. No feature code in any project.

### Session 4 — Packaging — write the packaging block and nothing else

**Releasable: no.**

Write the `packaging:` block in `dabbler.yaml`, replacing the commented-out example left by the bootstrap, and change nothing else in the repository. `pack` is one argv that produces exactly the three handoff artifacts the solution plan names, written into `{output}`: an IIS site package for each tier (`dotnet publish` of `Survsys.Web` and `Survsys.Api`, each compressed to a single `.zip` so one tier is one pushable artifact rather than several hundred loose files) and the database's idempotent SQL script (`dotnet ef migrations script --idempotent`). `push` is argv naming the feed and naming its credential by environment variable (`secret: DABBLER_FEED_PAT`) rather than holding its value, uploading each artifact over HTTPS. The block is rehearsed with `dabbler packaging --dry-run`, which exits 0 once the declaration loads.

### Session 5 — First vertical slice — one real table through the API

**Releasable: no.**

Session 5 puts one real table through the API tier, end to end and no further. Survsys.Data gains the Condition entity, the SurvsysDbContext, its entity configuration and the initial EF Core migration for the conditions table -- the thinnest real table in the legacy catalogue that a screen can show, carrying the columns of EpiTrax's own condition table (name, the three lead-in prose columns, active, cdc_code, condition_group_id). Survsys.Api gains two things the module challenge turned from promises into assertions: the startup assertion that reads the pending-migration list and refuses to serve while it is non-empty, so a database below the assembly's migration level is one loud startup failure rather than a 500 on whichever request first touches a missing column; and GET /api/conditions, paged, returning the Survsys.Contracts types this session adds -- a paged-result record and a condition response -- with Problem Details on every failure and the entity-to-wire mapping living in the API tier, because the forbidden Survsys.Data -> Survsys.Contracts reference leaves it nowhere else. Survsys.IntegrationTests gains the suite that runs against a real SQL Server, failing loudly when none is reachable rather than skipping, applying the migration and reading the condition rows a fixture seeds. One correction to this session's section of the session plan, made in the open rather than quietly: it names ../nedss/docs/db-snapshots/ as the fixture source, and those snapshots carry no condition rows at all -- every one of them dumps person, person_condition, facility and their neighbours, and none dumps condition. The 186 real condition rows live in the legacy Flyway seed ../nedss/legacy/nedss-jpa/src/main/resources/db/migration/V0003__init.sql, so the fixture is a small set of rows taken from there and committed into this repository with that provenance cited in the file, rather than read out of a sibling working copy at test time.

**Amended after acceptance:**

- 2026-10-15 — step 'data-initial-migration': its files: EF Core names a migration file after the timestamp it generates it at, which a plan written before the work cannot state (claude-code (anthropic, claude-opus-5))
- 2026-10-15 — step 'integration-suite-conditions': its files: The suite found a failure that was not Problem Details: a query value that will not parse into its parameter never reaches an endpoint, so the framework sets a bodiless 400 the exception handler never sees. One line in Program.cs (app.UseStatusCodePages) keeps the solution plan's promise that errors are Problem Details; the right answer was to fix the tier rather than to drop the test. (claude-code (anthropic, claude-opus-5))

### Session 6 — First MudBlazor screen and the Playwright harness

**Releasable: no.**

Put the first MudBlazor screen in front of the operator and build the walkthrough harness it will be driven through. Survsys.Web becomes a real Blazor Server host with MudBlazor, a typed conditions client over GET /api/conditions whose failures become a message on the screen rather than an unhandled circuit exception, and a conditions list screen carrying stable element identifiers a walkthrough's wording can point at. Each tier gains a readiness endpoint at /health/ready for the harness to wait on, and Survsys.Api gains an opt-in configuration switch that applies its migrations at startup instead of refusing to serve, so the harness can bring an empty database to migration level through the only tier allowed to touch it. Survsys.E2ETests then becomes the harness the walkthrough contract rests on: Playwright with video recording on for every test, both tiers built and launched as processes on free ports under a startup timeout, readiness waited on before the first click, the condition fixture seeded, and one smoke test proving the chain end to end. No prerelease package goes into it.

### Session 7 — The first walkthrough and the documentation it lives in

**Releasable: no.**

Write the first walkthrough and the documentation it lives in. docs/walkthroughs/review-the-conditions-catalogue.md becomes the repository's first walkthrough document: one user story inferred from the legacy EpiTrax administration Conditions screen and NMI's /conditions screen under ../nedss/docs/ -- a surveillance administrator reviewing the condition catalogue to see which conditions may be reported and which carry a CDC code -- written as numbered steps the reader performs, naming every control by the words the Survsys conditions screen actually shows. Survsys.E2ETests then gains the one Playwright test named after that document, ReviewTheConditionsCatalogueTests, which performs those steps in their written order, asserts the outcome the document promises, names the document and the step number in any failure, and records video of the run through the harness base class session 6 built; the placeholder screen test in SurvsysHarnessTests, whose own name said it was standing in until this walkthrough existed, goes with it. docs/ then gains the shape later phases add to: a map of the documentation, the walkthrough index, and the user and technical documentation skeletons -- with Survsys.UnitTests/WalkthroughContractTests making the contract mechanical, so that every walkthrough document from here on must have the test named after it and a line in the index, and no later session can add a document that nothing performs.
