# Create a case morbidity report

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/CreateACaseMorbidityReportTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As an investigator, I open a case morbidity report on a person who is
> already on file — so that the case has a record number and a form of its
> own, and every later fact about it is recorded against that one report.

This is the legacy system's two-step **New CMR** flow, and it is inferred from
three legacy screens, all read-only here and never modified:

| Legacy screen | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **New CMR**, step 1 (`/morbidity/create`) | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *Step 1: Person Search*, and the screenshots `../nedss/docs/screenshots/session-008/02-new-cmr-person-search.png` and `03-new-cmr-search-results.png` | The same person search form the People screen has, and under it the people found, each one offering to open a report on them. |
| EpiTrax → **New CMR**, step 2 (`/morbidity/create/{personId}`) | The same document, *Step 2: Disease & Agency Selection*, and the screenshots `04-new-cmr-step2-disease-selection.png` and `05-new-cmr-step2-filled.png` | Three fields — **First reported to public health**, **Disease** and **Investigating Agency** — and the buttons **Save & Continue** and **Cancel**. |
| EpiTrax → the **CMR edit form** (`/event/edit/{recordNumber}`) | The same document, *Header Bar (persistent across tabs)*, and the screenshot `06-cmr-after-save.png` | The form the flow ends on: a header bar that stays above every tab, naming the patient, the record number, the event type, the disease, the investigating agency and the event date, and a strip of eight tabs under it. |

Four differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The report's address is `/event/edit/` followed by the report's
  identifier**, the number the database gave the report, rather than by its
  record number. The record number is in the header, where the legacy form
  shows it too.
- **The header carries eleven fields and a brief note.** Patient name,
  Record #, Event type, Disease, Investigating agency, Event date, MMWR week,
  Case status, Investigator, State case status and LHD case status are what
  it shows, with a **Brief note** box and its **Save note** button under
  them. The legacy header's action buttons and menus are not there: the case
  status is set in a **Case status** panel under the header, the workflow is
  moved from a **Workflow Options** panel under that, the case is queued for
  the CDC from a **CDC notification** panel, and sent to IBIS from an
  **IBIS** panel.
- **The tab strip carries eleven tabs.** Demographic, Clinical, Laboratory,
  Investigation, Administrative, Forms, Vaccines, Treatments, Encounters,
  Contacts and Notes. The case's supplemental forms, its vaccine records, its
  treatments and its encounters are each a tab of their own.
- **The record number is nine digits — the four-digit year, then a sequence
  of five digits counting the year's reports from `00001`** — exactly as the
  legacy system numbers them. Its value depends on how many reports this
  installation has already opened this year, so this walkthrough promises
  its shape and not its value: it begins with the current year, and it is
  nine digits long.

The legacy search form's free-text *Person name* box and *Score* column, the
*Create Person and CMR* button for a person not yet on file, and the legacy
dropdown's type-to-filter are not part of this walkthrough. The person this
walkthrough reports on is on file already, and Survsys lists the diseases and
agencies rather than filtering them as you type.

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
   conditions, Malaria among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Paul Zimmerman among them; run the first and then the second against your
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
5. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do.
   On a fresh database the only user is the administrator the API tier
   creates on its first start from two settings you choose:
   `Survsys__Administrator__UserName` and `Survsys__Administrator__Password`,
   set where `Survsys.Api` runs. That administrator holds the Administrator
   role, which has every privilege. Open the application tier's address, type
   that user name and password on the **Sign in** page, and press **Sign
   in**; then begin at step 1.

This walkthrough **creates a report**. Perform it twice and you have two
reports on Paul Zimmerman, with consecutive record numbers; that is what the
legacy system does too, and nothing here prevents it. It changes nothing
about the person, so the *Find a person* walkthrough reads the same seven
people before and after it.

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
2. The browser tab reads **New case morbidity report — Survsys**, and below
   the application bar is the heading **New case morbidity report**. Under
   the heading, a line of small text begins **Step 1 of 2.** and tells you to
   find the person this report is about.
3. In the box labelled **Last name**, type `Zimmerman`, then press
   **Search**.
4. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Zimmerman, Paul**. At the right-hand end
   of that row is a button reading **Create CMR**.
5. Press **Create CMR** on that row.
6. The heading still reads **New case morbidity report**. Under it, the line
   now begins **Step 2 of 2. Reporting on Zimmerman, Paul**, followed by the
   person's ID in brackets — the number this installation gave him, which
   this document cannot tell you.
7. In the box labelled **First reported to public health**, enter the date
   `2026-10-01`.
8. Open the **Disease** list and choose **Malaria**. The list closes, and
   the **Disease** box now reads **Malaria**.
9. Open the **Investigating agency** list and choose **Salt Lake County**.
   The list closes, and the **Investigating agency** box now reads **Salt
   Lake County**.
10. Press **Save & Continue**.
11. The report now exists, and the screen is its own form. The heading reads
    **Case morbidity report**, and the browser tab reads **Case morbidity
    report** followed by the record number and **— Survsys**. Read the
    header bar under the heading, eleven fields left to right:

    | Field | Reads |
    |---|---|
    | Patient name | **Zimmerman, Paul** |
    | Record # | nine digits beginning with the current year — for example, the first report of 2026 is **202600001** |
    | Event type | **Morbidity** |
    | Disease | **Malaria** |
    | Investigating agency | **Salt Lake County** |
    | Event date | today's date, written as `yyyy-mm-dd` |
    | MMWR week | the MMWR week today falls in, written as **Week** then the week and the year — for example **Week 41, 2026** |
    | Case status | **Unknown** |
    | Investigator | **Not assigned** |
    | State case status | nothing |
    | LHD case status | nothing |

    Under the fields is an empty **Brief note** box with a **Save note**
    button. The event date is the day the report was opened, which is what the
    legacy system records at this point too; the date you entered in step 7
    is the date first reported to public health, which the header does not
    show. The MMWR week is the event date's, and the case status reads
    **Unknown** because none has been set.
12. Above the header bar are the buttons **Edit**, **Delete** and **Print**:
    the report opens read-only, and **Edit** opens it for editing. Under the
    header bar are four panels, headed **Case status**, **Workflow Options**,
    **CDC notification** and **IBIS**, and under them is the tab strip. It shows eleven tabs,
    left to right: **Demographic**, **Clinical**, **Laboratory**,
    **Investigation**, **Administrative**, **Forms**, **Vaccines**,
    **Treatments**, **Encounters**, **Contacts** and **Notes**.

## What you should see

A case morbidity report on Paul Zimmerman, opened in two steps and ending on
its own form:

- Step 1 finds the person: **1 person(s) found, showing 1.**, and the row
  **Zimmerman, Paul** with its **Create CMR** button.
- Step 2 takes a date first reported, a disease and an agency, and **Save &
  Continue** opens the report.
- The report's form, headed **Case morbidity report**, with a header bar
  reading **Zimmerman, Paul**, a nine-digit record number beginning with the
  current year, **Morbidity**, **Malaria**, **Salt Lake County**, today's
  date, today's MMWR week and **Unknown**.
- Eleven tabs, **Demographic** first.

You now have a report with a record number of its own to record the case
against. If what you saw differs from what is written above, the walkthrough
has failed: say so rather than adjusting what you did, because the promise
this document makes is that it is performable exactly as written.
