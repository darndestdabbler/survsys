# Review a generated message

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ReviewAGeneratedMessageTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I open one export of a case to CDC from the
> export log and read it whole — its record number, guide, when it was queued
> and sent, how it ended, and the message that was generated for it — so that
> I can check what Survsys sent to CDC about that case.

Reviewing a generated message is inferred from the legacy NMI, read-only here
and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| NMI → **Log** | `../nedss/docs/functional/nmi-ui-overview.md`, *Navigation Map* and *Core Export Tables* | The export log viewer, whose `log` row carries the record number, the guide, the queued and sent times and the generated message in its `xml` column, with the generated HL7 kept beside it. |
| NMI → message generation | `../nedss/docs/business-logic/NMI_XML_GENERATION.md`, *XML Generation Pipeline*, and `../nedss/docs/functional/nmi-ui-overview.md`, *Business Logic — The Export Pipeline* | The message generated for each exported case — an `ORU^R01` whose `OBR` is the *Epidemiologic Information* profile — which is what the reviewer reads. |

Three differences from the legacy are deliberate, and this walkthrough
describes what Survsys shows rather than what NMI showed:

- **One export opens on a screen of its own.** The export log's record number
  is a link, and it opens that export's review screen, headed **Export**
  and the export's own number, with a **Back to the export log** button.
- **The message is shown as it was generated.** The review screen shows the
  message whole, line for line, in a preformatted block — there is no
  separate XML copy. The build's own messages, where it raised any, are
  listed above it under **Messages**.
- **The queue is processed on demand.** Survsys's queue worker is off unless
  a deployment gives it an interval, so this walkthrough sends its case with
  the queue's **Process now** button and reviews the export that made.

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
   under one, **Walkthrough guide**, which carries no PHIN variables of its
   own. Run all three, in that order, against your database if you brought it
   up by hand.
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
   and five more digits. Note the record number: it is how you will find the
   case's export.
6. Press **Edit** at the top of the report, then, in the panel headed **CDC
   notification**, press **Queue for notification**. A green confirmation
   reads **This case was queued for notification at** followed by the date
   and time you pressed.
7. In the application bar, press **Notifications**. On the **Notification
   queue**, press **Process now**. A line beside it reads **_n_ case(s)
   processed; each is on the export log.**
8. In the application bar, press **Export log**. Find the row whose **Record
   number** is the one you noted; its **Outcome** reads **accepted**. Press
   its record number, which is a link.
9. The browser tab reads **Generated message — Survsys**, and the heading
   reads **Export** followed by the export's own number, which this document
   does not print. A **Back to the export log** button sits under it.
10. Read the export's summary:
    - **Record number** reads the record number you noted.
    - **Guide** reads a number: the identifier of the guide the message was
      built under, **Walkthrough guide**.
    - **Queued** and **Sent** read when you queued the case and when Process
      now sent it, as `yyyy-mm-dd hh:mm`.
    - **Outcome** reads **accepted**.
    - **Detail** reads **Written to the outbox as** followed by the name of
      the file the message was written to, ending `.hl7`.
    - **Processing** reads **P**, and **Queue** reads **production**.

    No **Messages** table follows: the build raised none.
11. Under the heading **Generated message**, a line reads **The message that
    was, or would be, sent to CDC for this export.** Below it, the message is
    shown whole, as two lines:

    ```
    MSH|^~\&|Survsys^2.16.840.1.114222.4.3.3.2.1^ISO|CTDPH^2.16.840.1.114222.4.1.3650^ISO|CDC^2.16.840.1.114222.4.3.2.10^ISO|CDC^2.16.840.1.114222.4.1.3650^ISO|<sent>||ORU^R01^ORU_R01|SURVSYS<sent><id>|P|2.5.1|||||||||NOTF_ORU_v3.0
    OBR|1|||68991-9^Epidemiologic Information^LN
    ```

    where `<sent>` is the moment it was sent, as `yyyymmddhhmmss`, and `<id>`
    the case's own identifier, neither of which this document prints. The
    `MSH` names Survsys at CTDPH sending an `ORU^R01` to CDC under processing
    `P` and profile `NOTF_ORU_v3.0`; the `OBR` is the *Epidemiologic
    Information* order, and there is no `OBX`, because the guide carries no
    PHIN variables.

## What you should see

One export to CDC read whole:

- On the **Export log**, a row for the record number you noted whose outcome
  is **accepted**.
- On its review screen, that record number, a guide, the queued and sent
  times, outcome **accepted**, the outbox file in the detail, processing
  **P** and queue **production**.
- The generated message, an `ORU^R01` of two segments — `MSH` and `OBR`.

You now know how to open an export from the export log and read the message
that was generated for it. If what you saw differs from what is written above,
the walkthrough has failed: say so rather than adjusting what you did, because
the promise this document makes is that it is performable exactly as written.
