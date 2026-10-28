# Review an eICR and its reportability response

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ReviewAnEicrAndItsReportabilityResponseTests.cs`, and a
step you cannot perform is a defect in the application or in this document —
never something to work around.

## User story

> As a surveillance reviewer, when a provider's electronic initial case report
> arrives with the reportability response that came back on it, I find the two
> on the eCR queue and open the report, so that I can read what the provider
> sent, which conditions the response found reportable and to whom, and what
> validation found wrong with either document.

eCR has no legacy screen to infer this from — legacy EMSA filed an eICR's
clinical content into a case's notes and never read the reportability
response — so it is inferred from Connecticut's eCR requirements, which
Survsys's phase 13 was specified from:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| CT DPH eCR | `../nedss/docs/business-logic/CT_SITE_REQUIREMENTS.md`, *2.1 Architecture Overview*, *2.4 Validation Pipeline* and *2.10 HTML Export* | An eICR and its RR arrive together as CDA documents; each is validated against the schema and the schematron; and the eICR is read by staff as HTML. |
| eCR requirements | `../planning/ecr-requirements.md`, *What survsys must do, by session*, items 3 to 5 | The eICR shown through the HL7 CDA stylesheet beside the RR's determinations, responsible agencies and timeframes, with the validation result recorded rather than blocking intake. |

Two things this walkthrough describes as Survsys does them:

- **The documents arrive in a folder.** The AIMS platform delivers an eICR and
  its RR as two files; Survsys polls a test feed's folder and a production
  feed's folder, and you deliver them yourself by copying the files in.
- **Validation never stops a document.** The fixture RR is deliberately
  small, so validation finds errors in it; it is stored and shown all the
  same, with its findings listed beneath it.

## Before you start

You need all six of these. The second is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The eCR intake's test feed turned on**, which a Survsys starts with off.
   Start `Survsys.Api` with `EcrIntake__TestDirectory` set to a directory of
   your choosing — the **eCR drop directory** below — with
   `EcrIntake__PollIntervalSeconds` set to a few seconds, and with
   `EcrIntake__ResponsibleAgencyIds__0` set to
   `2.16.840.1.113883.4.6^8000000001`, the id the fixture RR gives the Utah
   Department of Health as the condition's responsible agency. Without that
   last setting the RR's condition is for some other jurisdiction and is not
   kept.
3. **The reference data this walkthrough reads, in that database.** Run
   `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql`,
   `Survsys.E2ETests/Fixtures/elr-intake.sql` and then
   `Survsys.E2ETests/Fixtures/ecr-intake.sql` against it. The last maps the
   SNOMED code for Malaria, `61462000`, to the catalogue's **Malaria**. The
   end-to-end suite seeds them itself, into a database of its own.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **takes in two documents**, and the eICR, being reportable,
opens a Malaria case for its patient as *Open a case from an eICR* describes.
Performed a second time over the same database, the documents' ids are
already on file, so the intake moves the files to `.duplicate` instead of
`.processed`; start again from a freshly seeded database to perform it again
as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intakes turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Copy the file `Survsys.E2ETests/Fixtures/Cda/ecr-review-eicr.xml` into
   the eCR drop directory: an eICR R3.1.1 for **Ingrid Solberg**. Within a
   few seconds it is gone from the drop directory: the intake has read,
   validated and stored it and moved it into the directory's `.processed`
   folder. Then copy `Survsys.E2ETests/Fixtures/Cda/ecr-review-rr.xml`, the
   RR that responds to it, finding Malaria reportable, and wait the same way
   until it too is in `.processed`. The eICR goes first so that the RR finds
   the document it responds to.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Intake**, choose **eCR**. The screen is headed
   **eCR documents**.
3. Find the row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.9^e2e-review-eicr**. It reads **eICR** under
   **Kind**, **1** under **Version** and **test** under **Feed**. Find the row
   whose **Document** reads **2.16.840.1.113883.9.9.9.9.8^e2e-review-rr**. It
   reads **RR** under **Kind**, **test** under **Feed** and **0** under **To
   review**: none of its conditions waits for a reviewer.
4. Press **2.16.840.1.113883.9.9.9.9.9^e2e-review-eicr**. The browser tab reads
   **eCR document — Survsys**, and the heading reads **eICR
   2.16.840.1.113883.9.9.9.9.9^e2e-review-eicr**.
5. Under **eICR** is the eICR as the HL7 CDA stylesheet renders it. Its
   heading begins **Initial Public Health Case Report**, and the patient row
   beneath it names **Ingrid SOLBERG**, with a date of birth of **June 7,
   1985**.
6. Under **Reportability response** is a table with one row: the condition
   the RR found. It reads **Malaria (61462000)** under **Condition**,
   **Reportable (RRVS1)** under **Determination**, **Utah Department of Health
   (2.16.840.1.113883.4.6^8000000001)** under **Responsible agency**, **24 h**
   under **Report within**, **Malaria** under **Maps to** — the catalogue
   condition its code maps to — and **Reportable** under **Routing**.
7. Under **Validation findings** is a table of what validation found in both
   documents, the eICR's first. At least one row reads
   **eICR 2.16.840.1.113883.9.9.9.9.9^e2e-review-eicr** under **Document**. One of
   the RR's rows reads **error** under **Severity**, **schematron** under
   **Source**, **RR 2.16.840.1.113883.9.9.9.9.8^e2e-review-rr** under
   **Document** and **This participantRole SHALL contain at least one [1..*]
   addr (CONF:3315-413).** under **Message**: the RR names its responsible
   agency without an address.

## What you should see

An eICR and its reportability response, read together:

- Both documents on the **eCR documents** queue, on the test feed, the RR
  with nothing to review.
- The eICR rendered as the HL7 CDA stylesheet renders it, for **Ingrid
  SOLBERG**.
- The RR's condition, **Malaria**, reportable to the **Utah Department of
  Health** within **24 h**, mapped to the catalogue's **Malaria** and routed
  **Reportable**.
- The validation findings of both documents, stored with them rather than
  stopping them.

You now know how to review an eICR and the response that came back on it. If
what you saw differs from what is written above, the walkthrough has failed:
say so rather than adjusting what you did, because the promise this document
makes is that it is performable exactly as written.
