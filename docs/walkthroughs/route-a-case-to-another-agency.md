# Route a case to another agency

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RouteACaseToAnotherAgencyTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a state user, I route a case morbidity report from the local health
> department it was assigned to over to the department that should
> investigate it, with a note saying why, so that the right department has
> the case in its work queue, and anyone who opens the report reads who moved
> it, when, and why.

Routing a case is inferred from the legacy EpiTrax workflow, read-only here
and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Workflow Options** → **Route to LHD** | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *5. Workflow Transitions* — *Workflow state machine (observed)* and *Available workflow actions by state* | A case assigned to a local health department may be routed to another one, and stays assigned to an LHD; the move is offered alongside Accept Event and Do Not Investigate. |
| EpiTrax → the CMR header bar | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *CMR Edit Form Structure*, *Header Bar* | The report's **Workflow status** and **Investigating Agency**, shown on every tab. |

Two differences from the legacy system are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The Workflow Options are a panel, not a menu.** Above the report's tabs,
  the **Workflow Options** panel shows the workflow status and one button per
  move the case's state and your privileges allow. Routing takes the agency
  from the **Route to agency** list beside the buttons, and any move takes the
  **Note** typed beside them.
- **Every move is kept.** The report's **Administrative** tab carries a
  **Workflow history**, newest first, with who made each move, when, what it
  was, the state it moved from and to, and its note.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person the case is opened on, **Nguyen, An**, is one of the seven the
   people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`), and
   the condition, **Malaria**, is one of the seven the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`); run both against your
   database if you brought it up by hand. The sixteen investigating agencies
   are built into the API tier and need no seeding.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege**, routing among
   them. On a fresh database the only user is the administrator the API tier
   creates on its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs; the
   end-to-end suite names that user `administrator`, which is the name the
   workflow history shows. Open the application tier's address, sign in, then
   begin at step 1.

This walkthrough **opens a case and routes it**. Perform it twice and you have
two cases on An Nguyen, each routed once. The end-to-end suite empties the
cases on every run, so a run always begins with none.

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
2. In the box labelled **Last name**, type `Nguyen`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row reads **Nguyen, An**. Press **Create CMR** on that row.
4. Under the heading, the line begins **Step 2 of 2. Reporting on Nguyen,
   An**.
5. Open the **Disease** list and choose **Malaria**.
6. Open the **Investigating agency** list and choose **Davis County**, then
   press **Save & Continue**.
7. The report's own form opens. In the header bar, **Patient name** reads
   **Nguyen, An**, **Disease** reads **Malaria** and **Investigating agency**
   reads **Davis County**. Note the address in your browser's address bar: it
   ends in `/event/edit/` and a number, and step 12 comes back to it.
8. Press **Edit** at the top of the report, which opens it for editing. In the
   panel headed **Workflow Options**, **Workflow status** reads **Assigned to
   LHD**, and among the buttons under it is **Route to agency**, with a
   **Route to agency** list and a **Note** box above them.
9. In the box labelled **Note**, type `Patient lives in Utah County`. Open
   the **Route to agency** list and choose **Utah County**.
10. Press the **Route to agency** button. A green message in the panel reads
    **Route to agency was made: the report is now Assigned to LHD.**, the
    header bar's **Investigating agency** now reads **Utah County**, and the
    **Note** box is empty again.
11. Press the **Administrative** tab. Under the heading **Workflow history**
    is a table with the columns **When**, **Who**, **Move**, **From**, **To**
    and **Note**, and its newest row — the top one — reads, left to right: the
    date and time you routed the case as `yyyy-mm-dd hh:mm`, the user you
    signed in as, **Route to agency**, **Assigned to LHD**, **Assigned to
    LHD** and **Patient lives in Utah County**.
12. Go back to the report's address — the one you noted in step 7 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database. The header bar's **Investigating agency** reads **Utah
    County**. Press the **Administrative** tab: the **Workflow history** has
    the same top row as in step 11. That is how you know the move reached the
    database.

## What you should see

A case routed from one local health department to another, with the move on
record:

- A case on **Nguyen, An** for **Malaria**, opened under **Davis County** and
  assigned to that LHD.
- After **Route to agency** with **Utah County** chosen, the case still
  **Assigned to LHD**, now under **Utah County**.
- The move in the **Workflow history** on the **Administrative** tab: who made
  it, when, **Route to agency** from **Assigned to LHD** to **Assigned to
  LHD**, and the note **Patient lives in Utah County**, read back from the
  database.

You now know how to route a case to the department that should investigate
it. If what you saw differs from what is written above, the walkthrough has
failed: say so rather than adjusting what you did, because the promise this
document makes is that it is performable exactly as written.
