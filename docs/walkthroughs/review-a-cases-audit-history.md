# Review a case's audit history

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ReviewACasesAuditHistoryTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance administrator, I change a field on a case, then open the
> case's history of changes and read that change — who made it, when, and the
> case's values before and after — so that I can answer for every change made
> to a case.

Reviewing a case's audit history is inferred from the legacy EpiTrax,
read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → audit trail | `../nedss/docs/functional/phase-1.6-cross-cutting-patterns.md`, *2. Audit Trail* | Every insert, update and delete on a table fires EpiTrax's audit trigger into its `audit.audit` table, recorded as a change payload. |
| EpiTrax → audit and logging | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *14. Audit & Logging* | The database audit triggers are carried into the port, simplified to the data tier's own record of each save. |

Two differences from the legacy are deliberate, and this walkthrough describes
what Survsys shows rather than what EpiTrax showed:

- **One screen reads every table's changes.** A case's history is the
  **Audit** screen opened at the case's own row of the table
  `person_conditions`, which the report's **History of changes** link does
  for you.
- **The values are shown as the row's columns.** Before and after are each
  the whole row as JSON, column name to value, so the change is read by
  comparing one column across the two.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this walkthrough reports on, **Okafor, Chidi**, is one the people
   seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`), and the
   condition, **Lyme disease**, one the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`). Run both, in that
   order, against your database if you brought it up by hand.
2. **The `Survsys.Api` tier running.** On a developer machine: `dotnet run
   --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. On
   a fresh database the only user is the administrator the API tier creates on
   its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. The
   history link is shown only to a user who may read the audit. Open the
   application tier's address, sign in, then begin at step 1. Note the user
   name you signed in as: the history names it.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, signs in as the administrator `administrator`,
and performs the steps below in a real browser. It records video of every test
it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Okafor`, then press **Search**.
3. The one row reads **Okafor, Chidi**. Press its **Create CMR** button.
4. In the list labelled **Disease**, choose **Lyme disease**. In the list
   labelled **Investigating agency**, choose **Davis County**. Press **Save &
   Continue**.
5. The report's own form opens, headed **Case morbidity report**. Its header
   bar reads **Okafor, Chidi** for the patient and **Lyme disease** for the
   disease.
6. Press **Edit** at the top of the report, then the **Administrative** tab.
   In the box labelled **Event name**, type `Church picnic`, then press **Save
   & Continue**. A green message reads **The administrative data has been
   saved.**
7. Press **Done** at the top of the report, then press the **History of
   changes** link above it.
8. The browser tab reads **Audit — Survsys** and the heading reads **Audit**.
   The box labelled **Table** reads `person_conditions` and the box labelled
   **Key** reads the case's own id. A line reads **_n_ change(s), showing
   _n_.**
9. Find the row whose **Operation** reads **Update** and whose **Before**
   shows `"event_name":null` and whose **After** shows `"event_name":"Church
   picnic"`. Its **User** reads the user name you signed in as, its **Key**
   the case's id, and its **When (UTC)** the time you saved, as
   `yyyy-mm-dd hh:mm:ss` in UTC.
10. Find the row whose **Operation** reads **Insert**: the case's creation in
    step 4. Its **Before** reads **—**, because the case had no values before
    it was made, and its **User** reads the user name you signed in as.

## What you should see

One case's history of changes:

- On the **Audit** screen, opened from the case, every change saved to the
  case's row, newest first.
- The **Update** that set the event name, naming you, with the event name
  empty before and `Church picnic` after.
- The **Insert** that created the case, naming you, with nothing before.

You now know how to review a case's audit history. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
