# Review a staged message

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ReviewAStagedMessageTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I open the queue of inbound electronic lab
> reports waiting for review, narrow it to the ones held as exceptions, and
> open one to read its whole record — where it came from, when it arrived,
> what state it is in, whether it is flagged for quality assurance, why it was
> held, and the raw message exactly as the lab sent it — so that I can decide
> what to do with it from what it actually says.

This is the reviewer's half of the legacy message pipeline, and it is inferred
from the EMSA intake EpiTrax sat behind:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the message pipeline | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Intake* and the queue states it dumps on `system_messages` | Inbound lab messages queued for review, each in a review state — queued for review, held for review, or held as an exception — and carrying the raw message it arrived as. |

Two differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EMSA showed:

- **The queue is one screen with a review-state filter, not a queue per
  state.** The legacy moved a message between numbered queues; Survsys has one
  **Staged messages** screen and a **Review state** filter that narrows it to
  **Queued for review**, **Held for review** or **Exception**, or shows the
  whole queue on **All**.
- **Quality assurance is a flag on the message, not a queue of its own.** The
  legacy had a QA queue; Survsys shows a **QA** column that reads **Flagged**
  for a message raised for quality-assurance review and a dash for one that is
  not.

Promoting a message to a case is its own walkthrough, *Promote a message to a
case*; this one reads a message and changes nothing.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The staged messages this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/staged-messages.sql` seeds the four
   messages — `elr-exception-example.hl7` held as an exception,
   `elr-qa-example.hl7` flagged for quality assurance, and two queued for
   review — into the `system_messages` table; run it against your database.
   The end-to-end suite seeds it itself; a database you brought up by hand does
   not have the messages until you run it. On a live deployment these rows
   arrive instead from the ELR intake, which stages a dropped lab file into the
   same queue.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
5. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe: open the application tier's address, sign in
   as the administrator the API tier creates on its first start, and then
   begin at step 1.

This walkthrough **reads and changes nothing**. Perform it as many times as
you like and the queue is the same before and after it, because it promotes no
message and raises no flag.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, seeds the messages, and
performs the steps below in a real browser. It records video of every test it
runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Staged messages**.
2. The browser tab reads **Staged messages — Survsys**, and below the
   application bar is the heading **Staged messages**. Under it a line reads
   **The inbound electronic lab reports waiting for review. Filter by review
   state, or leave it on All to see the whole queue.** Below that are a
   **Review state** list, set to **All**, and a table whose columns read, left
   to right, **Source**, **Received**, **Review state**, **QA** and
   **Exception**.
3. In the table, the row whose **Source** reads **elr-exception-example.hl7**
   reads **Exception** under **Review state**, a dash under **QA**, and **The
   message could not be parsed as HL7 v2.** under **Exception**. The row whose
   **Source** reads **elr-qa-example.hl7** reads **Flagged** under **QA**:
   the quality-assurance flag, read in the queue.
4. Open the **Review state** list and choose **Exception**. The list closes,
   and the **Review state** box now reads **Exception**. The table now shows
   only the messages held as exceptions: one row, whose **Source** reads
   **elr-exception-example.hl7**.
5. In that row, press the **elr-exception-example.hl7** link in the **Source**
   column.
6. The browser tab reads **Staged message — Survsys**, and the heading reads
   **Staged message** followed by the message's id — the number at the end of
   the address in your browser's address bar, after `/staged-messages/`. Below
   the heading is a **Back to the queue** button.
7. Read the message's summary. **Source** reads **elr-exception-example.hl7**,
   **Received** reads **2026-10-15 07:45**, **Review state** reads
   **Exception**, **QA** reads **Not flagged**, and **Exception** reads **The
   message could not be parsed as HL7 v2.**
8. Below the summary, under the heading **Raw message**, is the message
   exactly as it arrived. It begins **GARBLED|this is not|an HL7 v2
   message|no MSH segment here** — which is why it was held as an exception:
   it has no MSH segment, so it is not an HL7 v2 message, and there is nothing
   to promote.
9. Press **Back to the queue**. Open the **Review state** list and choose
   **Queued for review**. The list closes, the **Review state** box now reads
   **Queued for review**, and the table shows the messages still waiting for a
   reviewer. The row whose **Source** reads **elr-qa-example.hl7** reads
   **Queued for review** under **Review state** and **Flagged** under **QA**.

## What you should see

A reviewer reading one staged message's whole record, and the queue surfacing
what matters about the rest:

- The **Staged messages** queue, filtered to **Exception**, showing the one
  message held as an exception.
- Its review screen reading back its source, its received time, its review
  state, that it is **Not flagged**, and the parser's account of why it was
  held, with the raw message it arrived as below.
- The queue, filtered to **Queued for review**, showing the quality-assurance
  flag on **elr-qa-example.hl7** as **Flagged**.

You now know how a reviewer finds a staged message and reads everything the
queue holds about it. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because the
promise this document makes is that it is performable exactly as written.
