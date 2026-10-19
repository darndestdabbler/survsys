# Promote a message to a case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/PromoteAMessageToACaseTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, I open a staged lab report that is queued for
> review, find the person it is about, confirm the disease, the investigating
> agency and the date it was first reported, and promote it — so that a case
> morbidity report is opened from what I confirmed, and the notification the
> promotion raises names the case.

This is the human-reviewed half of the legacy message pipeline's assignment,
and it is inferred from the EMSA intake EpiTrax sat behind:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the message pipeline | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Assignment* and *Delivery* | A reviewed message turned into a case: the pipeline's create-new-CMR decision, and the notification raised when a case is opened. |

Three differences from the legacy pipeline are deliberate, and this
walkthrough describes what Survsys does rather than what EMSA did:

- **A person confirms the decision; the pipeline does not create a case on its
  own.** The legacy rules engine decided create-new against update-existing
  automatically. Survsys opens a case only from what a reviewer confirms on the
  **Promote to a case** form: the person, the disease and the investigating
  agency.
- **The case is opened the one way every case is.** A promoted report is
  assigned a record number the same way a report keyed in directly is, so a
  promoted case is a case morbidity report like any other and opens on the same
  report form.
- **The notification is a line read back, not a message sent.** Survsys has no
  outbound notification transport yet, so the promotion reports the
  notification it raised as a line on the screen rather than delivering it.

Finding that a message is already a case, and not opening a second one, is its
own walkthrough, *Resolve a duplicate*.

## Before you start

You need all six of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The person, the disease and the staged message this walkthrough names, in
   that database.** `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds
   Listeriosis, `Survsys.IntegrationTests/Fixtures/persons.sql` seeds Chidi
   Okafor, and `Survsys.IntegrationTests/Fixtures/staged-messages.sql` seeds
   the queued message `elr-promote-example.hl7`; run the three against your
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

This walkthrough **promotes one message and opens one case**. The message
`elr-promote-example.hl7` is left promoted, and a case morbidity report on
Chidi Okafor for Listeriosis is left on file. Perform it a second time against
the same message and Survsys shows the message is already a case and offers no
second promotion, which is the *Resolve a duplicate* walkthrough; the
end-to-end suite reseeds the queue on every run, so a run always begins with
the message queued.

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
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Staged messages**.
2. Open the **Review state** list and choose **Queued for review**. The list
   closes, and the **Review state** box now reads **Queued for review**. In
   the table, find the row whose **Source** reads **elr-promote-example.hl7**.
3. In that row, press the **elr-promote-example.hl7** link in the **Source**
   column.
4. The browser tab reads **Staged message — Survsys**, and the heading reads
   **Staged message** followed by the message's id. Below the message's
   summary and its raw text is the heading **Promote to a case** and, under
   it, a person search.
5. In the box labelled **Last name**, type `Okafor`, then press **Search**.
   The one row's **Last, First** reads **Okafor, Chidi**. **Note the number**
   in that row's **ID** column.
6. Press **Use this person** on that row. A panel appears, and its first line
   reads **Reporting on Okafor, Chidi (person** followed by the ID you noted
   and **)**.
7. In the box labelled **First reported to public health**, enter
   `2026-03-12`.
8. Open the **Disease** list and choose **Listeriosis**. The list closes, and
   the **Disease** box now reads **Listeriosis**.
9. Open the **Investigating agency** list and choose **Utah State**. The list
   closes, and the **Investigating agency** box now reads **Utah State**.
10. Press **Promote to a case**.
11. A green panel replaces the form. Its first line reads **Case** followed by
    the new record number — nine digits beginning with the current year — and
    **was created for Listeriosis; a notification has been raised to public
    health.** Below it is a link reading **Open case** followed by that same
    record number. **Note the record number.**
12. Press the **Open case** link. The browser goes to the new report's form.
    In the header bar, **Patient name** reads **Okafor, Chidi**, **Disease**
    reads **Listeriosis**, **Investigating agency** reads **Utah State**, and
    **Record #** is the record number you noted.

## What you should see

A staged message turned into a case morbidity report by a reviewer:

- The queued message `elr-promote-example.hl7`, opened to its **Promote to a
  case** form.
- After confirming **Okafor, Chidi**, **Listeriosis** and **Utah State** and
  pressing **Promote to a case**, the message **Case … was created for
  Listeriosis; a notification has been raised to public health.** and a link
  to the new case.
- The new report's form, its header reading **Okafor, Chidi**, **Listeriosis**
  and **Utah State**, with the record number from the notification.

You now know how a reviewer turns a staged lab report into a case. If what you
saw differs from what is written above, the walkthrough has failed: say so
rather than adjusting what you did, because the promise this document makes is
that it is performable exactly as written.
