# Work a task list

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/WorkATaskListTests.cs`, and a step you cannot perform is
a defect in the application or in this document — never something to work
around.

## User story

> As an investigator, I raise the follow-up a case needs as tasks — the one
> interview, and the check that repeats every week until the case is done —
> and then work them off a list of everything due, so that nothing a case
> needs is remembered only by me.

This is the **Tasks** section of the legacy report form's **Notes** tab,
together with the list screen its **Show all tasks** filter implies, and it is
inferred from two legacy screens, read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Notes** tab | `../../../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Notes Tab → Layout*, and the screenshot `../../../nedss/docs/screenshots/session-012/16-notes-tab-initial.png` | The tab as three sections — **Attachments**, **Notes** and **Tasks** — with the Tasks section headed **Task Schedule** and offering **+ Add Task** and a **Show all tasks** filter. |
| The same tab, explored | the same document, *Tasks*, and `../../../nedss/docs/screenshots/session-012/17-notes-tab-explored.png` | What a task is made of: the `task` table's name, notes, priority, due date, status, category, user and report, and the three columns a repeat is held in — `repeating_interval`, `until_date` and `repeating_task_id`. |

**Two screens, one list.** A worker's tasks are not read a case at a time,
which is what the legacy **Show all tasks** filter is for. So the same list is
on two screens in Survsys: the **Tasks** section of a report's Notes tab,
which is that report's tasks and is where a task is added, and the **Tasks**
screen the application bar opens, which is every report's. This walkthrough
raises the tasks on the first and works them on the second.

Four differences from the legacy section are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **A task is added only where a report is in hand.** The **Add a task**
  panel is on the Notes tab, where the report is known; the Tasks screen is
  opened from the application bar and has no report, so it lists and
  completes but does not add. The panel opens inline rather than behind a
  **+ Add Task** button, as the note and attachment panels do.
- **A repeat is expanded once, into one dated task per date.** Give the task
  an interval and a day to repeat until, and the whole series is written
  then and there: one row per date, each carrying the interval and the until
  date so a reader of one can see it is one of several. Nothing re-expands a
  series afterwards and nothing runs on a clock — a task becomes due by its
  due date being read.
- **The filter offers the three choices the status vocabulary allows.**
  **To do**, **Done** and **All tasks**, which are the two statuses a task
  has and the absence of a filter. It opens on **To do**, because what a
  list of work is for is the work still to do. The **Status** column shows
  the code itself, `open` or `complete`, as every coded field in this
  release does.
- **A completed row stays on the screen until the list is read again.** Mark
  a task done while the list shows only the ones to do and the row remains,
  now reading `complete` and offering nothing: it was just acted on, and a
  row that vanished under the pointer would leave you wondering whether the
  click took. Change the filter, or open the screen again, and it is sorted
  into the status it now has.

The legacy section's task categories, and assigning a task to a named user,
are not part of this walkthrough: **Assigned to** is a box holding the legacy
user id, as every coded field in this release is, and there is no user
resource to choose from yet.

## Before you start

You need all four of these. The last one is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The person and the disease this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Malaria among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   among them the one on file under a last name alone — **Doe** — whom this
   walkthrough reports on; run the first and then the second against your
   database. The end-to-end suite seeds both itself; a database you brought
   up by hand does not have them until you run both. The sixteen
   investigating agencies are built into the API tier and need no seeding.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>. On a deployed site it is the
   address of the Survsys web site, which your deployment supplies.

This walkthrough **creates a report and raises four tasks on it**, one of
which it then completes. Perform it twice and you have two reports on Doe,
each with four tasks; that is what the legacy system does too, and nothing
here prevents it. It changes nothing about the person, so the *Find a person*
walkthrough reads the same seven people before and after it. The end-to-end
suite empties the tasks and the reports along with the people it reseeds on
every run, so a run always begins with none — which is why the **Tasks**
screen shows this walkthrough's four tasks and no others there.

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
2. In the box labelled **Last name**, type `Doe`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Doe** — that person is on file under a
   last name and nothing else.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Doe**.
6. Open the **Disease** list and choose **Malaria**. The list closes, and the
   **Disease** box now reads **Malaria**.
7. Open the **Investigating agency** list and choose **Salt Lake County**.
   The list closes, and the **Investigating agency** box now reads **Salt
   Lake County**.
