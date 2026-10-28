# Resolve a duplicate

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ResolveADuplicateTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I promote a staged lab report to a case; and
> when I come back to the same message, Survsys shows me it is already a case
> and offers me no way to promote it again — so that I do not open a second
> case for a report that already has one.

This is the duplicate guard Survsys ships, and it is the reviewed-pipeline
half of what the legacy system did to avoid double-entering a lab report:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the message pipeline | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Assignment* | A message checked against what already exists before a case is created, so a report already on file is not entered twice. |

**What Survsys ships here, and only this**, is a guard on the message: a
message that has already been promoted carries the terminal review state, and
the review screen shows it is already a case and offers no promote form. The
application tier refuses a second promotion of the same message with a 409,
and the screen never asks for one. What this phase does **not** ship, and this
walkthrough does **not** describe, is the legacy's wider deduplication: there
is no time-windowed graylist matching an inbound message against existing
events, no action to discard a message, and no action to merge one into an
existing case. The one duplicate this walkthrough resolves is the same message
promoted twice.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The person, the disease and the staged message this walkthrough names, in
   that database.** `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds
   Hepatitis A, `Survsys.IntegrationTests/Fixtures/persons.sql` seeds Paul
   Zimmerman, and `Survsys.IntegrationTests/Fixtures/staged-messages.sql` seeds
   the queued message `elr-duplicate-example.hl7`; run the three against your
   database, conditions first. The end-to-end suite seeds all three itself. The
   sixteen investigating agencies — Utah State among them — are built into the
   API tier and need no seeding.
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

This walkthrough **promotes one message and opens one case**, and leaves the
message `elr-duplicate-example.hl7` promoted and one case morbidity report on
Paul Zimmerman for Hepatitis A on file. It opens no second case, which is the
whole point. The end-to-end suite reseeds the queue on every run, so a run
always begins with the message queued.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, seeds the conditions, the
people and the staged messages, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Intake**, choose **Staged messages**.
2. Open the **Review state** list and choose **Queued for review**. In the
   table, find the row whose **Source** reads **elr-duplicate-example.hl7**,
   and press the **elr-duplicate-example.hl7** link in its **Source** column.
3. Below the message's summary and its raw text is the heading **Promote to a
   case** and a person search. In the box labelled **Last name**, type
   `Zimmerman`, then press **Search**. On the one row, whose **Last, First**
   reads **Zimmerman, Paul**, press **Use this person**.
4. Open the **Disease** list and choose **Hepatitis A**, and open the
   **Investigating agency** list and choose **Utah State**. Then press
   **Promote to a case**.
5. A green panel replaces the form. Its first line reads **Case** followed by
   the new record number and **was created for Hepatitis A; a notification has
   been raised to public health.**, and below it is a link reading **Open
   case** followed by that record number. **Note the record number** — this is
   the one case.
6. In the side menu, under **Intake**, choose **Staged messages**. The **Review state**
   list is back on **All**. In the table, find the row whose **Source** reads
   **elr-duplicate-example.hl7** again: there is still just the one, and its
   **Review state** now reads **assigned** — the message has left the review
   queue because it is a case.
7. Press the **elr-duplicate-example.hl7** link in that row's **Source**
   column to open the message again.
8. The review screen no longer shows a **Promote to a case** form, a disease
   or agency list, or a **Promote to a case** button. In their place is a
   notice reading **This message has already been promoted to a case.** There
   is no control here that opens a second case, because the message already has
   one — the record number you noted in step 5, and no other.

## What you should see

The message promoted once, and no way to promote it twice:

- After **Promote to a case**, the message **Case … was created for Hepatitis
  A; a notification has been raised to public health.** and the one record
  number.
- The queue still carrying the single `elr-duplicate-example.hl7` row, now in
  the **assigned** state.
- The message reopened, showing **This message has already been promoted to a
  case.** and offering no promote form and no promote button, so a second case
  cannot be opened from it.

You now know how Survsys keeps a reviewer from opening a second case for a
message that already has one. If what you saw differs from what is written
above, the walkthrough has failed: say so rather than adjusting what you did,
because the promise this document makes is that it is performable exactly as
written.
