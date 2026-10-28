# Close an investigation

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/CloseAnInvestigationTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a local health department investigator, I take an accepted case through
> its investigation and close it — so that the case's record says the
> investigation happened and is finished, and the queue no longer offers
> anything to do with it.

This is the rest of the legacy workflow — from a case accepted by a Local
Health Department, through its investigation, to closed — and it is inferred
from two legacy sources, both read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Workflow Options** menu | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *5. Workflow Transitions*, and the screenshots `../nedss/docs/screenshots/session-010/02-workflow-options-menu.png` and `04-workflow-options-after-accept.png` | The state-dependent menu a case is moved on from, and the table of every workflow state the legacy database holds: `assigned_to_lhd` as the start, `under_investigation` as the active investigation, and `closed` as the case closed. |
| EpiTrax → the CMR edit form → **header bar** | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *Header Bar (persistent across tabs)*, and the screenshot `../nedss/docs/screenshots/session-010/02-after-accept-event.png` | The **Workflow status** field, which the legacy shows on the report itself, and the **Investigating Agency** field beside it. |

**The case is assigned and accepted first.** A case is closed from the
queue of the agency investigating it, so this walkthrough begins the way
*Assign and accept a case* does — a report opened by the state in the two
steps, assigned to a department on the Agency board, and accepted from that
department's Work queue — and then carries it on to closed. Steps 1 to 15
are that walkthrough's steps over again, on a different person, disease and
department, so that the two walkthroughs leave two different queues and
never read each other's rows.

Four differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The investigation is two presses on the case's row in the Work queue**:
  **Begin investigation**, which moves the case to **Under investigation**,
  and **Close**, which moves it to **Closed**. The legacy moved a case on
  from the report's *Workflow Options* menu; the Survsys report form carries
  no workflow status and no workflow buttons yet.
- **Closed is the end.** A closed case's row offers no button at all; there
  is no reopening, by a manager or by the state, and no *investigation
  complete* or *approved by LHD* state on the way. The legacy's thirteen
  states are four here: **Assigned to LHD**, **Accepted**, **Under
  investigation** and **Closed**, each row offering the one move forward it
  is ready for.
- **A closed case stays in its agency's queue**, at **Closed**, and its
  report stays where it was. The queue does not hide it, and the report's
  address opens it as before.
- **Assigning names the investigating agency.** Once the state assigns the
  report to a department, the report's header reads that department under
  **Investigating agency**, where it read the state before: the agency that
  investigates a case is the agency it is assigned to, and the two are one
  field.

The **Share a report read-only** panel on the Agency board, and the sharing
it does, are not part of this walkthrough.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The person and the disease this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Lyme disease among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Jane Abbott among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself; a database you brought
   up by hand does not have them until you run both. The sixteen
   investigating agencies — Utah State and Summit County among them — are
   built into the API tier and need no seeding.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>. On a deployed site it is the
   address of the Survsys web site, which your deployment supplies.
5. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do.
   On a fresh database the only user is the administrator the API tier
   creates on its first start from two settings you choose:
   `Survsys__Administrator__UserName` and `Survsys__Administrator__Password`,
   set where `Survsys.Api` runs. That administrator holds the Administrator
   role, which has every privilege. Open the application tier's address, type
   that user name and password on the **Sign in** page, and press **Sign
   in**; then begin at step 1.

This walkthrough **creates a report and leaves it closed in Summit County's
queue**. Perform it twice and you have two reports on Jane Abbott, both
closed there, with consecutive record numbers; that is what the legacy
system does too, and nothing here prevents it. Every step that reads the
queue finds the report by the record number you noted, so other reports
already in the queue do not change what you read. It changes nothing about
the person, so the *Find a person* walkthrough reads the same seven people
before and after it. The end-to-end suite empties the reports along with the
people it reseeds on every run, so a run always begins with none.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, seeds the conditions and
the people, and performs the steps below in a real browser. It records video
of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. In the box labelled **Last name**, type `Abbott`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Abbott, Jane**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Abbott,
   Jane**.
