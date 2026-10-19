# Inspect the export log

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/InspectTheExportLogTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I open the CDC export log and read the history
> of case exports, newest first — each export's record number, when it was
> queued, when it was sent and how it ended — so that I can see which cases
> have gone to CDC and which are still waiting to go.

Inspecting the export log is inferred from the legacy NMI, read-only here and
never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → **Log** | `../nedss/docs/functional/nmi-ui-overview.md`, *Navigation Map* (`/log`, `v_log.php`, *Export log viewer & queue*) and *Core Export Tables* | The export log, one `log` row per export of a case, carrying its record number, queue time and sent time. |
| NMI → export pipeline | `../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md`, *Export Queue System* | A case is queued, then exported; the log keeps a row for every export, so a case exported more than once has a row for each. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **The log is a history, not the queue.** The cases waiting to be notified
  are the **Notification queue**'s, a screen of its own; the **Export log**
  lists the exports themselves, newest first, fifty to a page.
- **An export not yet sent reads as a dash.** Its **Sent** and **Outcome**
  read **—** until it is sent, rather than being left out of the log.
- **The history this walkthrough reads is seeded.** Survsys does not yet
  write the export log as it generates and sends messages, so the log you
  read is the one the seed places there
  (`Survsys.IntegrationTests/Fixtures/nmi-log.sql`): one case,
  **202500048**, exported twice. On a database without that seed the log is
  empty, and the screen says **No exports have been logged.** in place of the
  table.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The export
   history this walkthrough reads is the one
   `Survsys.IntegrationTests/Fixtures/nmi-log.sql` seeds, against a person and
   a condition `persons.sql` and `conditions.sql` seed in the same folder; run
   all three, in that order, against your database if you brought it up by
   hand.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
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
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, and performs the steps below in a real browser.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Export log**.
2. The browser tab reads **Export log — Survsys** and the heading reads
   **Export log**. A line under it reads **The history of case exports to
   CDC, newest first. Open an export to read the message that was generated
   for it.**
3. The count line reads **2 export(s), showing 2.**
4. The table has the columns **Record number**, **Queued**, **Sent** and
   **Outcome**, and two rows, both for record number **202500048**:
   - The first, the newer, reads **2025-11-10 08:00** for **Queued**, and
     **—** for both **Sent** and **Outcome**: this export is queued and not
     yet sent.
   - The second reads **2025-11-04 08:00** for **Queued**,
     **2025-11-04 08:05** for **Sent** and **accepted** for **Outcome**: this
     export was sent, and CDC accepted it.

## What you should see

The history of exports to CDC, newest first:

- The count line **2 export(s), showing 2.**
- Two exports of case **202500048**: one queued at **2025-11-10 08:00** and
  not yet sent, its **Sent** and **Outcome** reading **—**; and below it one
  queued at **2025-11-04 08:00**, sent at **2025-11-04 08:05** and
  **accepted**.

Each record number is a link into that export's message, which is the
[Review a generated message](review-a-generated-message.md) walkthrough. You
now know how to read the history of exports to CDC. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
