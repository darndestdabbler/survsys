# Link an eICR to an existing case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/LinkAnEicrToAnExistingCaseTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As an investigator, when a provider's eICR arrives for a patient I already
> have an open case on for its reportable condition, I find the eICR linked to
> that case rather than a second case opened beside it — so that everything
> the providers report about one illness is on one case.

It is inferred from the legacy EMSA update of an existing case and from
Connecticut's eCR requirements, which Survsys's phase 13 was specified from:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the assignment decision | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Stage 3: Assignment Decision*, *3.2 updateCmrProcess — Update Existing Event* | A message whose patient is a person on file, with an event on the same disease, updates that event instead of adding one; legacy EMSA took an eICR through the same pipeline. |
| eCR requirements | `../planning/ecr-requirements.md`, *What survsys must do, by session*, item 7 | The eICR's patient matched by the person matching and the eICR linked to the open case on that person and condition, including one opened from ELR. |

Two things this walkthrough describes as Survsys does them:

- **Survsys links the eICR, not the investigator.** The eICR's patient is an
  exact match for one person on file, who has an open case on the RR's
  reportable condition, so the eICR is written to that case. Where the match
  is not confident the eICR is held for a reviewer instead, and Survsys has
  no screen yet for settling one; this walkthrough's patient is not that case.
- **The eICR's screen links to the case.** Under **Cases** it shows an
  **Open case** link for each case it was written to, and here that is the
  case already on file.

Opening a case for a patient who has none is its own walkthrough, *Open a case
from an eICR*.

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
3. **The reference data and the case this walkthrough starts from, in that
   database.** Run `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql`,
   `Survsys.E2ETests/Fixtures/elr-intake.sql` and then
   `Survsys.E2ETests/Fixtures/ecr-intake.sql` against it. The last maps the
   SNOMED code for Giardiasis, `58265007`, to the catalogue's **Giardiasis**,
   and puts **Amara Osei** on file with her open Giardiasis case,
   **202600902**. The end-to-end suite seeds them itself, into a database of
   its own.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **takes in two documents and changes** case 202600902, by
writing the eICR onto it. Performed a second time over the same database, the
documents' ids are already on file and nothing more is written; start again
from a freshly seeded database to perform it again as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intakes turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Events**. In the box labelled
   **Last name**, type `Osei`. In the list labelled **Disease**, choose
   **Giardiasis**, then press **Search**.
2. The count line reads **1 case(s) found, showing 1.** The one row reads
   **202600902** for **Record #**, **Osei, Amara** for **Patient** and
   **Giardiasis** for **Disease**.
3. Copy the file `Survsys.E2ETests/Fixtures/Cda/ecr-link-eicr.xml` into the
   eCR drop directory, wait for it to be gone, then copy
   `Survsys.E2ETests/Fixtures/Cda/ecr-link-rr.xml` in after it. The first is
   an eICR for **Amara Osei**, born 1992-08-03, of 61 Liberty Avenue, Salt
   Lake City, 84101 — the person on file, in every field the person match
   reads; the second the RR that responds to it, finding **Giardiasis**
   reportable. Within a few seconds of each copy the file is gone from the
   drop directory: the intake has read and stored it and moved it into the
   directory's `.processed` folder.
4. In the application bar, press **eCR**. The screen is headed **eCR
   documents**, and the row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.9^e2e-link-eicr** reads **eICR** under
   **Kind**.
5. Press **2.16.840.1.113883.9.9.9.9.9^e2e-link-eicr**. The heading reads
   **eICR 2.16.840.1.113883.9.9.9.9.9^e2e-link-eicr**. Under **Reportability
   response** the one condition reads **Giardiasis (58265007)** under
   **Condition**, **Giardiasis** under **Maps to** and **Reportable** under
   **Routing**.
6. Under **Cases** is one link, reading **Open case**: the case the eICR was
   written to. If the section reads **No case has been written from this
   eICR.** instead, the intake is still writing it: reload the page until the
   link is there.
7. Press **Open case**. The browser goes to the report's form, headed **Case
   morbidity report**. In the header bar, **Patient name** reads **Osei,
   Amara**, **Disease** reads **Giardiasis**, and **Record #** reads
   **202600902** — the record number from step 2.
8. In the application bar, press **Events**. In the box labelled **Last
   name**, type `Osei`. In the list labelled **Disease**, choose
   **Giardiasis**, then press **Search**. The count line still reads **1
   case(s) found, showing 1.**, and the one row is still **202600902**: the
   eICR opened no second case.

## What you should see

An eICR that found the case it belonged to:

- Amara Osei's one Giardiasis case, **202600902**, before the eICR arrived.
- The eICR on the **eCR documents** queue, its RR's **Giardiasis** mapped and
  routed **Reportable**, and one **Open case** link under its **Cases**.
- That link leading to case **202600902**, and still only one Giardiasis case
  for Amara Osei on the **Events** screen.

You now know how an eICR is linked to a case already open. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
