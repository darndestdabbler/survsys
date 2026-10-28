# Configure an agency

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/ConfigureAnAgencyTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As a surveillance administrator, I open one of the health departments
> already on file and set its abbreviation and the zip codes of its
> jurisdiction — so that the agency is configured the way my state runs it,
> and a case is owned by the department whose jurisdiction the patient lives
> in.

This is administration, not case work: the agencies are reference data every
case morbidity report is assigned against, and configuring one is inferred
from two legacy sources, both read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → Admin → **Agencies** (`/nedss/admin/*`) | `../nedss/docs/functional/epitrax-core-workflows.md`, *Admin Pages → Key Admin Screens*, the **Agencies** row, and the screenshot `../nedss/docs/screenshots/session-007/12-admin-agencies.png` | The agencies administration screen — a *list of 16 Utah health departments* — edited through the admin section's shared list-and-edit pattern. |
| EpiTrax jurisdiction mapping | `../nedss/docs/functional/phase-1.6-cross-cutting-patterns.md`, *7. Jurisdiction / Geography* | EpiTrax maps a zip code to the agency whose jurisdiction includes it, in `zip_code_agency` against `agency`. The port's recommendation, stated there, is one zip-to-agency mapping table — the jurisdiction this screen edits, as a list of zip codes on the agency itself. |

Four differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **An agency's jurisdiction is one box of zip codes on the agency's own
  screen**, not a separate zip-to-agency admin screen. You type the zip codes
  separated by commas, spaces or new lines, and a save **replaces** the whole
  jurisdiction with what the box holds rather than adding to it.
- **Survsys reads each zip code before it stores it.** A ZIP+4 such as
  `84025-6789` is cut to its five-digit zip, the same zip given twice is
  stored once, the list is stored in ascending order however you typed it,
  and a value that is not a zip code is refused rather than saved.
- **The agencies list is one page with a single Name column**, each name a
  link into that agency's screen. The legacy admin list carried more columns
  and a pager; Survsys has sixteen agencies and shows them in one page.
- **The sixteen agencies are seeded by the database migration**, so this
  walkthrough configures one already on file rather than creating one. The
  **New agency** button opens an empty form for a new one, and is not part of
  this walkthrough.

The agency this walkthrough configures is **Davis County**, one of the
sixteen the migration seeds. Its identifier is the number the migration gave
it, so this document never tells you what the identifier reads, only that the
agency is there under that name.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   sixteen agencies this walkthrough names are inserted by that migration, so
   a database at migration level has them with no fixture to run.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do.
   On a fresh database the only user is the administrator the API tier
   creates on its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. That
   administrator holds the Administrator role, which has every privilege.
   Open the application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level, and performs the steps below in a real browser. It records
video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Reference data**, choose **Agencies**. (The agencies
   screen is also at `/agencies`, so <http://localhost:5000/agencies> opens
   the same screen.)
2. The browser tab reads **Agencies — Survsys**, and below the application
   bar is the heading **Agencies**. Above the list is a **New agency**
   button, which this walkthrough does not use.
3. The list has a single column, **Name**, and the sixteen Utah health
   departments down it in alphabetical order. **Davis County** is one of
   them. Each name is a link into that agency's screen.
4. Press **Davis County**.
5. The browser tab reads **Edit agency — Survsys**, and the heading reads
   **Edit agency**. The form's **Name** box reads **Davis County** — this is
   the agency you opened. The **Short name** and **Jurisdiction zip codes**
   boxes hold whatever Davis County was last configured with, which on a
   database nobody has configured yet is nothing; either way, the next two
   steps set them, because a save writes what the boxes hold.
6. In the box labelled **Short name**, type `Davis`. (Typing into a box
   replaces what it held.)
7. In the box labelled **Jurisdiction zip codes**, type these three lines,
   exactly as shown — two zip codes on the first line, a ZIP+4 on the second,
   and a repeat of the first line's second zip on the third:

   ```
   84037, 84010
   84025-6789
   84010
   ```

   The box's own hint reads **Separate zip codes with commas, spaces or new
   lines.**, which is why the first line's comma and the line breaks all count
   as separators.
8. Press **Save**. The screen goes back to the **Agencies** list, under the
   heading **Agencies**, which is how you know the save succeeded.
9. Press **Davis County** again, to read back from the database what the save
   wrote.
10. The heading reads **Edit agency** again. The **Short name** box reads
    **Davis**. The **Jurisdiction zip codes** box reads three zip codes, one
    per line, in ascending order:

    ```
    84010
    84025
    84037
    ```

    That is what Survsys made of what you typed: `84025-6789` cut to its
    five-digit zip **84025**, the two **84010** entries stored once, and the
    whole list put in ascending order however you typed it.

## What you should see

An agency already on file, configured and read back:

- The heading **Agencies**, under an application bar reading **Survsys**, with
  the sixteen Utah health departments down a single **Name** column and
  **Davis County** among them.
- **Davis County**'s screen, headed **Edit agency**, with its **Name** box
  reading **Davis County**.
- After you set **Short name** to **Davis** and the **Jurisdiction zip codes**
  box to the three lines above and press **Save**, the list comes back.
- Opening **Davis County** again reads **Davis** in **Short name** and, in
  **Jurisdiction zip codes**, **84010**, **84025** and **84037**, one per
  line and in that order — a ZIP+4 cut to its zip, a duplicate stored once,
  and the list sorted ascending.

You now know how to configure one of the agencies a case is assigned against:
its abbreviation and the zip codes of its jurisdiction. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
