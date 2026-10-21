# Retract a notification

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RetractANotificationTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I retract a case's notification that CDC has
> already accepted — from its row in the export log — and send the
> retraction, so that CDC withdraws a case that should not have been notified.

Retracting a notification is inferred from the legacy NMI, read-only here and
never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → export pipeline | `../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md`, *Export Queue System* | A case is queued again to be sent with its notification status changed, rather than a notification being deleted at CDC. |
| NMI → rule sets | `../nedss/docs/business-logic/NMI_RULESETS.md`, *notificationStatusResponse* | The notification status, PHIN variable `NOT118`, which a retraction sends as `X` — results cannot be obtained — and which the message carries as the order's result status. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **A retraction starts from the export log.** An export CDC accepted has a
  **Retract** button on its row, which queues the case again as a
  retraction; there is no separate retraction screen.
- **The queue is processed on demand.** Survsys's queue worker is off unless
  a deployment gives it an interval, so this walkthrough sends both the case
  and its retraction with the queue's **Process now** button.
- **The retraction is an export of its own.** It is logged as a second row
  for the case, newest first, beside the export it withdraws, and its message
  is read like any other.

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
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Okafor`, then press **Search**.
3. The one row reads **Okafor, Chidi**. Press its **Create CMR** button.
4. In the box labelled **First reported to public health**, enter
   `2026-10-02`. In the list labelled **Disease**, choose **Lyme disease**. In
   the list labelled **Investigating agency**, choose **Davis County**. Press
   **Save & Continue**.
5. The report's own form opens, headed **Case morbidity report**, with
   **Lyme disease** for the disease and a record number of the current year
   and five more digits. Note the record number: it is how you will follow the
   case.
6. Press **Edit** at the top of the report, then, in the panel headed **CDC
   notification**, press **Queue for notification**. A green confirmation
   reads **This case was queued for notification at** followed by the date
   and time you pressed.
7. In the application bar, press **Notifications**. On the **Notification
   queue**, press **Process now**. A line beside it reads **_n_ case(s)
   processed; each is on the export log.**
8. In the application bar, press **Export log**. The browser tab reads
   **Export log — Survsys**. The one row whose **Record number** is the one
   you noted reads **accepted** for its **Outcome**. Press its **Retract**
   button.
9. A green confirmation above the table reads **A retraction of case**
   followed by your record number and **is queued (NOT118 = X).**
10. In the application bar, press **Notifications**, and press **Process
    now** again. The line beside it reads **_n_ case(s) processed; each is on
    the export log.**
11. In the application bar, press **Export log**. Your record number now has
    two rows, both **accepted**: the retraction, newest, first, and the
    export it withdraws under it. Press the record number on the first of
    the two.
12. The browser tab reads **Generated message — Survsys**, and **Record
    number** reads your record number. Under **Generated message**, the
    message is two lines: an `MSH`, and the order

    ```
    OBR|1|||68991-9^Epidemiologic Information^LN|||||||||||||||||||||X
    ```

    whose last field, the result status (`OBR-25`), is `X`: the notification
    status `NOT118` a retraction sends.

## What you should see

One notification retracted:

- On the **Export log**, after **Retract**, the confirmation that a
  retraction of your case is queued with `NOT118 = X`.
- After **Process now**, two accepted rows for your case, the retraction
  first.
- The retraction's generated message, whose `OBR` carries the result status
  `X`.

You now know how to retract a notification CDC accepted. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
