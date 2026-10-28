# Send a case to CDC

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SendACaseToCdcTests.cs`, and a step you cannot perform is
a defect in the application or in this document — never something to work
around.

## User story

> As a surveillance reviewer, I queue a case for CDC notification, process the
> notification queue, and find the case gone from the queue and its export
> sent and accepted in the export log, with the message written for CDC's
> sender to pick up — so that I know the case has been notified to CDC.

Sending a case to CDC is inferred from the legacy NMI, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → export pipeline | `../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md`, *Export Queue System* | A case is queued, the queue is processed, each case's message is generated and handed to the transport, and the outcome is logged. |
| NMI → **Queue** and **Log** | `../nedss/docs/functional/nmi-ui-overview.md`, *Navigation Map* and *Business Logic — The Export Pipeline* | The queue a case waits on until it is exported, and the log row its export leaves. |
| NMI → transport | `../nedss/docs/functional/nmi-ui-overview.md`, *Business Logic — The Export Pipeline* | The generated message leaves for CDC through the PHINMS sender, which picks up files from a folder. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **The queue is processed on demand.** Survsys's queue worker is off unless
  a deployment gives it an interval, so this walkthrough processes the queue
  with its **Process now** button rather than waiting for a timed pass.
- **The transport is chosen in configuration.** Survsys either posts each
  message to an HTTP endpoint or writes it as a file into a folder a PHINMS or
  AIMS sender watches. This walkthrough uses the folder, so nothing leaves
  your machine; a written file is logged **accepted**, because the sender's
  delivery is its own.
- **A queued case is processed whatever queued it.** **Process now** sends
  every case waiting, not only yours, so the walkthrough follows its own case
  by its record number.

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
   runs. That folder is your **outbox**: each message is written into it for a
   PHINMS or AIMS sender to pick up, and nothing is sent anywhere.
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
writing into `Survsys.E2ETests/bin/<configuration>/net10.0/cdc-outbox`, brings
the database to migration level and seeds it, and performs the steps below in a
real browser. It records video of every test it runs, and writes the
recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm`
files.

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
5. The report's own form opens, headed **Case morbidity report**. Its header
   bar reads **Okafor, Chidi** for the patient, **Lyme disease** for the
   disease, and a record number of the current year and five more digits.
   Note the record number: it is how you will follow the case.
6. Press **Edit** at the top of the report, then, in the panel headed **CDC
   notification**, press **Queue for notification**. A green confirmation
   reads **This case was queued for notification at** followed by the date
   and time you pressed.
7. In the side menu, under **Notifications**, choose **Notifications**. The browser tab reads
   **Notification queue — Survsys** and the heading reads **Notification
   queue**. The table has a row whose **Record number** is the one you noted,
   reading **Okafor, Chidi** for the **Patient**, **Lyme disease** for the
   **Condition** and **—** for **Hold**: the case is waiting to be sent.
8. Press **Process now**. A line beside it reads **_n_ case(s) processed;
   each is on the export log.**, where _n_ counts every case that was waiting,
   including those other work queued. The table no longer has a row for your
   record number.
9. In the side menu, under **Notifications**, choose **Export log**. The browser tab reads
   **Export log — Survsys**. Find the row whose **Record number** is the one
   you noted. Its **Sent** reads when Process now sent it, as
   `yyyy-mm-dd hh:mm`; its **Outcome** reads **accepted**; and its **Detail**
   reads **Written to the outbox as** followed by a file name, `survsys-` and
   the time and a unique identifier, ending `.hl7`. Note the file name.
10. Open your outbox folder. It holds a file of the name you noted: the
    message that was sent. Its first line begins
    `MSH|^~\&|Survsys^` and names the message type `ORU^R01^ORU_R01`, and its
    last line reads `OBR|1|||68991-9^Epidemiologic Information^LN`.

## What you should see

One case sent to CDC:

- On the **Notification queue**, the case waiting until **Process now**, and
  gone once it is processed.
- On the **Export log**, the case's row, sent and **accepted**, naming the
  file the message was written to.
- In your outbox folder, that file: an `ORU^R01` from Survsys, ready for the
  PHINMS or AIMS sender.

You now know how to send a case to CDC. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
