# Assign and accept a case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/AssignAndAcceptACaseTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a state epidemiologist, I assign a case morbidity report to the local
> health department that will investigate it; and as that department's
> investigator, I accept the case into my queue — so that the case is in the
> hands of the agency responsible for it, and that agency has said so.

This is the start of the legacy workflow — a case entered into a Local
Health Department's queue, and the department accepting it — and it is
inferred from three legacy sources, all read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **header bar** | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *Header Bar (persistent across tabs)*, and the screenshots `../nedss/docs/screenshots/session-010/01-workflow-start.png` and `02-after-accept-event.png` | A **Workflow status** field reading **Assigned to LHD** on a report just opened, and an **Accept Event** button beside the save buttons, which moves it on. |
| EpiTrax → the CMR edit form → **Workflow Options** menu | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *5. Workflow Transitions*, and the screenshots `../nedss/docs/screenshots/session-010/02-workflow-options-menu.png` and `04-workflow-options-after-accept.png` | The state machine as observed: a case starts at `assigned_to_lhd`, **Accept Event** takes it to the accepted state, and **Route to LHD** puts it into a department's queue at the start again. |
| EpiTrax → **Events list** | `../nedss/docs/functional/epitrax-core-workflows.md`, *2. Events List* | The legacy case list, filtered by **Investigating agency** and by **Queues**: the screen on which a department finds the cases that are its own. |

Five differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **Assigning is a form on the Agency board, naming the report by its id.**
  The legacy routed a case to a department from the report's own *Workflow
  Options* menu. Survsys has an **Assign a report** panel on the **Agency
  board**, which takes the report's id — the number at the end of the
  report's address, after `/event/edit/` — and the agency to assign it to.
  The record number is in the report's header, where the legacy shows it
  too; the id is in the address only.
- **Accepting is a button on the case's row in the Work queue**, not the
  **Accept Event** button in the report's header bar. The report form
  carries no workflow status and no workflow buttons yet; the queue screens
  are where the workflow is read and moved.
- **A queue belongs to an agency, not to a person.** Survsys has agencies
  and no users yet, so nobody signs in: the state user and the department's
  investigator are the same browser choosing different agencies from a
  list. The **Work queue** shows the cases an agency investigates, and the
  **Agency board** shows its whole queue, including cases shared read-only
  to it.
- **The workflow has four states**: **Assigned to LHD**, **Accepted**,
  **Under investigation** and **Closed**, in that order, and each row offers
  the one move forward it is ready for. The legacy's thirteen states, its
  *Do Not Investigate*, *Assign to Investigator* and reopening moves, and
  its *Route to LHD* from a later state are not there.
- **The Work queue is one agency's cases, not the legacy Events list.** It
  has no saved views and no filters: it lists what the chosen agency
  investigates, fifty to a page, with the record number, disease, workflow
  state, case status, onset date and whether the case is shared.

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
   conditions, Listeriosis among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Chidi Okafor among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself; a database you brought
   up by hand does not have them until you run both. The sixteen
   investigating agencies — Utah State and Davis County among them — are
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

This walkthrough **creates a report and leaves it in Davis County's queue,
accepted**. Perform it twice and you have two reports on Chidi Okafor, both
accepted in that queue, with consecutive record numbers; that is what the
legacy system does too, and nothing here prevents it. Every step that reads
the queue finds the report by the record number you noted, so other reports
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
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. In the box labelled **Last name**, type `Okafor`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Okafor, Chidi**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Okafor,
   Chidi**.
6. Open the **Disease** list and choose **Listeriosis**. The list closes,
   and the **Disease** box now reads **Listeriosis**.
7. Open the **Investigating agency** list and choose **Utah State**. The
   list closes, and the **Investigating agency** box now reads **Utah
   State**. The state opens the report; the department that will
   investigate it is named in step 12.
8. Press **Save & Continue**.
9. The report now exists, and the screen is its own form. In the header bar,
   **Patient name** reads **Okafor, Chidi**, **Disease** reads
   **Listeriosis**, **Investigating agency** reads **Utah State** and
   **Record #** is nine digits beginning with the current year. **Note two
   numbers**: the record number, which steps 13 to 17 find the case by, and
   the report's id, which is the number at the end of the address in your
   browser's address bar, after `/event/edit/` — step 12 needs it.
10. In the application bar, press **Agency board**. The browser tab reads
    **Agency board — Survsys**, and below the application bar is the heading
    **Agency board**. Under the **Agency** list, a line reads **Choose an
    agency to see its work queue.**, and below that are two panels, headed
    **Assign a report** and **Share a report read-only**.
11. Open the **Agency** list and choose **Davis County**. The list closes,
    and the **Agency** box now reads **Davis County**. The board below it
    now shows Davis County's queue, and no row in it carries the record
    number you noted: the report is not Davis County's yet.
12. In the **Assign a report** panel, type the report's id — the number you
    noted from the address in step 9 — in the box labelled **Report id**.
    Open the **Assign to agency** list and choose **Davis County**. The list
    closes, and the **Assign to agency** box now reads **Davis County**.
13. Press **Assign**. A green message appears at the top of the panel,
    reading **Report** followed by the record number and **is assigned, at
    the assigned-to-LHD start.** The board above now has a row carrying the
    record number, and it reads, left to right: the record number,
    **Listeriosis**, **Assigned to LHD**, **Unknown**, today's date written
    as `yyyy-mm-dd`, and **Investigating**. The date is the case's onset,
    which is the day the report was opened, as the *Create a case morbidity
    report* walkthrough says of the event date; **Unknown** is the case
    status nobody has set.
14. In the application bar, press **Work queue**. The browser tab reads
    **Work queue — Survsys**, and below the application bar is the heading
    **Work queue**. Under the **Agency** list, a line reads **Choose an
    agency to see its work queue.**
15. Open the **Agency** list and choose **Davis County**. The list closes,
    and the **Agency** box now reads **Davis County**. The table below lists
    the cases Davis County is investigating, and the row carrying the record
    number reads, left to right: the record number, **Listeriosis**,
    **Assigned to LHD**, **Unknown**, today's date, **Investigating**, and
    in the **Action** column a button reading **Accept**.
16. Press **Accept** on that row. A green message appears above the table,
    reading **Report** followed by the record number and **moved to
    Accepted.** In the row, **Workflow** now reads **Accepted**, and the
    button in the **Action** column now reads **Begin investigation**.
17. In the application bar, press **Agency board**, open the **Agency** list
    and choose **Davis County** again, so that the queue is read afresh from
    the database rather than from what you just pressed. The row carrying
    the record number reads **Accepted** under **Workflow**. That is how
    you know the acceptance reached the database.

## What you should see

A case morbidity report opened by the state, assigned to Davis County and
accepted there:

- A report on **Okafor, Chidi** for **Listeriosis**, investigated by **Utah
  State**, with a nine-digit record number in its header and its id in its
  address.
- On the **Agency board**, after **Assign**, the message **Report … is
  assigned, at the assigned-to-LHD start.** and the case on Davis County's
  board at **Assigned to LHD**.
- On the **Work queue** for Davis County, the case's row offering
  **Accept**; after pressing it, the message **Report … moved to
  Accepted.**, the row reading **Accepted**, and its button now offering
  **Begin investigation**.
- The Agency board, read again, showing the case at **Accepted**.

You now know how a case reaches the agency responsible for it, and how that
agency takes it on. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because
the promise this document makes is that it is performable exactly as
written.
