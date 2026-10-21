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
  separate XML and HL7 copy, and no per-PHIN-variable log beneath it.
- **The history this walkthrough reads is seeded.** Survsys does not yet
  write the export log as it generates and sends messages, so the export you
  review is one the seed places in the log
  (`Survsys.IntegrationTests/Fixtures/nmi-log.sql`): case **202500048**,
  exported twice — once sent and accepted with its message, and once queued
  with no message yet. This walkthrough reviews the first.

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
   **Export log**. Under it, the table lists the exports newest first, each
   with its **Record number**, **Queued**, **Sent** and **Outcome**.
3. Find the row whose **Record number** is **202500048** and whose
   **Outcome** reads **accepted**. Its **Sent** reads **2025-11-04 08:05**.
   Press its record number, **202500048**, which is a link.
4. The browser tab reads **Generated message — Survsys**, and the heading
   reads **Export** followed by the export's own number, which this document
   does not print. A **Back to the export log** button sits under it.
5. Read the export's summary:
   - **Record number** reads **202500048**.
   - **Guide** reads **—**: no notification guide was recorded for this export.
   - **Queued** reads **2025-11-04 08:00**.
   - **Sent** reads **2025-11-04 08:05**.
   - **Outcome** reads **accepted**.
6. Under the heading **Generated message**, a line reads **The message that
   was, or would be, sent to CDC for this export.** Below it, the message is
   shown whole, as four lines:

   ```
   MSH|^~\&|SURVSYS|CT_DPH|CDC|CDC_NNDSS|20251104080500||ORU^R01^ORU_R01|NMI202500048-1|P|2.5.1
   PID|1||202500048^^^SURVSYS||NGUYEN^AN||19920229|M
   OBR|1||202500048^SURVSYS|68991-9^Epidemiologic information^LN|||20251103
   OBX|1|CWE|78746-5^Country of birth^LN||VNM^Viet Nam^ISO3166_1||||||F
   ```

## What you should see

One export to CDC read whole:

- On the **Export log**, a row for **202500048** that was sent at
  **2025-11-04 08:05** and whose outcome is **accepted**.
- On its review screen, the summary **202500048**, guide **—**, queued
  **2025-11-04 08:00**, sent **2025-11-04 08:05**, outcome **accepted**.
- The generated message, an `ORU^R01` of four segments — `MSH`, `PID`, `OBR`
  and `OBX` — about **NGUYEN^AN** under record number **202500048**.

You now know how to open an export from the export log and read the message
that was generated for it. If what you saw differs from what is written above,
the walkthrough has failed: say so rather than adjusting what you did, because
the promise this document makes is that it is performable exactly as written.
