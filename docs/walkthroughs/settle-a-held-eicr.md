# Settle a held eICR

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SettleAHeldEicrTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As a surveillance reviewer, when a reportable eICR arrives for a patient
> who matches someone on file only partly, I find the eICR held for me, say
> that the patient is that person, and open the case it was written to — so
> that a reportable eICR reaches the right person's case, and never a guess.

It is inferred from the legacy EMSA handling of clinical documents and from
Connecticut's eCR requirements, which Survsys's phase 13 was specified from:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → clinical documents | `../planning/ecr-requirements.md`, *Where survsys stands* | Legacy EMSA took an eICR as a clinical-document message, through the same pipeline as a laboratory message, so its patient was matched as a message's is. |
| eCR requirements | `../planning/ecr-requirements.md`, *What survsys must do, by session*, items 7 and 8 | The eICR's patient matched by the person matching, a match that is not confident left to a reviewer, and the eICR's content written to the case of the person chosen. |
| EMSA → the assignment decision | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Stage 3: Assignment Decision* | A message whose patient only probably matches a person on file goes to a person to decide, rather than being assigned. |

Two things this walkthrough describes as Survsys does them:

- **The patient matches someone on file only partly.** Henrik Dahl is on
  file with the same name, birth date and sex as the eICR's patient, at the
  address he lived at before; the eICR carries his new one. The person match
  scores him a probable match and not an exact one, so the eICR is held,
  saying why, and no case is opened until a reviewer settles it.
- **The eICR is settled on its own screen.** Under **Cases** each person the
  patient may be is listed with **Use this person**, beside **Create new
  person**; either writes the eICR to that person's cases, and the screen then
  shows the case it was written to. Both are offered to a user holding
  `edit_cases`. This walkthrough uses a person on file; **Create new person**
  is not part of it.

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
   Department of Health as the condition's responsible agency.
3. **The reference data this walkthrough reads, in that database.** Run
   `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql`,
   `Survsys.E2ETests/Fixtures/elr-intake.sql` and then
   `Survsys.E2ETests/Fixtures/ecr-intake.sql` against it. The third puts the
   zip code **84101** under **Salt Lake County**; the last maps the SNOMED code
   for Malaria, `61462000`, to the catalogue's **Malaria**, and puts **Henrik
   Dahl** on file at 88 East 100 South. The end-to-end suite seeds them
   itself, into a database of its own.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **takes in two documents and opens one case**, a Malaria case
on the Henrik Dahl already on file. Performed a second time over the
same database, the documents' ids are already on file and nothing more is
held or opened; start again from a freshly seeded database to perform it again
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

1. Copy the file `Survsys.E2ETests/Fixtures/Cda/ecr-held-eicr.xml` into the
   eCR drop directory, wait for it to be gone, then copy
   `Survsys.E2ETests/Fixtures/Cda/ecr-held-rr.xml` in after it. The first is
   an eICR for **Henrik Dahl**, born 1985-06-30, of 412 South State Street,
   Salt Lake City, 84101; the second the RR that responds to it, finding
   **Malaria** reportable. Within a few seconds of each copy the file is gone
   from the drop directory: the intake has read and stored it and moved it
   into the directory's `.processed` folder.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **eCR**. The screen is headed
   **eCR documents**, and the row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.9^e2e-held-eicr** reads **eICR** under
   **Kind**.
3. Press **2.16.840.1.113883.9.9.9.9.9^e2e-held-eicr**. The heading reads
   **eICR 2.16.840.1.113883.9.9.9.9.9^e2e-held-eicr**. Under **Reportability
   response** the one condition reads **Malaria (61462000)** under
   **Condition**, **Malaria** under **Maps to** and **Reportable** under
   **Routing**.
4. Under **Cases** a notice reads **Held for review: 1 people on file may be
   the eICR's patient, none of them an exact match.** If the section reads
   **No case has been written from this eICR.** instead, the intake is still
   matching it: reload the page until the notice is there. Below the notice is
   the one person the patient may be, reading **Dahl, Henrik** (person and a
   number), **born 1985-06-30 — Probable match**, with a **Use this person**
   button beside it. Below that is a **Create new person** button.
5. Press **Use this person**. The notice and the buttons are
   gone, and under **Cases** the section reads **The eICR was written to these
   cases:** above one link, reading **Open case**.
6. Press **Open case**. The browser goes to the new report's form, headed
   **Case morbidity report**. In the header bar, **Patient name** reads
   **Dahl, Henrik**, **Disease** reads **Malaria**, **Investigating agency**
   reads **Salt Lake County**, and **Record #** is a new record number — nine
   digits beginning with the current year.

## What you should see

A reportable eICR held for a reviewer, and settled:

- The eICR on the **eCR documents** queue, its RR's **Malaria** mapped and
  routed **Reportable**.
- Under its **Cases**, the notice that it is held because one person on file
  may be its patient and is not an exact match, and that person listed as a
  **Probable match**, with **Use this person**.
- After **Use this person**, one **Open case** link in place of the notice.
- A new case morbidity report for **Dahl, Henrik**, on **Malaria**, under
  **Salt Lake County**.

You now know how to settle an eICR Survsys held for you. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
