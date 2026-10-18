# Save a view

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SaveAViewTests.cs`, and a step you cannot perform is a
defect in the application or in this document — never something to work
around.

## User story

> As a user of Survsys, I define a view of the case morbidity reports — a
> name, the columns I want it to export, and the filters that select which
> reports it covers — and save it, so that the cut of the data I keep coming
> back to is mine to open again and to export, and I can change it when what I
> need changes.

A saved view is inferred from two legacy screens, both read-only here and
never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **Events List** → *View Management* | `../nedss/docs/functional/epitrax-core-workflows.md`, *2. Events List (`03-events-list`)*, the *View Management* and *View Criteria* subsections, and the screenshot `../nedss/docs/screenshots/session-007/03-events-list.png` | A **Current view** dropdown of *saveable named views*, a **New view name** box with **Save**, and the filters a view selects by — among them the diseases, the investigating agency, and the event date range Survsys keeps. |
| EpiTrax → **Exports/AVR** | `../nedss/docs/functional/epitrax-core-workflows.md`, *7. Exports/AVR (`08-exports`)*, and the screenshot `../nedss/docs/screenshots/session-007/08-exports.png` | A *custom data export builder* — the legacy screen on which the columns of an export are chosen, which Survsys folds into the view itself as the columns it exports. |

Four differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **One screen does both jobs.** The legacy kept the saveable views on the
  Events List and the custom exports on a separate Exports screen. In Survsys
  a **saved view** both selects the reports (its filters) and names the
  columns it exports, on the one **Saved views** screen.
- **A view is the signed-in person's own.** The list shows your views by
  name, with the columns each exports and the filters each selects by. There
  are no Drafts or Public folders.
- **A filter left at *Any* selects every report**, and a view with no filters
  at all reads **Every report** in the list. Setting a filter narrows the
  view to reports that match it.
- **The columns are a fixed catalogue** — the fields the system knows how to
  read off a report — each chosen by its checkbox. However you tick them, a
  view stores and shows them in the catalogue's own order.

This walkthrough defines a view, saves it, reads it back in the list, and then
edits it and saves over it. Exporting the view's data to a file is its own
walkthrough, [Export a dataset](export-a-dataset.md).

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   condition this view selects by, **Malaria**, and the agency it selects by,
   **Davis County**, are among the data a database at migration level already
   has — Malaria from the conditions seed and Davis County from the migration.
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
   saved view belongs to whoever saved it, so the view this walkthrough saves
   is the signed-in user's. On a fresh database the only user is the
   administrator the API tier creates on its first start from
   `Survsys__Administrator__UserName` and `Survsys__Administrator__Password`,
   set where `Survsys.Api` runs. Open the application tier's address, sign in,
   then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level, clears the saved views table, and performs the steps below
in a real browser. It records video of every test it runs, and writes the
recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as
`.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Saved views**. (The screen is
   also at `/saved-views`.)
2. The browser tab reads **Saved views — Survsys**, and below the application
   bar is the heading **Saved views**. Below that is a form whose own heading
   reads **New saved view** — the form on which you define one.
3. In the box labelled **Name**, type `Malaria cases`.
4. Under **Columns**, tick three boxes — **record_number**, **last_name** and
   **condition** — and leave the rest clear. These are the columns the view
   will export.
5. In the list labelled **Condition**, choose **Malaria**. Leave every other
   filter — **Investigating agency**, **Workflow state**, **Case status**,
   **Onset from** and **Onset to** — at **Any**.
6. Press **Save view**. The form's heading goes back to **New saved view**
   and its boxes clear, because the view has been saved and the form is ready
   for the next one.
7. Below the form is the list of your saved views. It now has a row for
   **Malaria cases**, whose columns are **Columns** and **Filters**:

   | Name | Columns | Filters |
   |---|---|---|
   | Malaria cases | record_number, last_name, condition | Malaria |

   The **Columns** read in the catalogue's order, not the order you ticked
   them. The **Filters** read **Malaria** — the one filter you set, by the
   condition's name. On the far side of the row are **Export CSV**, **Edit**
   and **Delete**.
8. Press **Edit** on the **Malaria cases** row. The form's heading changes to
   **Edit Malaria cases**, and the form fills with the view's name, its ticked
   columns and its **Condition** of **Malaria** — ready to change.
9. In the list labelled **Investigating agency**, choose **Davis County**,
   then press **Save view**.
10. The list still has a single **Malaria cases** row — saving over a view
    does not add a second — and its **Filters** now read both the condition
    and the agency, by name:

    | Name | Columns | Filters |
    |---|---|---|
    | Malaria cases | record_number, last_name, condition | Malaria; Davis County |

## What you should see

A view of the reports, defined, saved, read back, and saved over:

- The heading **Saved views**, under an application bar reading **Survsys**,
  with a form headed **New saved view**.
- After you name the view **Malaria cases**, tick **record_number**,
  **last_name** and **condition**, choose **Malaria** for **Condition**, and
  press **Save view**, the list shows a **Malaria cases** row whose
  **Columns** read **record_number, last_name, condition** and whose
  **Filters** read **Malaria**.
- After you press **Edit**, add **Davis County** as the **Investigating
  agency**, and press **Save view** again, there is still one **Malaria
  cases** row, and its **Filters** read **Malaria; Davis County**.

You now know how to save a view of the reports — its columns and its filters —
and to change one you have saved. If what you saw differs from what is written
above, the walkthrough has failed: say so rather than adjusting what you did,
because the promise this document makes is that it is performable exactly as
written.
