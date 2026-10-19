# Find a case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/FindACaseTests.cs`, and a step you cannot perform is a
defect in the application or in this document — never something to work
around.

## User story

> As an investigator, I find a case morbidity report again by searching the
> cases on file for its patient and its disease, read its row, and open the
> case from that row, so that I can get back to a case I am working without
> having to remember its record number.

The Events screen is inferred from the legacy EpiTrax, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **EVENTS** | `../nedss/docs/functional/epitrax-core-workflows.md`, *Core Screens*, *2. Events List* (screenshot `03-events-list`) | The main case management view: events searched by record number, last and first name, disease, investigating agency, investigation status and event date, each row reading its patient, record number, disease, statuses, agency and event date. |

Two differences from the legacy system are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The search carries the criteria Survsys keeps.** The legacy Events list
  filters by saved views, age, tags, investigators, queues, language, an
  address and a facility as well. The **Events** screen searches by record
  number, last and first name, disease, investigating agency, workflow state,
  case status and an onset date range, and nothing else.
- **The record number is the link.** Each row's **Record #** opens the case at
  the same address the creation flow opens it at.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person the case is opened on, **Rodriguez, Carlos**, is one of the seven
   the people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`),
   and the conditions this walkthrough names, **Giardiasis** and **Malaria**,
   are among the seven the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`); run both against your
   database if you brought it up by hand.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. On
   a fresh database the only user is the administrator the API tier creates on
   its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, sign in, then begin at step 1.

This walkthrough **opens a case** on Rodriguez, Carlos for Giardiasis, so that
there is a case to find. Perform it twice and the search in step 13 finds two;
the end-to-end suite empties the cases on every run, so a run finds one.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, and performs the steps below in a real browser.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**. The heading reads
   **New case morbidity report**.
2. In the box labelled **Last name**, type `Rodriguez`, then press **Search**.
3. The count line reads **2 person(s) found, showing 2.** Press **Create CMR**
   on the row reading **Rodriguez, Carlos**.
4. The line under the heading now reads **Step 2 of 2. Reporting on
   Rodriguez, Carlos**, followed by an identifier this document does not
   print.
5. In the box labelled **First reported to public health**, enter
   `2026-10-05`.
6. In the list labelled **Disease**, choose **Giardiasis**.
7. In the list labelled **Investigating agency**, choose **Tooele County**,
   then press **Save & Continue**.
8. The report's own form opens, headed **Case morbidity report**. Its header
   bar reads **Rodriguez, Carlos** for the patient, **Giardiasis** for the
   disease and **Tooele County** for the agency, with a record number of the
   current year and five more digits. Note the record number: it is what you
   will find the case by.
9. In the application bar, press **Events**. The browser tab reads **Events —
   Survsys**, the heading reads **Events**, and no results are shown until you
   search.
10. In the box labelled **Last name**, type `Rodriguez`; in the box labelled
    **First name**, type `Carlos`.
11. In the list labelled **Disease**, choose **Malaria**, then press
    **Search**. The screen reads **No cases found.**: Rodriguez, Carlos has no
    Malaria case, so the case you opened is not among the results.
12. In the list labelled **Disease**, choose **Giardiasis** instead, and press
    **Search** again.
13. The count line reads **_n_ case(s) found, showing _n_.**, and the table
    has a row whose **Record #** is the one you noted. That row reads, left to
    right after the record number: **Rodriguez, Carlos** for **Patient**,
    **Giardiasis** for **Disease**, **Tooele County** for **Agency**,
    **Assigned to LHD** for **Workflow**, **Unknown** for **Status**, the
    case's MMWR week and year as _week_/_year_ for **MMWR week**, and today's
    date as `yyyy-mm-dd` for **Onset**, the date the case was opened.
14. Press the record number in that row. The report's own form opens, headed
    **Case morbidity report**, and its header bar reads the record number you
    noted, **Rodriguez, Carlos**, **Giardiasis** and **Tooele County**.

## What you should see

A case found by its patient and disease and opened from its row:

- A search for Rodriguez, Carlos with **Malaria** that finds nothing, and the
  same search with **Giardiasis** that finds the case you opened.
- Its row on the **Events** screen, reading **Rodriguez, Carlos**,
  **Giardiasis**, **Tooele County**, **Assigned to LHD** and **Unknown**, with
  its MMWR week and onset date.
- The case itself, opened from its record number, with the same header.

You now know how to find a case again from the Events screen. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
