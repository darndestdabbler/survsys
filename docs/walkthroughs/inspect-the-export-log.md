# Inspect the export log

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/InspectTheExportLogTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I open the CDC export log and read the history
> of case exports, newest first — each export's record number, when it was
> queued, when it was sent, how it ended and what the send said — so that I
> can see which cases have gone to CDC and how.

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
- **The queue is processed on demand.** Survsys's queue worker is off unless
  a deployment gives it an interval, so this walkthrough sends its case with
  the queue's **Process now** button rather than waiting for a timed pass.
- **The row reads what the send said.** Each row carries the send's detail,
  its processing id and its queue beside the outcome, and buttons to resend
  the case or retract an accepted notification.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this walkthrough reports on, **Okafor, Chidi**, is one the people
   seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`), and the
   condition, **Lyme disease**, one the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`). A case's message is
   built only under the notification guide its condition is exported under,
   and `Survsys.E2ETests/Fixtures/notification-guide.sql` puts Lyme disease
   under one. Run all three, in that order, against your database if you
   brought it up by hand.
2. **The `Survsys.Api` tier running, writing to CDC as files.** On a
   developer machine: `dotnet run --project Survsys.Api --urls
   http://localhost:5100`, with `CdcTransmission__Transport=File` and
   `CdcTransmission__OutboxDirectory` set to a folder of your own where it
   runs. Each message is then written into that folder for a PHINMS or AIMS
   sender to pick up, and nothing is sent anywhere.
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

That suite starts both tiers on ports it finds free, with the file transport
writing into a folder under its own build output, brings the database to
migration level and seeds it, and performs the steps below in a real browser.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Okafor`, then press **Search**.
3. The one row reads **Okafor, Chidi**. Press its **Create CMR** button.
4. In the box labelled **First reported to public health**, enter
   `2026-10-02`. In the list labelled **Disease**, choose **Lyme disease**. In
   the list labelled **Investigating agency**, choose **Davis County**. Press
   **Save & Continue**.
5. The report's own form opens, headed **Case morbidity report**, with
   **Lyme disease** for the disease and a record number of the current year
   and five more digits. Note the record number: it is how you will find the
   case's export.
6. Press **Edit** at the top of the report, then, in the panel headed **CDC
   notification**, press **Queue for notification**. A green confirmation
   reads **This case was queued for notification at** followed by the date
   and time you pressed.
7. In the side menu, under **Notifications**, choose **Notifications**. On the **Notification
   queue**, press **Process now**. A line beside it reads **_n_ case(s)
   processed; each is on the export log.**, where _n_ counts every case that
   was waiting, including those other work queued.
8. In the side menu, under **Notifications**, choose **Export log**. The browser tab reads
   **Export log — Survsys** and the heading reads **Export log**. Under it a
   line reads **The history of case exports to CDC, newest first. Open an
   export to read the message that was generated for it, resend its case, or
   retract a notification CDC accepted.**
9. The count line reads **_n_ export(s), showing _m_.**, where the numbers
   count every export on file.
10. Find the row whose **Record number** is the one you noted. It reads:
    - **Queued**: when you queued the case, as `yyyy-mm-dd hh:mm`.
    - **Sent**: when Process now sent it, as `yyyy-mm-dd hh:mm`.
    - **Outcome**: **accepted**.
    - **Detail**: **Written to the outbox as** followed by the name of the
      file the message was written to, `survsys-` and the time and a unique
      identifier, ending `.hl7`.
    - **Processing**: **P**, for production.
    - **Queue**: **production**.

    At its end are a **Resend** button and a **Retract** button.

## What you should see

One case sent to CDC, read in the export log:

- After **Process now**, a line saying how many cases were processed.
- On the **Export log**, a row for the record number you noted, queued and
  sent, whose outcome is **accepted**, whose detail names the outbox file it
  was written to, under processing **P** and queue **production**, with
  **Resend** and **Retract** buttons.

You now know how to read the history of exports to CDC. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
