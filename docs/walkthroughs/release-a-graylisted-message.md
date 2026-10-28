# Release a graylisted message

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ReleaseAGraylistedMessageTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, when a laboratory reports a result that on its
> own does not open a case, I find it waiting on the graylist rather than
> lost, and once an investigator opens a case for that patient and disease I
> find the result released to me with the case it belongs to, so that a
> result that arrived early still reaches the case.

This is the graylist of the legacy message pipeline, and it is inferred from
the EMSA processing EpiTrax sat behind:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the assignment decision | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Stage 3: Assignment Decision*, *3.1 addCmrProcess*, step 3 | A message whose case-management rule allows no new event (`CMR_NO`) is graylisted. |
| EMSA → graylist processing | The same document, *Stage 5: Post-Assignment Processing*, *Graylist Processing* and its *Automation Channels* | When a new event appears, the Graylist Request Spooler matches waiting graylisted messages to it by condition and event date, and returns them for processing. |

Two differences from the legacy are deliberate, and this walkthrough
describes what Survsys does rather than what EMSA did:

- **A released message comes back to a reviewer, not straight onto the
  case.** EMSA returned it to the entry queue for automatic processing;
  Survsys moves it to **Held for review** with the case it matched named in
  its **Exception** column, and a reviewer decides from there.
- **The match is drawn around the day the message arrived when it carries no
  collection date.** The graylist matches a case whose event date is within
  30 days either side of the message's collection date; this walkthrough's
  message carries none, so its received date stands in, and a case opened
  today matches it whatever day you perform this.

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
   read; the second turns on the assignment pass, which also does the
   graylist's releasing, and which is off unless it is set.
3. **The reference data and the person this walkthrough starts from, in that
   database.** Run `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql` and then
   `Survsys.E2ETests/Fixtures/elr-intake.sql` against it. The last puts the
   sending lab **ARUP_LAB** on file with the vocabulary for its message,
   gives **Lyme disease** a case-management rule that opens no case for a
   positive result, and puts **Owen Brennan** on file with no case. The
   end-to-end suite seeds them itself, into a database of its own; a database
   you brought up by hand does not have them until you run them.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **adds** a case and a staged message. Performed a second time
over the same database, Owen Brennan already has the case the first run
opened, so the second message is released as soon as it is graylisted; start
again from a freshly seeded database to perform it again as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intake turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Copy the file `Survsys.E2ETests/Fixtures/Hl7/elr-graylist-example.hl7`
   into the drop directory. It is a Borrelia burgdorferi IgM result, positive,
   from **ARUP_LAB** for **OWEN BRENNAN**, born 1971-09-30, of 7 Canyon Road,
   84101 — the person on file — with no collection date. Within a few seconds
   the file is gone from the drop directory: the intake has read it, queued it
   and moved it into the directory's `.processed` folder.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Intake**, choose **Staged messages**. Open the
   **Review state** list and choose **Graylisted**.
3. The row whose **Source** reads **elr-graylist-example.hl7** reads
   **Graylisted** under **Review state**, and under **Exception**:
   **Graylisted: the condition's case-management rule opens no case for this
   result, so it waits for a case to attach to.** If the row is not there
   yet, the assignment pass has not run since the intake queued the message:
   choose **All** from the **Review state** list and then **Graylisted**
   again, which reads the queue afresh, until it is.
4. In the side menu, under **Cases**, choose **New CMR**. In the box labelled **Last
   name**, type `Brennan`, then press **Search**.
5. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row reads **Brennan, Owen**. Press **Create CMR** on that row. Under
   the heading, the line begins **Step 2 of 2. Reporting on Brennan, Owen**.
6. Open the **Disease** list and choose **Lyme disease**. Open the
   **Investigating agency** list and choose **Salt Lake County**, then press
   **Save & Continue**.
7. The report's own form opens. In the header bar, **Patient name** reads
   **Brennan, Owen** and **Disease** reads **Lyme disease**. Note the record
   number in the header bar: the released message names it.
8. In the side menu, under **Intake**, choose **Staged messages**. Open the **Review
   state** list and choose **Held for review**. The row whose **Source** reads
   **elr-graylist-example.hl7** reads **Held for review** under **Review
   state**, and under **Exception**: **Released from the graylist: case**,
   the record number you noted, **has appeared for this patient.** If the row
   is not there yet, the pass has not run since you saved the case: choose
   **All** and then **Held for review** again, until it is.
9. In that row, press the **elr-graylist-example.hl7** link in the **Source**
   column. The message's screen reads **Held for review** for **Review
   state** and the same sentence for **Exception**. Below the raw message,
   under **Promote to a case**, Survsys proposes the person it matched: the
   line begins **Proposed match: Brennan, Owen**. The message is a reviewer's
   again, with the case it belongs to named.

## What you should see

A laboratory result that waited for its case:

- The message on the **Staged messages** queue, filtered to **Graylisted**,
  with the reason it waits.
- A Lyme disease case opened for Owen Brennan by the ordinary **New CMR**
  screens.
- The same message, filtered to **Held for review**, released from the
  graylist and naming that case, with Owen Brennan proposed as its patient.

You now know how a graylisted message waits for a case and comes back when
one appears. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because the
promise this document makes is that it is performable exactly as written.
