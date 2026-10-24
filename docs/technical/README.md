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
| [The inbound laboratory message pipeline](#the-inbound-laboratory-message-pipeline) | 5, 9, 10 |
| [The eCR pipeline](#the-ecr-pipeline) | 13 |
| [The CDC notification pipeline](#the-cdc-notification-pipeline) | 6, 11 |

---

## How Survsys is put together

Two deployed tiers, each its own IIS site, over three libraries and three test
projects, all in `Survsys.sln`. .NET 10, pinned by `global.json`.

| Project | What it is |
|---|---|
| `Survsys.Web` | The application tier: Blazor Server with MudBlazor. It reads everything through the API tier over HTTP and **reaches no database** — it references `Survsys.Contracts` alone, which a test asserts rather than a reviewer checks. |
| `Survsys.Api` | The API tier: minimal APIs. **The only tier permitted to touch the database.** Its endpoints take and return `Survsys.Contracts` types, never entities. It references `Survsys.Data`, `Survsys.Contracts` and `Survsys.Messaging`. |
| `Survsys.Contracts` | The request and response types both tiers share. No behaviour, no dependencies. |
| `Survsys.Data` | EF Core on SQL Server: the entities, `SurvsysDbContext`, the migrations, the audit interceptor, and the pure rules the data carries (workflow, case status, classification, MMWR week, addresses, edit locks, reconciliation cutoff, message review states and exception categories). Referenced by `Survsys.Api` alone. |
| `Survsys.Messaging` | HL7 v2.5.1, CDA and notification logic, pure and in memory: the message parser seam with the ORU^R01 parser (on nHapi) and the CDA parser, the conformance profiles, CDA validation, person matching, the graylist, vocabulary matching, the rules engine, the PHIN variable mapper, repeating groups, the ruleset formatters and the ORU^R01 generator. It references `Survsys.Contracts` and neither `Survsys.Data` nor EF Core, which a test asserts. |
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
agencies, the Administrator role, report sections, vaccines, the code lists
and the Connecticut towns are seeded by the migrations themselves.

| Fixture | What it holds | Used by |
|---|---|---|
| `conditions.sql` | Seven condition rows from the legacy EpiTrax seed, with their print criteria. | integration, end-to-end |
| `persons.sql` | Seven people and the rows hung off them. | integration, end-to-end |
| `person-conditions.sql` | One case morbidity report. | integration |
| `staged-messages.sql` | Four staged inbound messages, queued and exceptions. | end-to-end |
| `nmi-log.sql` | One case and its two exports in the CDC export log. | nothing since phase 11; kept as data |
| `Hl7/*.hl7` | An inbound ORU^R01 and an outbound notification. | integration |

`Survsys.E2ETests/Fixtures` holds the end-to-end suite's own:
`notification-guide.sql` (a guide Lyme disease is notified under),
`elr-intake.sql` and `ecr-intake.sql` (a sending facility, vocabulary, rules,
a zip-to-agency row, an eCR condition mapping, and the people and cases the
intake walkthroughs meet), and the HL7 and CDA files those walkthroughs drop.

## The test suites

Three suites, declared in `dabbler.yaml`, each with a five-minute hang limit.
Neither of the last two skips when what it needs is missing: a suite that
silently skips reads as a green run.

| Suite | Command | Needs |
|---|---|---|
| `unit` — the run of record | `dotnet test Survsys.UnitTests/Survsys.UnitTests.csproj` | nothing |
| `integration` — run whole before every release | `dotnet test Survsys.IntegrationTests/Survsys.IntegrationTests.csproj` | a Docker-compatible container engine, or the SQL Server `SURVSYS_SQLSERVER` names |
| `e2e` — the walkthroughs | `dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj` | a reachable SQL Server; Playwright's chromium, which the harness installs if it is missing |

The integration suite brings its own server. Unless `SURVSYS_SQLSERVER` names
one, it starts a SQL Server 2022 container through Testcontainers
(`Testcontainers.MsSql`) when the run begins, with a password generated for
that container, and removes it when the run ends. Nothing has to be started
first and no password is kept. A missing engine fails the suite and names the
endpoint it tried, `DOCKER_HOST` and `SURVSYS_SQLSERVER`. With Podman on
Windows, a machine that answers on the Docker pipe (`npipe:////./pipe/docker_engine`)
needs nothing set. Otherwise point `DOCKER_HOST` at Podman's own pipe, for
example `npipe:////./pipe/podman-machine-default`. Testcontainers' cleanup
container (Ryuk) works under rootless Podman, so `TESTCONTAINERS_RYUK_DISABLED`
is not needed. Give the Podman machine 4 GiB of memory: 2 GiB is SQL Server's
bare minimum. A whole run takes about nine minutes, so it is not the run of
record. A release session runs it whole, and green, before anything is
published.

The suite drops and re-migrates its own database, `survsys_integration`.
`SURVSYS_SQLSERVER` replaces its connection string whole, so the suite still
runs against a local SQL Server. The end-to-end suite uses `survsys_e2e`, and
`SURVSYS_E2E_SQLSERVER` replaces that one, so a CI runner can change the
authentication as well as the host. The end-to-end harness starts both built tiers on free loopback ports,
migrates, creates a walkthrough-only administrator, empties every table a
walkthrough fills, reseeds the fixtures, and waits on `health/ready` for up to
two minutes. It sends CDC notifications through the file transport into
`bin/<configuration>/net10.0/cdc-outbox`, and IBIS to a stand-in that accepts
everything. The ELR and eCR intake walkthroughs run a second pair of tiers
over a database of their own, with intake, automatic assignment and the eCR
folders switched on. It records video of every test into
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
| `Survsys:DataProtectionKeysPath` | A folder to keep the data-protection keys in. Set it, on both tiers, to keep users signed in across an app-pool recycle or on more than one server. | unset: the default key ring |
| `ElrIntake:DropDirectory` | The folder inbound HL7 files are dropped into. Empty turns the file drop off. | empty |
| `ElrIntake:MllpPort` | The port the MLLP listener takes HL7 on. Empty turns it off. | empty |
| `ElrIntake:PollIntervalSeconds` | How often the drop folder is read. | `120` |
| `ElrIntake:MessageTimeoutSeconds` | How long one message may take. | `30` |
| `Conformance:SenderProfiles` | Which sending facilities (MSH-4.1) are held to which profile; `CT` is the one there is. | empty |
| `Conformance:ConnecticutReceivingApplicationOid`, `Conformance:ConnecticutReceivingFacilityOid` | The OIDs the CT profile expects in MSH-5 and MSH-6. Development has its own application OID. | the production OIDs |
| `MessageProcessing:PollIntervalSeconds` | How often automatic assignment, graylist release and pruning run. Empty turns them off. | empty |
| `MessageProcessing:HomeState` | The state whose patients are in state; others are held out of state. | empty |
| `MessageProcessing:GraylistRetentionDays` | How long a graylisted message waits for a case before it is set aside. | `548` |
| `MessageProcessing:ReprocessIntervalMinutes`, `MessageProcessing:MaxRetries` | How often held, retryable messages are tried again, and how many times. Empty turns reprocessing off. | empty, `3` |
| `MessageProcessing:Whitelist`, `MessageProcessing:WhitelistByCondition` | Which open case a message updates: the rule, and its days, overall and per condition. | `TimeOnset`, 90 days |
| `MessageNotification:IntervalMinutes`, `:Host`, `:Port`, `:EnableSsl`, `:From`, `:UserName`, `:Password` | The SMTP batch e-mail of assigned messages to each agency's notification addresses. Off unless the interval, host and sender are set. The password comes from the environment only. | off, `587`, `true` |
| `PersonMatching:Exact`, `PersonMatching:Probable` | The scores at which a match is exact, and probable. | `95`, `70` |
| `CountyPrograms:elr`, `:ecr`, `:cdc-notification` | Which of an address's county and county equivalent each program uses: `county` or `county-equivalent`. | `county`, `county`, `county-equivalent` |
| `EcrIntake:TestDirectory`, `EcrIntake:ProductionDirectory` | The folders eICRs and RRs are dropped into, one per feed. Both empty turns eCR intake off. | empty |
| `EcrIntake:PollIntervalSeconds` | How often they are read. | `120` |
| `EcrIntake:ResponsibleAgencyIds` | This jurisdiction's ids (`root^extension`); an RR's condition is kept only when it names one. | empty |
| `NotificationQueue:PollIntervalSeconds` | How often the queue worker sends the CDC notification queue. Empty turns it off. | empty |
| `NotificationQueue:ProcessingId`, `NotificationQueue:Queue` | `Production`, `Training` or `Debug` (MSH-11); and `Production`, `Onboarding` or `Batch`, recorded on each log row. | `Production`, `Production` |
| `NotificationQueue:ExportLogRetentionDays` | How long export log rows are kept. Empty keeps them all. | empty |
| `CdcTransmission:Transport` | `Http` posts each notification to the endpoint; `File` writes it into the outbox for a PHINMS or AIMS sender. | `Http` |
| `CdcTransmission:Endpoint`, `:OutboxDirectory`, `:UserName`, `:Password`, `:TimeoutSeconds` | Where the transport sends, its Basic credentials (the password from the environment only), and the timeout. Transmission is off while the endpoint or outbox it needs is empty. | empty, `30` |
| `Geocoding:Enabled` | Geocodes an address saved with a street and no coordinates (the Demographic tab, ELR promotion and eCR intake): the US Census Geocoder's one-line lookup, then Google's Geocoding API where Census fails, times out or finds no match. Each service is sent the street address — street, city, state and zip — and nothing else about the person. A failure of both leaves the coordinates empty and never stops the save. | `false` |
| `Geocoding:CensusEndpoint`, `:CensusTimeoutSeconds`, `:GoogleEndpoint`, `:GoogleTimeoutSeconds` | Each service's endpoint and timeout. | the public services, `10`, `10` |
| `Geocoding:GoogleApiKey` | Google's API key, read from the environment only (`Geocoding__GoogleApiKey`), never a file. Without one Google is skipped. | unset |
| `IbisTransmission:Endpoint`, `IbisTransmission:TimeoutSeconds` | Where a case is posted to IBIS as JSON, and the timeout. Empty turns IBIS off. Not in `appsettings.json`; set it where the tier runs. | unset, `30` |

Every background service — the ELR file drop and MLLP listener, automatic
assignment and reprocessing, the e-mail, eCR intake and the CDC queue worker —
is off until its setting turns it on, and logs once at startup that it is
off.

**What `Survsys.Web` reads:** `Survsys:ApiBaseAddress`, the API tier's
address, which it refuses to start without; `Survsys:DataProtectionKeysPath`,
as above; and `Survsys:CountyEquivalentLabel`, what the county-equivalent
column is called (`Planning region` by default).

Sign-in is not configurable: the API tier issues an opaque bearer token on
`POST /api/sign-in`, valid for eight hours, carrying the user's privileges and
agencies, and the application tier keeps it in its own sign-in cookie, also
eight hours and not sliding. Five failed sign-ins in a row lock an account for
fifteen minutes. The **System settings** screen keeps name-value settings in
the `system_setting` table, but nothing in 1.0.0 reads them: every setting
above is configuration. Neither tier redirects to HTTPS or reads forwarded
headers; terminate TLS at IIS.

## The data model

The schema is `Survsys.Data`'s, built by 55 migrations, in order:
`InitialCreate`, `Persons`, `PersonConditions`, `ClinicalAndLaboratory`,
`CaseClassification`, `Workflow`, `InvestigationAndAdministrative`,
`ContactsAndExposures`, `NotesAndTasks`, `Attachments`,
`AgenciesAndFacilities`, `CoreElements`, `UsersAndRoles`, `SavedViews`,
`VocabularyMatching`, `Graylist`, `MessageQueue`,
`NotificationGuidesAndPhinVariables`, `RuleSets`,
`NotificationQueueAndExportLog`, `Outbreaks`, `Forms`, `TreatmentsAndVaccines`,
`AuditEntries`, `IbisExport`, `RunReportsPrivilege`, `InvestigationWorkflow`,
`EventTypesAndContactEvents`, `CaseTreatmentsAndEncounters`, `CodeLists`,
`CaseDeletionLockAndPrivateNotes`, `OutbreakAttachments`,
`PersonIdentifiersAndAddressHistory`, `PersonMerges`, `ParsedMessages`,
`VocabularyAtIntake`, `FullQueue`, `MessageExceptionsAndAudit`,
`ConformanceWarnings`, `CountyEquivalents`, `LabResultValueLength`,
`VaccineActive`, `ExportLogDetail`, `NotificationReview`,
`NotificationAdministration`, `ExportLogBatchFile`, `AuditUserAndValues`,
`JurisdictionAccessAndLockout`, `ConditionIntakeFlags`, `SystemSettings`,
`EcrDocuments`, `EcrValidationFindings`, `EcrReportability`, `EcrCases` and
`EcrSenders`.

| Area | Tables |
|---|---|
| People | `persons`, `person_addresses`, `person_aliases`, `person_emails`, `person_races`, `person_telephones`, `parent_guardians`, `person_identifiers`, `person_name_history` |
| Cases | `person_conditions` (the case morbidity report), `person_condition_labs`, `person_condition_shares`, `workflow_history`, `encounters`, `notes`, `tasks`, `attachments`, `attachment_files`, `saved_views` |
| Laboratory | `labs`, `lab_tests`, `lab_test_results`, `lab_resist_tests` |
| Exposures | `exposures`, `exposure_places`, `exposure_contacts`, `exposure_sources` |
| Treatments and vaccines | `treatments`, `condition_treatments`, `person_treatments`, `vaccines`, `person_vaccines` |
| Reference data | `conditions`, `agencies`, `facilities`, `zip_code_agencies`, `zip_codes`, `ct_towns`, `core_elements`, `core_element_overrides`, `report_sections`, `code_values`, `code_value_conditions` |
| Supplemental forms | `forms`, `form_sections`, `form_questions`, `form_question_options`, `form_assignments`, `form_answers` |
| Outbreaks | `outbreaks`, `outbreak_access`, `outbreak_updates`, `outbreak_report_sections` |
| Security and audit | `users`, `roles`, `user_roles`, `role_privileges`, `user_agencies`, `access_records`, `audit_entries` |
| Inbound messages | `system_messages`, `system_message_results`, `system_messages_audits`, `graylist_spool` (unused), `vocab_master_loinc`, `vocab_child_loinc`, `vocab_master_organism`, `vocab_master_vocab`, `vocab_child_vocab`, `vocab_rules`, `vocab_rule_conditions` |
| eCR | `ecr_document`, `ecr_validation_finding`, `ecr_rr_condition`, `ecr_case_link`, `ecr_case_value`, `ecr_condition_mapping`, `ecr_sender` |
| CDC notification | `guide`, `guide_link`, `guide_phin_var`, `phin_var`, `condition_detail` (a condition's guide), `nmi_guide_variable`, `nmi_guide_variable_default`, `rule_set` and its `rule_set_*` tables, `nmi_phin_value_set`, `nmi_phin_unit_of_measure`, `nmi_condition_print_criteria`, `nmi_condition_lab_override`, the `nmi_hep_lab_group` tables, `nmi_export_queue`, `nmi_log`, `nmi_log_detail`, `nmi_reconciliation_cutoff` |
| Configuration | `system_setting` |

**The mapping from EpiTrax.** Columns keep the legacy snake_case names, which
is what a later data migration reads across. EpiTrax's tables are pluralised
(`person` became `persons`); the EMSA and NMI tables keep their legacy names.
Strings the legacy schema left unbounded are bounded, so they can be indexed.

**Audit** is an EF Core `SaveChanges` interceptor, `AuditInterceptor`, in
place of the legacy PostgreSQL triggers. It is on every context. For each row a
save inserts, updates or deletes it writes one `audit_entries` row — the
table, the row's key values, the operation, the time in UTC, the signed-in
user, and the row's values before and after as JSON (never a password hash) —
in the same transaction, after the save, so a generated key is the real one; a
save that fails or is rolled back writes nothing. A bulk `ExecuteUpdate` or
`ExecuteDelete` bypasses it, as EF Core bypasses every interceptor; the
lockout counters, code-to-condition links and some administration
replacements are written that way. `GET /api/audit` reads it, for the
**Audit** screen. `access_records` keeps who signed in (`SignIn`), opened a
report (`View`), searched for cases (`Search`) or exported a saved view or its
counts (`Export`), read by `GET /api/access-audit`. Two histories of their
own sit beside them: `workflow_history`, every workflow move, and
`system_messages_audits`, every state a staged message entered.

## The API

Every endpoint is under `/api`, mapped by a `*Endpoints.cs` file in
`Survsys.Api` named after its resource. The case is `person-conditions`, with
its `administrative`, `clinical`, `investigation`, `labs`, `exposures`,
`contacts/import`, `notes`, `attachments`, `tasks`, `status`, `workflow`,
`lock`, `restore`, `brief-note`, `promote`, `shares`, `hidden-elements`,
`notification-queue` and `ibis` beneath it, and `cases` (the case search) with
its `encounters`, `treatments`, `vaccines` and `forms`. Beside them: `persons`
(and `persons/match`), `person-merges`, `labs`, `lab-tests`,
`lab-test-results`, `lab-resist-tests`, `exposures`, `exposure-places`,
`exposure-contacts`, `exposure-sources`, `notes`, `attachments`, `tasks`,
`work-queue`, `saved-views`, `reports`, `outbreaks`, `report-sections`,
`forms`, `treatments`, `vaccines`, `staged-messages` (with its `statistics`,
`move`, `retry`, `delete` and a message's `case` and `person-matches`),
`ecr-documents`, `ecr-quality`, `ecr-condition-mappings`, `ecr-senders`,
`notification-queue`, `export-log`, `reconciliation-cutoff`,
`notification-setup` (guides, PHIN variables, rule sets, value sets, units of
measure, hepatitis lab groups and header defaults), `conditions` (with its
`rules`, `print-criteria` and `lab-overrides`), `agencies`, `facilities`,
`core-elements`, `codes`, `vocabulary`, `ct-towns`, `zip-codes`, `users`,
`investigators`, `roles`, `privileges`, `audit`, `access-audit`,
`system-settings`, `sign-in` and `me`. Reference tables answer CSV on their
`csv` route, an import previewing what it would do until it is sent again
with `?confirm=true`. There is no OpenAPI document; the endpoints' own
summaries and the `Survsys.Contracts` types are the reference.

**Authentication.** `POST /api/sign-in` checks the password (hashed with
ASP.NET Core Identity's hasher) and returns a bearer token carrying the user's
privileges and agencies as claims; five failures in a row lock the account
for fifteen minutes. The application tier's `ApiTokenHandler` puts the token
on every call it makes.

**Privileges.** One authorization policy, `PrivilegeRules`, decides every
request by its method and path, against ten privileges
(`Survsys.Contracts/Privileges.cs`): users, roles, privileges, the audit, the
access audit and system settings need `manage_users`; reference data —
conditions, agencies, facilities, core elements, codes, vocabulary, towns and
zip codes, vaccines, the notification set-up, forms, treatments and the eCR
mappings and senders — is readable by anyone signed in and needs
`manage_reference_data` to change; saved views need `view_cases`; reports
need `run_reports`; person merges need `merge_persons`; a workflow move needs
`view_cases` here, and its endpoint then checks the move's own privilege
(`edit_cases`, `route_cases`, `assign_investigators` or `approve_cases`);
and everything else needs `view_cases` to read and `edit_cases` to write. Not
signed in is a 401; signed in without the privilege is a 403.

**Jurisdiction.** `CaseAccess`, middleware after authorization, finds the
case a request is about — from the route, or from the case a lab, exposure,
note, attachment, task or other child record belongs to — and answers 404
unless the caller holds `all_jurisdictions`, the case's agency is one of the
caller's (`user_agencies`), or the case is shared with one of them. The case
search, the work queue, the task list, saved-view exports and counts and the
case-count report are narrowed the same way, by `CaseAccess.Visible`; a work
queue asked for an agency not the caller's answers no rows. A case the caller
sees only through a share is read-only: every method but GET and HEAD on its
routes, and on those of the records under it, is a 403 saying so, and
`GET /api/person-conditions/{id}` answers `SharedReadOnly` for the report
screen to offer no **Edit**.

**Paging.** A list answers `PagedResult<T>` — `Items`, `Page`, `PageSize`,
`TotalCount` — for `?page=` (from 1) and `?pageSize=` (default 50, at most
200); a page that cannot be returned is a 400 saying why.

**Errors** are RFC 7807 problem details, each with a `correlationId` and never
an exception's text. A refused request is a 400 whose `detail` is one plain
sentence saying what was wrong; a missing resource is a 404, a request the
resource's state refuses is a 409, and an IBIS failure is a 502, or a 504 when
it timed out.

**IBIS.** `POST /api/person-conditions/{id}/ibis` posts the case to IBIS under
the configured timeout — the record number, condition, agency, case status,
onset date and MMWR week and year, and nothing that names the patient — and
marks it sent only when IBIS accepts it. The report's **Send to IBIS** calls
it.

## The inbound laboratory message pipeline

Mirth Connect is not part of Survsys: its channels are hosted services in the
API tier, each off until configured.

**Intake.** Messages enter by a **file drop** — `ElrIntakeService` reads the
top level of `ElrIntake:DropDirectory` every poll interval, moving each file
into `.processed`, or `.failed` when it could not be read or saved — or over
**MLLP**: `MllpListener` takes framed messages on `ElrIntake:MllpPort` and
answers each with an ACK, `AA` when it was queued for review, `AE` with the
reason when it was held, and `AR` when it could not be stored. Both hand the
text to `ElrIntakeService.Receive`, which:

1. parses it through the parser seam (`IMessageParser`; `OruR01Parser` for
   HL7 v2.5.1) and stages **one `system_messages` row per order**, each with
   its parsed patient, order and specimen fields and its results in
   `system_message_results`; an unparseable message is one row held as an
   `exception` (category `unparseable`);
2. holds the message as `unknown-sender` unless MSH-4.1 is the short name of
   a facility on file;
3. checks it against the conformance profile its sender is mapped to in
   `Conformance:SenderProfiles` — `ConnecticutElrProfile`, from the CT
   ELR implementation guide, on the usage codes R, RE, O, X and the
   conditional C(R/X), C(RE/X), C(R/RE) and C(R/O) — holding it as
   `conformance` on a required failure and keeping the warnings on the row;
4. translates each result through `VocabularyMatcher` — the laboratory's
   child code, then the master, then the master's application value — to its
   master LOINC, condition, organism and interpretation, holding the message
   as `loinc-not-mapped` when no result reaches a condition;
5. runs the condition's message-filter and case-management rules through
   `ExpressionEvaluator` (`vocab_rules`), which filter the message out, hold
   it, or set whether it may open a case, update one, or is surveillance; and
6. sets the investigating agency where exactly one agency claims the
   patient's zip code.

Every state a message enters is kept in `system_messages_audits`.

**States.** `MessageReviewStatusRules` holds the fifteen legacy states:
`entry`, `pending`, `exception`, `assigned`, `filtered`, `locked`, `qa`,
`out-of-state`, `semi-auto`, `unprocessed` and the gray cluster (`gray`,
`gray-pending`, `gray-processed`, `gray-unprocessable`, `gray-exception`).
`MessageExceptionRules` holds the exception categories, each with its legacy
id and whether it may be retried. `POST /api/staged-messages/move`, `/retry`
and `/delete` act on up to 500 messages at once, all or none: a move goes
anywhere but `assigned`, a delete takes anything but an assigned message, and
a retry translates a held, parsed, retryable message again without parsing
it again.

**Assignment.** `MessageProcessingService`, every
`MessageProcessing:PollIntervalSeconds`, decides each `entry` message: one
with no condition or no case decision goes to `pending`; one whose results are
all preliminary, for a condition that filters those, to `filtered`; one whose
rules do not allow a new case to `gray`; one for a patient outside
`MessageProcessing:HomeState` to `out-of-state`, unless its condition says
otherwise; one with no agency to `pending`. Then the patient is matched: one
exact match is `assigned`, updating that person's open case on the condition
where the whitelist rule finds one; several exact or only probable matches go
to `pending`; no match at all creates the person and opens the case, in one
transaction. The same service releases a graylisted message to `pending` when
a case on its condition appears for its person within 30 days of its
collection, sets aside (`gray-unprocessable`) one older than
`GraylistRetentionDays`, and on its own interval retries held, retryable
messages up to `MaxRetries` times. `MessageNotificationService` e-mails each
agency's notification addresses a list of the messages newly assigned to its
cases, naming no patient.

**Promotion and update.** A reviewer promotes a staged message from `entry`,
`pending`, `semi-auto` or `qa` (`POST /api/staged-messages/{id}/case`),
naming the person, condition, agency and date first reported.
`MessageCaseWriter` writes the case, the message's specimen, tests (under
their master LOINC), results and susceptibilities, and its address as the
person's current one, together with the message's assignment, in one
transaction; the message is claimed first, so a second promotion writes
nothing. Updating an open case follows `WhitelistRules` (exception, newest,
never new, always new, time from onset, or a window before and after) and
compares labs by `LabComparison`: a lab the case lacks is added, one it holds
has missing tests knitted on and its statuses upgraded, a duplicate writes
nothing, and a conflicting result goes to `pending`. A patient who is not an
exact match for the case's person also goes to `pending`.

**Person matching.** `PersonMatchRules` scores a person on file against an
incoming one from name, birth date, sex at birth, identifiers and current
address, and classifies the score as Exact, Probable or None at the
thresholds in the `PersonMatching` configuration section (95 and 70 by
default). `POST /api/persons/match` answers the candidates best first, each
with its reasons; `GET /api/staged-messages/{id}/person-matches` does the same
for a staged message's patient, whom the review screen proposes a match for.

**Counties.** A Connecticut address keeps its historical county and its
planning region (its county equivalent). Promotion stores a message's county
in whichever `CountyPrograms:elr` names and fills the other from the town in
`ct_towns`, which an administrator keeps on the **Towns and zip codes**
screen.

**Duplicate persons are merged by a person, in one transaction.**
`GET /api/person-merges/candidates` pairs live people who share a last name,
birth date or identifier value and answers the pairs `PersonMatchRules` calls
Exact or Probable, best first. `POST /api/person-merges` keeps one person and
moves the other's cases (with everything on them), labs, contact and
exposure-source links, guardian links, identifiers, aliases, name history,
races, telephones, emails and addresses onto them; the merged-away person's
current address is closed where the kept one already has one, and the record
stays on file as a tombstone (`merged_into_person_id`, `merged_at`) that
person search and matching no longer find. The `AuditInterceptor` audits every
moved row inside the same transaction, so a failed merge changes nothing. Both
routes, and `GET /api/person-merges/persons/{id}` through which the merge
screen reads the two records it compares, need the `merge_persons` privilege
and nothing more.

The `graylist_spool` table, from the legacy schema, is written and read by
nothing: the graylist is the `gray` states on `system_messages`.

## The eCR pipeline

**Intake.** `EcrIntakeService` reads `EcrIntake:TestDirectory` and
`EcrIntake:ProductionDirectory` every poll interval, each its own feed. Each
document is parsed by `CdaParser` (eICR R1.1, R3.1 and R3.1.1, and RR 1.1, by
template id, with DTDs refused), validated by `CdaValidator` against the CDA
R2 schema and the HL7 schematron for its kind and version, and stored whole in
`ecr_document` with its findings in `ecr_validation_finding`; findings never
stop a document being stored. A stored file moves into `.processed`, one
whose id is already on file into `.duplicate`, and one that cannot be read
into `.failed`. An eICR's versions share a set id; an RR is paired with the
eICR it responds to, within its feed. Nothing enters the ELR queue.

**Reportability.** An RR's conditions (`ecr_rr_condition`) are kept when
their responsible agency is one of `EcrIntake:ResponsibleAgencyIds` and the
determination is reportable (RRVS1) or may be reportable (RRVS2). A kept
condition is routed `reportable` when it is RRVS1 and its code maps to a
catalogue condition in `ecr_condition_mapping`, and `review` otherwise.
Routing is worked out when a document is read, so a mapping added later takes
effect at once.

**Cases.** Once an eICR and a reportable RR are both stored, whichever came
second, `EcrCaseWriter` finds the patient — one exact match and no probable
one, or a new person where there is no candidate — and links the eICR to the
person's open case on each reportable condition, or opens one, with the
agency of the patient's zip. It writes the demographics, address (its county
by `CountyPrograms:ecr`), encounter, problems as clinical notes, onset date,
pregnancy, occupation, medications as treatments, travel as exposures, and
labs for senders marked to send them (`ecr_sender`). Each value it writes is
recorded in `ecr_case_value` under its `ecr_case_link`, so a later version
overwrites only what still reads as the previous version wrote it, and
removes the encounters, notes and treatments it no longer carries that no one
has edited. Any other patient holds the document for review, with its
reason. `GET /api/ecr-documents/{id}/person-matches` and
`POST /api/ecr-documents/{id}/case` settle a held document by API; there is no
screen for it.

## The CDC notification pipeline

**Queueing.** A change to a case — its labs (those an ELR message writes
among them), clinical, investigation, administrative or demographic data, its
status, its workflow — raises a `CaseChanged` domain event in the save that
changes it, and `CaseNotificationQueue` upserts the case's one row in
`nmi_export_queue` when the condition's print criteria
(`nmi_condition_print_criteria`) name the case's status. By hand, a case is
queued from its report (`POST /api/person-conditions/{id}/notification-queue`),
by record number (`POST /api/notification-queue/records`, without the print
criteria) or by MMWR range (`POST /api/notification-queue/mmwr-range`), held or
released (`PATCH .../hold`), removed (`DELETE`), resent from the export log
(`POST /api/export-log/{id}/resend`), or retracted
(`POST /api/notification-queue/{id}/retract`, for a case with an accepted
export, sent with NOT118 = X).

**Sending.** `NotificationQueueService`, every
`NotificationQueue:PollIntervalSeconds` or at once on
`POST /api/notification-queue/process`, first deletes export log rows older
than `ExportLogRetentionDays`, then takes each unheld entry off the queue,
oldest first, claiming it by deleting it so two passes cannot both send it.
`NotificationMessageBuilder` resolves the condition's guide and PHIN
variables (`PhinVariableMapper`), expands the laboratory, vaccine, treatment
and travel groups (`RepeatingGroupProcessor`, with the hepatitis lab grouping
and the condition's organism and test-type overrides), shapes each value by
its rule set, and has `OruR01Generator` write the HL7 v2.5.1 ORU^R01 — two
OBRs under the arbovirus guide, and an NK1 mother segment when a mother's
variable is valued, with the header from the guide's variables over its
defaults and MSH-11 from `ProcessingId`. `CdcTransmitter` sends it by
`CdcTransmission:Transport`: `Http` posts it as `application/hl7-v2`, with
Basic credentials when a user name is set; `File` writes it into
`OutboxDirectory` under a temporary name and renames it, for a PHINMS or AIMS
sender. The outcome is written to `nmi_log` — accepted or failed, the message,
the processing id, the queue and the batch file — with its messages by
severity in `nmi_log_detail`. An entry leaves the queue whatever the outcome.

**Reconciliation.** The cutoff, an MMWR year and week, is stored in
`nmi_reconciliation_cutoff` and set on the **Reconciliation** screen
(`/api/reconciliation-cutoff`); its `totals` count the accepted cases by
condition and MMWR year it takes in.

**Administration.** Guides, their PHIN variables, linked guides and header
variables, the header defaults, rule sets, PHIN value sets, units of measure
and hepatitis lab groups are kept under `/api/notification-setup`, each on its
own screen; a guide exports and imports as JSON. A condition's guide, print
criteria and lab overrides are set on its own screen.

Whether the legacy NETSS fixed-width export is still needed is the question in
[`../planning/netss-export-question.md`](../planning/netss-export-question.md);
it is not built.
