# See a case shared read-only

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SeeACaseSharedReadOnlyTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator in one local health department, when a case another
> department investigates is shared with mine, I find it on my agency's
> board and open it, and see that I may read it and change nothing on it —
> so that a department can show another a case without handing it over.

It is inferred from the legacy documentation, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Options** menu | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *7. CMR Header Bar (persistent across tabs)* | **Agency Sharing** among the actions taken on a report from its own form. |
| The legacy feature matrix | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *1. Case Management*, the **Multi-agency access** row | Another agency given access to a case through `person_condition_agency_allow`, for read-only sharing. |

Three things this walkthrough describes as Survsys does them:

- **Sharing is a button on the case's row on the Agency board.** The legacy
  shared a case from the report's own **Options** menu. On the Survsys
  **Agency board**, each row carries a **Share** button that opens the
  choice of agency to share that report with; the row is found by its
  record number.
- **Read-only is enforced, not only shown.** A user who sees a case only
  because it is shared with their agency may read it and every record under
  it, and the API tier refuses every change to them. The report says so, and
  offers no **Edit** and no **Delete** — even to a user whose role holds
  `edit_cases`, as this walkthrough's does.
- **A user works for the agencies named on them.** The administrator holds
  `all_jurisdictions` and reaches every case, so this walkthrough adds a role
  and a user who works for Davis County alone, and signs in as them.

## Before you start

You need all five of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The person and the disease this walkthrough names, in that database.**
   Run `Survsys.IntegrationTests/Fixtures/conditions.sql` and then
   `Survsys.IntegrationTests/Fixtures/persons.sql` against it: they seed
   Listeriosis and Jane Abbott. The end-to-end suite seeds both itself.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
5. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **adds a role, a user and a report, and shares the report**.
Performed a second time over the same database, the role's name and the user
name are already taken; start again from a freshly seeded database to perform
it again as written. The end-to-end suite clears every role but the
administrator's and every user but the administrator on every run, along with
the reports and their shares.

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
   developer machine, <http://localhost:5000>. In the side menu, under **Administration**, choose **Roles**. The screen is headed
   **Roles**, and its form **New role**.
2. In the box labelled **Name**, type `Davis County investigator`. Tick
   **view_cases** and **edit_cases**, and nothing else, and press **Save
   role**. The table gains a row whose **Name** reads **Davis County
   investigator** and whose **Privileges** read **view_cases, edit_cases**.
3. In the side menu, under **Administration**, choose **Users**. The screen is headed **Users**,
   and its form **New user**. Type `davis.investigator` in **User name**,
   `Davis Investigator` in **Display name** and `davis-walkthrough-password`
   in **Password**. Leave **May sign in** ticked, tick **Davis County
   investigator**, and under **Agencies** tick **Davis County**, and nothing
   else. Press **Save user**. The table gains a row whose **User name** reads
   **davis.investigator**, **Roles** **Davis County investigator**,
   **Agencies** **Davis County** and **May sign in** **Yes**.
4. In the side menu, under **Cases**, choose **New CMR**. In the box labelled **Last
   name**, type `Abbott` and press **Search**; press **Create CMR** on the one
   row, **Abbott, Jane**. Open the **Disease** list and choose
   **Listeriosis**, open the **Investigating agency** list and choose **Salt
   Lake County**, and press **Save & Continue**. The screen is the report's
   own form. Note the number the header bar reads under **Record #**, which
   is its record number.
5. In the side menu, under **Cases**, choose **Agency board**. Open the
   **Agency** list and choose **Salt Lake County**. Its queue is newest
   record number first, so the row carrying the record number you noted is
   at the top. Press **Share** on that row. A panel opens below the board,
   headed **Share report** and the record number and **read-only**. Open its
   **Share with agency** list and choose **Davis County**, and press the
   panel's **Share**. A message reads **Report** and the record number **is
   shared read-only with Davis County.**
6. Press **Sign out** at the right of the application bar. The screen is the **Sign in**
   page. Type `davis.investigator` as the user name and
   `davis-walkthrough-password` as the password, and press **Sign in**. The
   application bar now reads **Davis Investigator** beside **Sign out**.
7. In the side menu, under **Cases**, choose **Agency board**. Open the **Agency** list
   and choose **Davis County**. The row whose **Record #** reads the record
   number you noted reads **Listeriosis** under **Disease**, and **Shared
   read-only** under **Sharing**.
8. Press the record number. The browser goes to the report's form, headed
   **Case morbidity report**. In the header bar, **Patient name** reads
   **Abbott, Jane**, **Disease** reads **Listeriosis** and **Investigating
   agency** reads **Salt Lake County**. Above the header a notice reads
   **This case is shared with your agency read-only: you may read it, and
   change nothing on it.**, and there is no **Edit** button and no **Delete**
   button — only **Print**.

## What you should see

A case one agency investigates, shared with another and read there:

- A role, **Davis County investigator**, holding **view_cases** and
  **edit_cases**, and a user, **davis.investigator**, holding it and working
  for **Davis County**.
- A Listeriosis report on **Abbott, Jane** under **Salt Lake County**, shared
  read-only with **Davis County**.
- Signed in as that user, the report on **Davis County**'s board marked
  **Shared read-only**, and on its own form the notice that it is shared with
  their agency read-only, with no **Edit** and no **Delete**.

You now know what a case shared with your agency looks like to you. If what
you saw differs from what is written above, the walkthrough has failed: say
so rather than adjusting what you did, because the promise this document
makes is that it is performable exactly as written.
