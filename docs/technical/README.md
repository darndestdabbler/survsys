# Survsys technical guide

How Survsys is put together, how to run it, and how it is tested and deployed.

**This guide is a skeleton.** Every section it will have is declared below,
each naming the phase of [`../planning/solution-plan.md`](../planning/solution-plan.md)
that fills it. A section is written only once the application does what it
describes. The architecture *decisions* — and the reasoning behind them — live
in the solution plan and are not repeated here; this guide says how to work
with what those decisions produced.

| Section | Phase | State |
|---|---|---|
| [How Survsys is put together](#how-survsys-is-put-together) | 0 | **Written** |
| [Running it on a developer machine](#running-it-on-a-developer-machine) | 0 | **Written** |
| [The test suites](#the-test-suites) | 0 | **Written** |
| Deployment and configuration | 0, at the 0.1.0 release | Not yet written |
| The data model | 1 onward | Not yet written |
| The API | 1 onward | Not yet written |
| The inbound laboratory message pipeline | 5 | Not yet built |
| The CDC notification pipeline | 6 | Not yet built |

---

## How Survsys is put together

Two deployed tiers, each its own IIS site, over four libraries and three test
projects. .NET 10, pinned by `global.json`.

| Project | What it is |
|---|---|
| `Survsys.Web` | The application tier: Blazor Server with MudBlazor. It reads everything through the API tier over HTTP and **reaches no database** — it references neither `Survsys.Data` nor EF Core, which a test asserts rather than a reviewer checks. |
| `Survsys.Api` | The API tier: minimal APIs. **The only tier permitted to touch the database.** Its endpoints take and return `Survsys.Contracts` types, never entities. |
| `Survsys.Contracts` | The request and response types both tiers share. No behaviour, no dependencies. |
| `Survsys.Data` | EF Core: the entities, the `DbContext` and the migrations. Referenced by `Survsys.Api` alone. |
| `Survsys.UnitTests` | The run of record. Covers the whole repository, and holds the architecture tests and the walkthrough-contract test. |
| `Survsys.IntegrationTests` | `Survsys.Data` and `Survsys.Api` against a real SQL Server. |
| `Survsys.E2ETests` | The walkthroughs, through a browser. It launches both tiers as processes and references neither. |

The database is SQL Server, and `Survsys.Api` owns its schema. On startup the
API tier **refuses to serve a database below its migration level** rather than
migrating one silently; started with `Survsys__ApplyMigrations=true` it applies
its migrations first and then serves, which is how an empty database is brought
up to level and how the end-to-end harness prepares its own.

## Running it on a developer machine

You need .NET 10 and a reachable SQL Server. Connections use integrated
security, so there is no password anywhere in configuration.

1. **Bring up the database.** Start the API tier once with the migration switch
   on:

   ```
   Survsys__ApplyMigrations=true dotnet run --project Survsys.Api --urls http://localhost:5100
   ```

   On PowerShell, set `$env:Survsys__ApplyMigrations = 'true'` first. Afterwards
   the plain command below is enough, until a migration is added.

2. **Run the API tier:**

   ```
   dotnet run --project Survsys.Api --urls http://localhost:5100
   ```

   Its connection string is `ConnectionStrings:Survsys` in
   `Survsys.Api/appsettings.Development.json`.

3. **Run the application tier:**

   ```
   dotnet run --project Survsys.Web --urls http://localhost:5000
   ```

   `Survsys.Web/appsettings.Development.json` already points
   `Survsys:ApiBaseAddress` at `http://localhost:5100`. Open
   <http://localhost:5000>.

Both tiers answer `GET /health/ready` with 200 once they are serving, which is
what the end-to-end harness waits on and what a load balancer should probe.

**Seed data.** `Survsys.IntegrationTests/Fixtures/conditions.sql` holds seven
real condition rows taken from the legacy EpiTrax seed. Run it against your
database to have something on the screen; the integration and end-to-end suites
seed it themselves.

## The test suites

Three suites, declared in `dabbler.yaml` with their own hang limits. Neither of
the last two skips when what it needs is missing: a suite that silently skips
reads as a green run.

| Suite | Command | Needs |
|---|---|---|
| `unit` — the run of record | `dotnet test Survsys.UnitTests/Survsys.UnitTests.csproj` | nothing |
| `integration` | `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj` | a reachable SQL Server |
| `e2e` — the walkthroughs | `dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj` | a reachable SQL Server; Playwright's chromium, which the harness installs if it is missing |

`SURVSYS_E2E_SQLSERVER` redirects the end-to-end suite at another SQL Server,
as a whole connection string, so a CI runner can change the authentication as
well as the host. The end-to-end suite records video of every test it runs into
`Survsys.E2ETests/bin/<configuration>/net10.0/videos`.

A source file's tests live in the test file named after it. The exceptions are
the structural tests, which are named after the rule they keep rather than
after a source file: `ArchitectureTests` (the tier fences) and
`WalkthroughContractTests` (every walkthrough document has the test named after
it, and the index links it).

---

## Deployment and configuration

*Not yet written — at the 0.1.0 release.* `dabbler.yaml` already declares the
handoff: an IIS site package for each tier and the database's idempotent SQL
script. The release session is what produces them, and what this section will
describe installing.

## The data model

*Not yet written — phase 1 onward.* The entities, the migration history and the
mapping from the legacy EpiTrax schema, grown as each phase ports its tables.

## The API

*Not yet written — phase 1 onward.* The endpoints, their contracts, paging and
error shapes, grown as each phase adds its resources.

## The inbound laboratory message pipeline

*Not yet built — phase 5.* HL7 intake, person matching, the rules engine and
the staging queue; Mirth Connect is dropped rather than carried over.

## The CDC notification pipeline

*Not yet built — phase 6.* Guides, rulesets, HL7 generation and the export log.