8. Press **Save & Continue**. The report now exists, and the screen is its
   own form. In the header bar, **Patient name** reads **Doe** and
   **Disease** reads **Malaria**. Note the address in your browser's address
   bar: it ends in `/event/edit/` and a number — **that number is the
   report's**, and step 13 reads it again in a column of the task list.
9. Press the **Notes** tab and scroll to the bottom of it, past the
   **Attachments** and **Notes** sections, to the **Tasks** section. It is
   headed **Tasks**, with a **Show** box beside the heading reading **To
   do**, and under the heading it reads **No tasks found.** Below it is the
   **Add a task** panel: the boxes **Task**, **Due**, **Priority**,
   **Assigned to**, **Repeats** — reading **Not at all** — **Repeats
   until** and **Notes**, and an **Add task** button.
10. In the box labelled **Task**, type `Interview the patient`. In the box
    labelled **Due**, enter the date `2026-10-17`. In the box labelled
    **Priority**, type `High`. Leave the rest of the panel as it is, and
    press **Add task**. A green message appears, reading **The task has been
    added.**, and the **Tasks** section is now a table with the columns
    **Due**, **Task**, **Priority**, **Status**, **Schedule** and **Action**
    and one row reading, left to right: **2026-10-17**, **Interview the
    patient**, **High**, **open**, a dash for **Schedule**, and a
    **Complete** button.
11. In the box labelled **Task**, type `Weekly symptom check`. In the box
    labelled **Due**, enter the date `2026-10-19`. In the box labelled
    **Priority**, type `Routine`. Open the **Repeats** list and choose
    **Weekly**; the list closes and the **Repeats** box now reads
    **Weekly**. In the box labelled **Repeats until**, enter the date
    `2026-11-02`. Press **Add task**.
12. The green message now reads **The task has been added, as 3 dated
    tasks.** — the three Mondays from the 19th of October to the 2nd of
    November — and the **Tasks** table has four rows, soonest due first:
    **2026-10-17 Interview the patient**, then **Weekly symptom check** due
    **2026-10-19**, **2026-10-26** and **2026-11-02**. Each of the three
    reads **Routine**, **open**, and **weekly until 2026-11-02** under
    **Schedule**, and each offers a **Complete** button.
13. In the application bar, press **Tasks**. The screen that opens is headed
    **Tasks**, and under the heading a line explains that it holds the tasks
    raised against every case, soonest due first. Its table has the same
    columns with one more between **Schedule** and **Action**: **Report**.
    The four rows are the four you raised, in the same order, and each one's
    **Report** reads the report's number — the number the address in step 8
    ended with.
14. Press **Complete** on the **Interview the patient** row. A green message
    appears, reading **The task has been completed.**, that row's **Status**
    now reads **complete**, and its **Action** column is empty: a completed
    task offers nothing further. The row has not moved and the other three
    are untouched, each still reading **open** with a **Complete** button.
15. Open the **Show** list and choose **Done**. The list closes, the **Show**
    box reads **Done**, and the table is read again from the database: its
    one row is **2026-10-17 Interview the patient**, reading **complete**,
    and the three **Weekly symptom check** rows are gone, because they are
    not done.
16. Open the **Show** list and choose **To do**. The table is read again, and
    now it is the other way round: three rows, all three **Weekly symptom
    check**, due **2026-10-19**, **2026-10-26** and **2026-11-02**, and
    **Interview the patient** is not among them. That is how you know the
    completion reached the database rather than the screen alone.

## What you should see

Four tasks raised on Doe's case morbidity report and one of them worked off:

- A report on **Doe** for **Malaria**, opened in the two steps, with its
  **Notes** tab's **Tasks** section reading **No tasks found.** above an
  **Add a task** panel.
- **Interview the patient**, due **2026-10-17**, added as one task reading
  **open** with no schedule.
- **Weekly symptom check**, due **2026-10-19** and repeating weekly until
  **2026-11-02**, added as **3 dated tasks** — one row per Monday, each
  carrying the schedule it came from.
- The **Tasks** screen from the application bar listing all four with the
  report's own number beside each, soonest due first.
- **Complete** on the first answering **The task has been completed.**,
  leaving that row in place reading **complete** with nothing left to press.
- The **Show** filter sorting them afterwards: **Done** holds the one
  completed task alone, and **To do** holds the three that are not.

You now know how to raise the follow-up a case needs and work it off a list
of everything due. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because
the promise this document makes is that it is performable exactly as written.