6. Open the **Disease** list and choose **Lyme disease**. The list closes,
   and the **Disease** box now reads **Lyme disease**.
7. Open the **Investigating agency** list and choose **Utah State**. The
   list closes, and the **Investigating agency** box now reads **Utah
   State**.
8. Press **Save & Continue**.
9. The report now exists, and the screen is its own form. In the header bar,
   **Patient name** reads **Abbott, Jane**, **Disease** reads **Lyme
   disease**, **Investigating agency** reads **Utah State** and **Record #**
   is nine digits beginning with the current year. **Note three things**:
   the record number, which every queue step finds the case by; the
   report's id, which is the number at the end of the address in your
   browser's address bar, after `/event/edit/`, and which step 11 needs; and
   the address itself, which step 18 comes back to.
10. In the side menu, under **Cases**, choose **Agency board**. Below the application
    bar is the heading **Agency board**. Open the **Agency** list and choose
    **Summit County**. The list closes, and the **Agency** box now reads
    **Summit County**. The board below shows Summit County's queue, and no
    row in it carries the record number you noted.
11. In the **Assign a report** panel, type the report's id in the box
    labelled **Report id**. Open the **Assign to agency** list and choose
    **Summit County**. The list closes, and the **Assign to agency** box now
    reads **Summit County**.
12. Press **Assign**. A green message appears at the top of the panel,
    reading **Report** followed by the record number and **is assigned, at
    the assigned-to-LHD start.** The board above now has a row carrying the
    record number, and it reads, left to right: the record number, **Lyme
    disease**, **Assigned to LHD**, **Unknown**, today's date written as
    `yyyy-mm-dd`, and **Investigating**.
13. In the side menu, under **Cases**, choose **Work queue**. Below the application bar
    is the heading **Work queue**. Open the **Agency** list and choose
    **Summit County**. The list closes, and the **Agency** box now reads
    **Summit County**.
14. The table below lists the cases Summit County is investigating, and the
    row carrying the record number reads, left to right: the record number,
    **Lyme disease**, **Assigned to LHD**, **Unknown**, today's date,
    **Investigating**, and in the **Action** column a button reading
    **Accept**.
15. Press **Accept** on that row. A green message appears above the table,
    reading **Report** followed by the record number and **moved to
    Accepted.** In the row, **Workflow** now reads **Accepted**, and the
    button in the **Action** column now reads **Begin investigation**.
16. Press **Begin investigation** on that row. The green message now reads
    **Report** followed by the record number and **moved to Under
    investigation.** In the row, **Workflow** now reads **Under
    investigation**, and the button in the **Action** column now reads
    **Close**.
17. Press **Close** on that row. The green message now reads **Report**
    followed by the record number and **moved to Closed.** In the row,
    **Workflow** now reads **Closed**, and the **Action** column is empty:
    the row offers no button, because there is nothing further to do with a
    closed case. The row is still in the table.
18. Go back to the report's address — the one you noted in step 9 — by
    entering it in the address bar again, so that the report is read afresh
    from the database. The report form opens as before: **Patient name**
    reads **Abbott, Jane**, **Disease** reads **Lyme disease**, **Record #**
    is the record number you noted, and **Investigating agency** now reads
    **Summit County** — the department the state assigned it to in step 12.
    That is how you know the case and what was done to it reached the
    database: the report is still there, closed in the queue of the
    department that investigated it.

## What you should see

A case taken from opened to closed, and still on file afterwards:

- A report on **Abbott, Jane** for **Lyme disease**, opened by **Utah
  State**, assigned to **Summit County** on the Agency board and accepted
  from Summit County's Work queue.
- On the same row, **Begin investigation** answering **Report … moved to
  Under investigation.** and **Close** answering **Report … moved to
  Closed.**
- The row reading **Closed**, with nothing in its **Action** column.
- The report's address opening the report again, headed by the same record
  number and now investigated by **Summit County**.

You now know how a case runs through its investigation and is closed, and
that closing it loses nothing. If what you saw differs from what is written
above, the walkthrough has failed: say so rather than adjusting what you did,
because the promise this document makes is that it is performable exactly as
written.
