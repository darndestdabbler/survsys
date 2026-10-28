# Export a dataset

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ExportADatasetTests.cs`, and a step you cannot perform is
a defect in the application or in this document — never something to work
around.

## User story

> As a user of Survsys, I take a saved view of the case morbidity reports and
> export its data to a file — the columns I chose, for the reports the view
> selects — so that I can open the cut I keep in a spreadsheet or hand it to
> someone who works outside the system.

Exporting a dataset is inferred from two legacy screens, both read-only here
and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **Exports/AVR** | `../nedss/docs/functional/epitrax-core-workflows.md`, *7. Exports/AVR (`08-exports`)*, and the screenshot `../nedss/docs/screenshots/session-007/08-exports.png` | A *custom data export builder* — the legacy screen whose whole purpose is building and running an export of the data for analysis, visualisation and reporting. |
| EpiTrax → **Events List** / **Facilities** → *Export* | `../nedss/docs/functional/epitrax-core-workflows.md`, *2. Events List* and the facilities search's *Action buttons: Search, Export, Clear* | The **Export** button that takes a filtered list of records out of the system as a file, which is what a saved view's export does in Survsys. |

Three differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The export is a saved view's own.** There is no separate export builder:
  a saved view already names the columns and the filters, so exporting it is
  a single **Export CSV** link on the view's row. Saving the view is its own
  walkthrough, [Save a view](save-a-view.md).
- **The file is CSV, and the browser downloads it.** The link hands the
  browser a file named `saved-view-<id>.csv`, which the browser saves the way
  it saves any download. The file is a header row of the view's column names,
  then one row per report the view selects.
- **The reports are the ones on file.** A fresh Survsys has no reports — the
  migration and the seed bring up the conditions, the people and the agencies
  but no case — so this walkthrough first creates the one report it exports,
  through the New CMR screens, exactly as a user would.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this report is opened on, **Zimmerman, Paul**, is one of the seven
   the people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`),
   and the condition it is opened for, **Bartonellosis**, is one of the seven
   the conditions seed inserts (`Survsys.IntegrationTests/Fixtures/conditions.sql`);
   run both against your database if you brought it up by hand.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. A
   saved view belongs to whoever saved it, so the view you export is the
   signed-in user's. On a fresh database the only user is the administrator
   the API tier creates on its first start from
   `Survsys__Administrator__UserName` and `Survsys__Administrator__Password`,
   set where `Survsys.Api` runs. Open the application tier's address, sign in,
   then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, and performs the steps below in a real browser —
creating the report, saving the view and downloading the file. It records
video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. The browser tab reads **New case morbidity report — Survsys**, the heading
   reads **New case morbidity report**, and a line under it reads **Step 1 of
   2.**
3. In the box labelled **Last name**, type `Zimmerman`, then press
   **Search**.
4. The count line reads **1 person(s) found, showing 1.**, and the one row
   reads **Zimmerman, Paul** with a **Create CMR** button.
5. Press **Create CMR** on that row.
6. The heading still reads **New case morbidity report**, and the line now
   reads **Step 2 of 2. Reporting on Zimmerman, Paul**, followed by an
   identifier this document does not print.
7. In the box labelled **First reported to public health**, enter
   `2026-10-01`.
8. In the list labelled **Disease**, choose **Bartonellosis**.
9. In the list labelled **Investigating agency**, choose **Davis County**,
   then press **Save & Continue**.
10. The report's own form opens, headed **Case morbidity report**. Its header
    bar reads **Zimmerman, Paul** for the patient, **Bartonellosis** for the
    disease and **Davis County** for the agency, with a record number of the
    current year and five more digits. This is the report you are about to
    export.
11. In the side menu, under **Cases**, choose **Saved views**. The browser tab reads
    **Saved views — Survsys** and the heading reads **Saved views**, with a
    form headed **New saved view**.
12. Define the view that selects your report:
    - In **Name**, type `Bartonellosis export`.
    - Under **Columns**, tick **last_name**, **first_name** and **condition**,
      and leave the rest clear.
    - In the list labelled **Condition**, choose **Bartonellosis**, leave
      every other list at **Any**, and leave the **Onset from** and **Onset
      to** boxes empty.
    - Press **Save view**.
13. The list of views now has a **Bartonellosis export** row, whose
    **Columns** read **last_name, first_name, condition** and whose
    **Filters** read **Bartonellosis**. On the far side of the row is an
    **Export CSV** link.
14. Press **Export CSV**. The browser downloads a file named
    `saved-view-<id>.csv`, where `<id>` is the view's own number.
15. Open the downloaded file. It has two lines — a header of the column names
    you chose, and your report's one row:

    ```
    last_name,first_name,condition
    Zimmerman,Paul,Bartonellosis
    ```

    The columns are in the catalogue's order, not the order you ticked them,
    and the one data row is the report you opened, because the view's
    **Bartonellosis** filter selects it and no other report on file.

## What you should see

A report created, a view saved over it, and its data taken out as a file:

- A case morbidity report opened on **Zimmerman, Paul** for **Bartonellosis**
  under **Davis County**, shown on its own form.
- A **Bartonellosis export** view whose **Columns** read **last_name,
  first_name, condition** and whose **Filters** read **Bartonellosis**.
- A downloaded CSV file of two lines: the header **last_name,first_name,condition**
  and the row **Zimmerman,Paul,Bartonellosis**.

You now know how to export the data of a saved view to a file. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that it
is performable exactly as written.
