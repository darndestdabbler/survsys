# Find a message assigned automatically

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/FindAMessageAssignedAutomaticallyTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As a surveillance reviewer, I find an electronic lab report that Survsys
> assigned to a new case on its own, with no reviewer, and I open the case it
> made, so that I can trust that a confident message does not wait in the
> queue for me and see what it became.

This is the automated half of the legacy message pipeline, and it is inferred
from the EMSA processing EpiTrax sat behind:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the assignment decision | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Stage 3: Assignment Decision*, *3.1 addCmrProcess — Create New Event* | A message the message filter allows, whose case-management rule opens a case, and whose patient matches no person on file, is given a new person and a new event without a reviewer. |
| EMSA → the queue | The same document, *Queue State Machine* | A message assigned to an event leaves the reviewer's queues for the assigned state. |

Two differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EMSA showed:

- **The assigned state reads as its code.** The **Review state** list and the
  queue's **Review state** column read **assigned** in lower case, as they
  did when a reviewer's promotion was the only way into it.
- **The message's screen does not link to its case.** An assigned message's
  own screen says it is already a case; you find the case on the **Events**
  screen by its patient, as you would any other.

Promoting a message to a case by hand is its own walkthrough, *Promote a
message to a case*; this one has no reviewer in it.

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
3. **The reference data this walkthrough's message is translated by, in that
   database.** Run `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql` and then
   `Survsys.E2ETests/Fixtures/elr-intake.sql` against it. The last puts the
   sending lab **ARUP_LAB** on file, maps its LOINC codes to their conditions,
   gives **Hepatitis A** a case-management rule that opens a case for a
   positive result, and maps the patient's zip code **84101** to **Salt Lake
   County**. The end-to-end suite seeds them itself, into a database of its
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

This walkthrough **adds** a person, a case and a staged message. Performed a
second time over the same database, its message is no longer for a patient
nobody has on file, so it updates the case the first run opened instead of
opening another; start again from a freshly seeded database to perform it
again as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intake turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Copy the file `Survsys.E2ETests/Fixtures/Hl7/elr-assign-example.hl7` into
   the drop directory. It is a Hepatitis A IgM result, positive, from
   **ARUP_LAB** for **ROSA CASTILLO**, born 1994-12-05, who is not on file.
   Within a few seconds the file is gone from the drop directory: the intake
   has read it, queued it and moved it into the directory's `.processed`
   folder.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Intake**, choose **Staged messages**. The heading
   reads **Staged messages**.
3. Open the **Review state** list and choose **assigned**. The table shows the
   messages assigned to a case. The row whose **Source** reads
   **elr-assign-example.hl7** reads **assigned** under **Review state** and a
   dash under **Exception**: nothing held it, and no reviewer touched it. If
   the row is not there yet, the assignment pass has not run since the intake
   queued the message: choose **All** from the **Review state** list and then
   **assigned** again, which reads the queue afresh, until it is.
4. In that row, press the **elr-assign-example.hl7** link in the **Source**
   column.
5. The browser tab reads **Staged message — Survsys**. **Source** reads
   **elr-assign-example.hl7**, **Review state** reads **assigned** and
   **Exception** reads a dash. Under **Raw message** is the message as the
   lab sent it, with its patient's line beginning
   **PID|1||ASSIGNPAT001^^^ARUP_LAB^MR||CASTILLO^ROSA||19941205|F**. Below it,
   where a reviewer would promote a message, the screen reads **This message
   has already been promoted to a case.**
6. In the side menu, under **Cases**, choose **Events**. The heading reads **Events**. In
   the box labelled **Last name**, type `Castillo`. In the list labelled
   **Disease**, choose **Hepatitis A**, then press **Search**.
7. The count line reads **1 case(s) found, showing 1.** The one row reads
   **CASTILLO, ROSA** for **Patient**, the name in capitals as the lab sent it;
   **Hepatitis A** for **Disease**; **Salt Lake County** for **Agency**, the
   agency the zip code **84101** maps to; **Assigned to LHD** for
   **Workflow**; and today's date as `yyyy-mm-dd` for **Onset**, the day the
   case was opened. Its **Record #** is a record number of the current year
   and five more digits.
8. Press the record number in that row. The report's own form opens, headed
   **Case morbidity report**, and its header bar reads **CASTILLO, ROSA** for
   the patient, **Hepatitis A** for the disease and **Salt Lake County** for
   the agency.

## What you should see

A laboratory message that became a case with no one reviewing it:

- The message on the **Staged messages** queue, filtered to **assigned**, with
  no exception against it.
- Its own screen, saying it is already a case and offering nothing to
  promote.
- The case it opened for a person who was not on file, found on the
  **Events** screen and opened from its record number, investigated by the
  agency its patient's zip code maps to.

You now know where a message Survsys assigned by itself goes, and how to find
what it became. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because the
promise this document makes is that it is performable exactly as written.
