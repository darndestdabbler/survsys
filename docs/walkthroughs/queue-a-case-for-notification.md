# Queue a case for notification

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/QueueACaseForNotificationTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I queue a case morbidity report for CDC notification by
> hand from the report itself, read that it was queued, and find it waiting on
> the notification queue, so that a case that must be sent to CDC again is
> sent rather than forgotten.

Queuing a case for notification is inferred from the legacy NMI, read-only
here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → export queue | `../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md`, *Export Queue System* — *Queue Operations* and *Queue Population* | Cases enter the export queue automatically when they change, or by hand when a user triggers the export of a specific record; a case already queued has its entry replaced rather than a second one added. |
| NMI → **Log** | `../nedss/docs/functional/nmi-ui-overview.md`, *Navigation Map* and *Business Logic — The Export Pipeline*, *1. Queue Population* | The export log viewer and queue a user reads to see which cases are waiting to go to CDC. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **The action is on the report.** There is no separate export screen to
  pick a record from: the case morbidity report carries a **CDC
  notification** panel with a **Queue for notification** button.
- **A new case is already queued.** Survsys queues a case for notification
  whenever it is created or changed, so the report this walkthrough creates
  is on the queue before you press the button. Pressing it queues the case
  again by hand: the case keeps its one place on the queue, and its queued
  time becomes the moment you pressed.
- **The queue is its own screen.** The **Notification queue** lists every
  case waiting to be notified, oldest first, fifty to a page, with the hold a
  reviewer raises to keep one back. Holding a case is not part of this
  walkthrough.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this report is opened on, **Okafor, Chidi**, is one of the seven
   the people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`),
   and the condition it is opened for, **Lyme disease**, is one of the seven
   the conditions seed inserts (`Survsys.IntegrationTests/Fixtures/conditions.sql`);
   run both against your database if you brought it up by hand.
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
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. The browser tab reads **New case morbidity report — Survsys**, the heading
   reads **New case morbidity report**, and a line under it reads **Step 1 of
   2.**
3. In the box labelled **Last name**, type `Okafor`, then press **Search**.
4. The count line reads **1 person(s) found, showing 1.**, and the one row
   reads **Okafor, Chidi** with a **Create CMR** button.
5. Press **Create CMR** on that row.
6. The heading still reads **New case morbidity report**, and the line now
   reads **Step 2 of 2. Reporting on Okafor, Chidi**, followed by an
   identifier this document does not print.
7. In the box labelled **First reported to public health**, enter
   `2026-10-02`.
8. In the list labelled **Disease**, choose **Lyme disease**.
9. In the list labelled **Investigating agency**, choose **Davis County**,
   then press **Save & Continue**.
10. The report's own form opens, headed **Case morbidity report**. Its header
    bar reads **Okafor, Chidi** for the patient, **Lyme disease** for the
    disease and **Davis County** for the agency, with a record number of the
    current year and five more digits. Note the record number: it is how you
    will find the case on the queue.
11. Press **Edit** at the top of the report, which opens it for editing. Below the header, find the panel headed **CDC notification**. It reads
    **Queue this case to be notified to CDC. A case change queues it
    automatically; this queues it again by hand.** Press **Queue for
    notification**.
12. A green confirmation appears in the panel, reading **This case was queued
    for notification at** followed by the date and time you pressed, as
    `yyyy-mm-dd hh:mm`.
13. In the side menu, under **Notifications**, choose **Notifications**. The browser tab
    reads **Notification queue — Survsys** and the heading reads
    **Notification queue**.
14. The count line reads **_n_ case(s) queued, showing _m_.**, where the
    numbers count every case waiting, including those other work has queued.
    The table has a row whose **Record number** is the one you noted. That row
    reads **Okafor, Chidi** for the **Patient**, **Lyme disease** for the
    **Condition**, the time you pressed for **Queued**, and **—** for
    **Hold**, with a **Hold** button at its end. The queue lists cases oldest
    first, so the case you just queued is at or near the bottom of the page.

## What you should see

A case queued by hand and found waiting on the notification queue:

- A case morbidity report opened on **Okafor, Chidi** for **Lyme disease**
  under **Davis County**, whose **CDC notification** panel confirms **This
  case was queued for notification at** the time you pressed.
- On the **Notification queue**, one row for that record number, reading
  **Okafor, Chidi**, **Lyme disease**, and **—** for **Hold**: the case is
  waiting to be notified and nothing is holding it back.

You now know how to queue a case for CDC notification by hand and where to
find it waiting. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because the
promise this document makes is that it is performable exactly as written.
