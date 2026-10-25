# Update an existing case from a laboratory message

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/UpdateAnExistingCaseFromALaboratoryMessageTests.cs`, and a
step you cannot perform is a defect in the application or in this document —
never something to work around.

## User story

> As an investigator, when a laboratory sends a new result for a patient I
> already have an open case on for that disease, I find the result added to
> that case, rather than a second case opened beside it, so that everything
> the labs say about one illness is on one case.

This is the update half of the legacy message pipeline, and it is inferred
from the EMSA processing EpiTrax sat behind:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the assignment decision | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Stage 3: Assignment Decision*, *3.2 updateCmrProcess — Update Existing Event* | A message whose patient is a person on file, with an event on the same disease the whitelist rules match, updates that event instead of adding one. |
| EMSA → what an update writes | `../nedss/docs/business-logic/EMSA_EPITRAX_CONTRACT.md`, and the same pipeline document's *Lab Comparison Logic* | The message's specimen, tests and results are added to the event's laboratory record. |

One difference from the legacy screens is deliberate, and this walkthrough
describes what Survsys shows rather than what EMSA showed: **the message's
screen does not link to the case it updated.** You read the update on the
case itself, which you find on the **Events** screen.

The whitelist rule that decides which case a message updates is Survsys's
default unless a deployment sets its own: a case whose event date is no more
than 90 days before the message's collection date. Leila Haddad's case has an
event date of 2026-09-28 and the message was collected on 2026-10-05, a week
later.

## Before you start

You need all six of these. The second is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The inbound ELR intake turned on**, which a Survsys starts with off.
   Start `Survsys.Api` with `ElrIntake__DropDirectory` set to a directory of
   your choosing — the **drop directory** below — and with
   `ElrIntake__PollIntervalSeconds` and `MessageProcessing__PollIntervalSeconds`
   set to a few seconds each. The first is how often the drop directory is
   read; the second turns on the assignment pass, which is off unless it is
   set.
3. **The reference data and the case this walkthrough starts from, in that
   database.** Run `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql` and then
   `Survsys.E2ETests/Fixtures/elr-intake.sql` against it. The last puts the
   sending lab **ARUP_LAB** on file with the vocabulary and the Giardiasis
   rule that let its message update a case, and puts **Leila Haddad** on file
   with her open Giardiasis case, **202600901**, which has no laboratory
   result yet. The end-to-end suite seeds them itself, into a database of its
   own; a database you brought up by hand does not have them until you run
   them.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **changes** case 202600901, by adding a specimen to it.
Performed a second time over the same database, the message's specimen is
already on the case, so the second message is compared against it rather than
added; start again from a freshly seeded database to perform it again as
written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intake turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Events**. In the box labelled
   **Last name**, type `Haddad`. In the list labelled **Disease**, choose
   **Giardiasis**, then press **Search**.
2. The count line reads **1 case(s) found, showing 1.** The one row reads
   **202600901** for **Record #**, **Haddad, Leila** for **Patient**,
   **Giardiasis** for **Disease**, **Salt Lake County** for **Agency** and
   **2026-09-28** for **Onset**.
3. Press **202600901**. The report's own form opens, headed **Case morbidity
   report**, and its header bar reads **Haddad, Leila** for the patient,
   **202600901** for the record number and **Giardiasis** for the disease.
   Note the address in your browser's address bar: it ends in `/event/edit/`
   and a number, and step 7 comes back to it.
4. Press **Edit** at the top of the report, then press the **Laboratory** tab.
   Its **Specimens** table reads **No specimens recorded yet.**
5. Copy the file `Survsys.E2ETests/Fixtures/Hl7/elr-update-example.hl7` into
   the drop directory. It is a Giardia antigen result, positive, from
   **ARUP_LAB** for **LEILA HADDAD**, born 1988-04-12, of 48 Orchard Lane,
   84101 — the person on file, in every field the person match reads — with
   the accession number **OBR86002**, collected on **2026-10-05**. Within a
   few seconds the file is gone from the drop directory: the intake has read
   it, queued it and moved it into the directory's `.processed` folder.
6. In the application bar, press **Staged messages**. Open the **Review
   state** list and choose **assigned**. The row whose **Source** reads
   **elr-update-example.hl7** reads **assigned** under **Review state** and a
   dash under **Exception**. If the row is not there yet, the assignment pass
   has not run since the intake queued the message: choose **All** from the
   **Review state** list and then **assigned** again, which reads the queue
   afresh, until it is.
7. Go back to the report's address — the one you noted in step 3 — by
   entering it in the address bar again, so that the screen is read afresh
   from the database. Press **Edit** at the top of the report, then the
   **Laboratory** tab. The **Specimens** table now has one row, whose
   **Accession** reads **OBR86002** and **Collected** reads **2026-10-05**:
   the message's specimen, written onto the case Leila Haddad already had.
8. In the application bar, press **Events**. In the box labelled **Last
   name**, type `Haddad`. In the list labelled **Disease**, choose
   **Giardiasis**, then press **Search**. The count line still reads **1
   case(s) found, showing 1.**, and the one row is still **202600901**: the
   message opened no second case.

## What you should see

A laboratory message that found the case it belonged to:

- Case **202600901** with no laboratory result before the message arrived.
- The message on the **Staged messages** queue, assigned with no reviewer and
  no exception.
- The message's specimen, **OBR86002**, collected **2026-10-05**, on the
  case's **Laboratory** tab afterwards.
- Still only one Giardiasis case for Leila Haddad on the **Events** screen.

You now know how Survsys adds a laboratory result to the case it belongs to.
If what you saw differs from what is written above, the walkthrough has
failed: say so rather than adjusting what you did, because the promise this
document makes is that it is performable exactly as written.
