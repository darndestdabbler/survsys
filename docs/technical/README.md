# Survsys technical guide

How Survsys is put together, how to run it, and how it is tested and deployed,
as of release 1.0.0. The architecture *decisions* — and the reasoning behind
them — live in [`../planning/solution-plan.md`](../planning/solution-plan.md)
and are not repeated here; this guide says how to work with what those
decisions produced.

| Section | Phase |
|---|---|
| [How Survsys is put together](#how-survsys-is-put-together) | 0 |
| [Running it on a developer machine](#running-it-on-a-developer-machine) | 0 |
| [The test suites](#the-test-suites) | 0 |
| [Deployment and configuration](#deployment-and-configuration) | 0 onward |
| [The data model](#the-data-model) | 1 onward |
| [The API](#the-api) | 1 onward |
| [The inbound laboratory message pipeline](#the-inbound-laboratory-message-pipeline) | 5 |
| [The CDC notification pipeline](#the-cdc-notification-pipeline) | 6 |

---

## How Survsys is put together

Two deployed tiers, each its own IIS site, over three libraries and three test
projects, all in `Survsys.sln`. .NET 10, pinned by `global.json`.

| Project | What it is |
|---|---|
| `Survsys.Web` | The application tier: Blazor Server with MudBlazor. It reads everything through the API tier over HTTP and **reaches no database** — it references `Survsys.Contracts` alone, which a test asserts rather than a reviewer checks. |
| `Survsys.Api` | The API tier: minimal APIs. **The only tier permitted to touch the database.** Its endpoints take and return `Survsys.Contracts` types, never entities. It references `Survsys.Data`, `Survsys.Contracts` and `Survsys.Messaging`. |
| `Survsys.Contracts` | The request and response types both tiers share. No behaviour, no dependencies. |
| `Survsys.Data` | EF Core on SQL Server: the entities, `SurvsysDbContext`, the migrations, the audit interceptor, and the pure rules the data carries (workflow, case status, MMWR week, reconciliation cutoff, message review states). Referenced by `Survsys.Api` alone. |
| `Survsys.Messaging` | HL7 v2.5.1 and notification logic, pure and in memory: the ORU^R01 parser and generator (on nHapi), person matching, the graylist, vocabulary matching, the PHIN variable mapper, repeating groups and the ruleset formatters. It references `Survsys.Contracts` and neither `Survsys.Data` nor EF Core, which a test asserts. |
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
security, so there is no database password anywhere in configuration.

1. **Bring up the database, and the first user.** Start the API tier once with
   the migration switch on and an administrator named:

   ```
   Survsys__ApplyMigrations=true \
   Survsys__Administrator__UserName=admin \
   Survsys__Administrator__Password='choose-one' \
   dotnet run --project Survsys.Api --urls http://localhost:5100
   ```

   On PowerShell, set each as `$env:Survsys__ApplyMigrations = 'true'` and so
   on first. Afterwards the plain command below is enough, until a migration
   is added.

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
   <http://localhost:5000> and sign in as the administrator.

Both tiers answer `GET /health/ready` with 200 once they are serving — the API
tier only after its migration gate — which is what the end-to-end harness
waits on and what a load balancer should probe.

**Seed data.** The fixtures under `Survsys.IntegrationTests/Fixtures` give a
fresh database something to show. Run `conditions.sql` and then `persons.sql`
for the seven legacy conditions and seven people the walkthroughs use; the
agencies, roles, privileges, report sections and vaccines are seeded by the
migrations themselves.

| Fixture | What it holds | Used by |
|---|---|---|
| `conditions.sql` | Seven condition rows from the legacy EpiTrax seed. | integration, end-to-end |
| `persons.sql` | Seven people and the rows hung off them. | integration, end-to-end |
| `person-conditions.sql` | One case morbidity report. | integration |
| `staged-messages.sql` | Four staged inbound messages, queued and exceptions. | end-to-end |
| `nmi-log.sql` | One case and its two exports in the CDC export log. | end-to-end |
| `Hl7/*.hl7` | An inbound ORU^R01 and an outbound notification. | integration |

## The test suites

Three suites, declared in `dabbler.yaml`, each with a five-minute hang limit.
Neither of the last two skips when what it needs is missing: a suite that
silently skips reads as a green run.

| Suite | Command | Needs |
|---|---|---|
| `unit` — the run of record | `dotnet test Survsys.UnitTests/Survsys.UnitTests.csproj` | nothing |
| `integration` | `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj` | a reachable SQL Server |
| `e2e` — the walkthroughs | `dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj` | a reachable SQL Server; Playwright's chromium, which the harness installs if it is missing |

The integration suite drops and re-migrates its own database,
`survsys_integration`; `SURVSYS_SQLSERVER` replaces its connection string
whole. The end-to-end suite uses `survsys_e2e`, and `SURVSYS_E2E_SQLSERVER`
replaces that one, so a CI runner can change the authentication as well as the
host. The end-to-end harness starts both built tiers on free loopback ports,
migrates, creates a walkthrough-only administrator, empties every table a
walkthrough fills, reseeds the fixtures, and waits on `health/ready` for up to
two minutes. It records video of every test into
`Survsys.E2ETests/bin/<configuration>/net10.0/videos`. The whole suite is the
full walkthrough regression a release runs.

A source file's tests live in the test file named after it. The exceptions are
the structural tests, which are named after the rule they keep rather than
after a source file: `ArchitectureTests` (the tier fences) and
`WalkthroughContractTests` (every walkthrough document has the test named after
it, and the index links it).

---

## Deployment and configuration

**The handoff.** A release session produces three artifacts, declared by the
`packaging` block of `dabbler.yaml`, and copies them into the handoff folder:

| Artifact | What it is |
|---|---|
| `Survsys.Web.zip` | The application tier, `dotnet publish -c Release`, zipped. |
| `Survsys.Api.zip` | The API tier, published the same way. |
| `Survsys.Database.sql` | The database's idempotent migration script, from `dotnet ef migrations script --idempotent`. |

**Installing it.**

1. **The database.** Run `Survsys.Database.sql` against the target database.
   It is idempotent: it applies only the migrations the database does not have
   yet, so the same script brings a new database up and an existing one
   forward. Seed `conditions.sql` and the people you need, or load real data.
2. **The API tier.** Unzip `Survsys.Api.zip` into its own IIS site, with the
   ASP.NET Core Hosting Bundle for .NET 10 installed. Publishing generates the
   default `web.config`, which hosts in process. The site's application pool
   identity is the account integrated security connects as, so grant it the
   database. Give it its settings (below), and set the administrator's two
   settings for its first start.
3. **The application tier.** Unzip `Survsys.Web.zip` into a second IIS site,
   and set `Survsys:ApiBaseAddress` to the API site's address. It is the
   address users open.
4. Probe both sites' `/health/ready`.

Settings are read the ASP.NET Core way — `appsettings.json`, then
`appsettings.<Environment>.json`, then environment variables, where `:`
becomes `__`.

**What `Survsys.Api` reads:**

| Setting | Meaning | Default |
|---|---|---|
| `ConnectionStrings:Survsys` | The SQL Server connection, with integrated security. | none; the tier refuses to start without it |
| `Survsys:ApplyMigrations` | `true` applies pending migrations at startup. Otherwise the tier refuses to start on a database below its migration level. | `false` |
| `Survsys:Administrator:UserName`, `Survsys:Administrator:Password` | On startup, creates this user with the Administrator role if no such user exists. Nothing is created unless both are set. | unset |
| `ElrIntake:DropDirectory` | The folder inbound HL7 files are dropped into. Empty turns intake off. | empty |
| `ElrIntake:PollIntervalSeconds` | How often the drop folder is read. | `120` |
| `ElrIntake:MessageTimeoutSeconds` | How long one file may take. | `30` |
| `CdcTransmission:Endpoint`, `CdcTransmission:TimeoutSeconds` | Where an HL7 notification would be posted, and the timeout. Nothing in 1.0.0 posts one; see [the CDC notification pipeline](#the-cdc-notification-pipeline). | empty, `30` |
| `IbisTransmission:Endpoint`, `IbisTransmission:TimeoutSeconds` | Where a case is posted to IBIS as JSON, and the timeout. Empty turns IBIS off. Not in `appsettings.json`; set it where the tier runs. | unset, `30` |

**What `Survsys.Web` reads:** `Survsys:ApiBaseAddress`, the API tier's
address. It refuses to start without it.

Sign-in is not configurable: the API tier issues an opaque bearer token on
`POST /api/sign-in`, valid for eight hours, and the application tier keeps it
in its own sign-in cookie, also eight hours and not sliding. Both depend on
ASP.NET Core's default data-protection key ring, which Survsys does not
configure; a deployment that runs either tier on more than one server, or that
must keep users signed in across a recycle, should persist the key ring
itself. Neither tier redirects to HTTPS or reads forwarded headers; terminate
TLS at IIS.

## The data model

The schema is `Survsys.Data`'s, built by 26 migrations, in order:
`InitialCreate`, `Persons`, `PersonConditions`, `ClinicalAndLaboratory`,
`CaseClassification`, `Workflow`, `InvestigationAndAdministrative`,
`ContactsAndExposures`, `NotesAndTasks`, `Attachments`,
`AgenciesAndFacilities`, `CoreElements`, `UsersAndRoles`, `SavedViews`,
`VocabularyMatching`, `Graylist`, `MessageQueue`,
`NotificationGuidesAndPhinVariables`, `RuleSets`,
`NotificationQueueAndExportLog`, `Outbreaks`, `Forms`, `TreatmentsAndVaccines`,
`AuditEntries`, `IbisExport` and `RunReportsPrivilege`.

| Area | Tables |
|---|---|
| People | `persons`, `person_addresses`, `person_aliases`, `person_emails`, `person_races`, `person_telephones`, `parent_guardians` |
| Cases | `person_conditions` (the case morbidity report), `person_condition_labs`, `person_condition_shares`, `condition_detail`, `notes`, `tasks`, `attachments`, `attachment_files`, `saved_views` |
| Laboratory | `labs`, `lab_tests`, `lab_test_results`, `lab_resist_tests` |
| Exposures | `exposures`, `exposure_places`, `exposure_contacts`, `exposure_sources` |
| Treatments and vaccines | `treatments`, `condition_treatments`, `vaccines`, `person_vaccines` |
| Reference data | `conditions`, `agencies`, `facilities`, `zip_code_agencies`, `core_elements`, `core_element_overrides`, `report_sections` |
| Supplemental forms | `forms`, `form_sections`, `form_questions`, `form_question_options`, `form_assignments`, `form_answers` |
| Outbreaks | `outbreaks`, `outbreak_access`, `outbreak_updates`, `outbreak_report_sections` |
| Security and audit | `users`, `roles`, `user_roles`, `role_privileges`, `access_records`, `audit_entries` |
| Inbound messages | `system_messages`, `graylist_spool`, `vocab_master_loinc`, `vocab_child_loinc`, `vocab_master_organism` |
| CDC notification | `guide`, `phin_var`, `guide_phin_var`, `guide_link`, `rule_set` and its `rule_set_*` tables, `nmi_export_queue`, `nmi_log` |

**The mapping from EpiTrax.** Columns keep the legacy snake_case names, which
is what a later data migration reads across. EpiTrax's tables are pluralised
(`person` became `persons`); the EMSA and NMI tables keep their legacy names.
Strings the legacy schema left unbounded are bounded, so they can be indexed.

**Audit** is an EF Core `SaveChanges` interceptor, `AuditInterceptor`, in
place of the legacy PostgreSQL triggers. It is on every context. For each row a
save inserts, updates or deletes it writes one `audit_entries` row — the
table, the row's key values, the operation and the time in UTC — in the same
transaction, after the save, so a generated key is the real one; a save that
fails or is rolled back writes nothing. It records neither the values before
and after nor the user. Who opened which report is recorded separately, in
`access_records`, each time a report is read.

## The API

Every endpoint is under `/api`, mapped by a `*Endpoints.cs` file in
`Survsys.Api` named after its resource: `persons`, `person-conditions` (the
case, with its `administrative`, `clinical`, `investigation`, `labs`,
`exposures`, `contacts/import`, `notes`, `attachments`, `tasks`, `status`,
`workflow`, `assignment`, `shares`, `hidden-elements`, `notification-queue` and
`ibis` beneath it), `labs`, `exposures`, `notes`, `attachments`, `tasks`,
`work-queue`, `saved-views`, `staged-messages`, `notification-queue`,
`export-log`, `outbreaks`, `forms` and `cases/{id}/forms`, `treatments`,
`vaccines` and `cases/{id}/vaccines`, `report-sections`, `reports`,
`conditions`, `agencies`, `facilities`, `core-elements`, `users`, `roles`,
`privileges`, `access-audit`, `sign-in` and `me`. There is no OpenAPI
document; the endpoints' own summaries and the `Survsys.Contracts` types are
the reference.

**Authentication.** `POST /api/sign-in` checks the password (hashed with
ASP.NET Core Identity's hasher) and returns a bearer token carrying the user's
privileges as claims. The application tier's `ApiTokenHandler` puts it on
every call it makes.

**Privileges.** One authorization policy, `PrivilegeRules`, decides every
request by its method and path: users, roles, privileges and the access audit
need `manage_users`; conditions, agencies, facilities and core elements are
readable by anyone signed in and need `manage_reference_data` to change;
saved views need `view_cases`; reports need `run_reports`; and everything else
needs `view_cases` to read and `edit_cases` to write. Not signed in is a 401;
signed in without the privilege is a 403.

**Paging.** A list answers `PagedResult<T>` — `Items`, `Page`, `PageSize`,
`TotalCount` — for `?page=` (from 1) and `?pageSize=` (default 50, at most
200); a page that cannot be returned is a 400 saying why.

**Errors** are RFC 7807 problem details, each with a `correlationId` and never
an exception's text. A refused request is a 400 whose `detail` is one plain
sentence saying what was wrong; a missing resource is a 404, a request the
resource's state refuses is a 409, and an IBIS failure is a 502, or a 504 when
it timed out.

**IBIS.** `POST /api/person-conditions/{id}/ibis` posts the case to IBIS under
the configured timeout, and marks it sent only when IBIS accepts it. Nothing in
the application tier calls it yet.

## The inbound laboratory message pipeline

Messages enter through a **file drop**: `ElrIntakeService`, a background
service in the API tier, reads the top level of `ElrIntake:DropDirectory`
every poll interval. Each file is parsed as an HL7 v2.5.1 ORU^R01 by
`OruR01Parser`; one that parses is staged in `system_messages` in the `entry`
state, and one that does not is staged as an `exception` with what was wrong.
The file then moves into `.processed`, or into `.failed` when it could not be
read or saved. Intake writes no person, laboratory result or case. Mirth
Connect is not part of Survsys.

A reviewer promotes a staged message on its review screen
(`POST /api/staged-messages/{id}/case`), naming the person, condition, agency
and date first reported. The case is opened by the same path a case created by
hand takes, and the message moves to `assigned`, from which it cannot be
promoted again. The review states are `entry`, `pending`, `exception` and
`assigned`, mapped to the legacy queue numbers by `MessageReviewStatusRules`.

**Person matching proposes; the reviewer decides.** `PersonMatchRules` scores
a person on file against an incoming one from name, birth date, sex at birth,
identifiers and current address, and classifies the score as Exact, Probable or
None at the thresholds in the `PersonMatching` configuration section (95 and
70 by default). `POST /api/persons/match` answers the candidates best first,
each with its reasons; `GET /api/staged-messages/{id}/person-matches` does the
same for a staged message's patient, whom the review screen proposes a match
for. Nothing is assigned without a reviewer.

**Built but not wired in.** Graylist duplicate detection (`GraylistRules`),
vocabulary matching (`VocabularyMatcher`) and the rules engine
(`ExpressionEvaluator`) are in `Survsys.Messaging` with their unit tests, but
nothing in the API tier calls them, and nothing reads or writes the graylist
or vocabulary tables at runtime. In 1.0.0 a message is routed by
no rule.

## The CDC notification pipeline

**Queueing works.** A change to a case raises a `CaseChanged` domain event in
the save that changes it, and `CaseNotificationQueue` upserts the case's one
row in `nmi_export_queue`; `POST /api/person-conditions/{id}/notification-queue`
queues it by hand, and `PATCH /api/notification-queue/{id}/hold` holds or
releases it. The export log, `nmi_log`, is read by `GET /api/export-log` and
its detail.

**Generation and sending are built but not wired in.** `Survsys.Messaging`
holds everything a notification is made of — `PhinVariableMapper` resolves a
condition to its guide and variables, `RepeatingGroupProcessor` expands the
laboratory, vaccine, treatment and travel groups, the ruleset formatters shape
each value, and `OruR01Generator` writes the HL7 v2.5.1 message — and
`ReconciliationCutoff` decides which cases fall inside an MMWR week's
reconciliation. `CdcTransmitter` posts a message to
`CdcTransmission:Endpoint` as `application/hl7-v2` under a timeout. Each is
tested, but in 1.0.0 nothing takes a case off the queue, generates its
message, sends it or writes it to `nmi_log`: the export log holds only the
rows put there from outside the application. Wiring the queue to the
generator and the transmitter is the work that makes Survsys notify the CDC.
Whether the legacy NETSS fixed-width export is still needed is the question in
[`../planning/netss-export-question.md`](../planning/netss-export-question.md);
it is not built.
