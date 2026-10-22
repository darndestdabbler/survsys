# Run an ad-hoc report

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RunAnAdHocReportTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As an epidemiologist, I count the cases one of my saved views selects,
> grouped by one of the view's columns, read the counts on screen and export
> them as a file — so that I can answer a question about the cases without
> asking for a report to be written.

Running an ad-hoc report is inferred from the legacy EpiTrax, read-only here
and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → AVR | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *12. Reporting & Export* | Ad-hoc reporting, gated by a privilege of its own, over the data a user may export. |
| EpiTrax → saved views | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *12. Reporting & Export* | The saved filter and column configurations a user has made, which a report runs over. |

Two differences from the legacy are deliberate, and this walkthrough describes
what Survsys shows rather than what EpiTrax showed:

- **A report is a count over a saved view.** Survsys counts the cases a view
  selects by any one of the view's own columns, rather than offering a query
  builder of its own: the view says which cases, the column says how to
  group them.
- **The counts are exported as they are shown.** The file is the column you
  grouped by and the count, one line per group, in the order the screen
  shows them — the largest count first, then by the group.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The people
   this walkthrough reports on, **Nguyen, An** and **Abbott, Jane**, are ones
   the people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`),
   and the condition, **Lyme disease**, one the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`). Run both, in that
   order, against your database if you brought it up by hand. The counts below
   are of a database with no case of Lyme disease on file before you begin, as
   a freshly seeded one has none.
2. **The `Survsys.Api` tier running.** On a developer machine: `dotnet run
   --project Survsys.Api --urls http://localhost:5100`.
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
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. The
   **Reports** screen needs the privilege to run reports. Open the application
   tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, and performs the steps below in a real browser,
seeding the database again before them so no case another walkthrough opened
is counted.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Nguyen`, then press **Search**. The one row reads
   **Nguyen, An**. Press its **Create CMR** button.
3. In the list labelled **Disease**, choose **Lyme disease**. In
   the list labelled **Investigating agency**, choose **Davis County**. Press
   **Save & Continue**. The report's own form opens, its header bar reading
   **Nguyen, An** for the patient and **Lyme disease** for the
   disease.
4. In the application bar, press **New CMR** again. In the box labelled
   **Last name**, type `Abbott`, then press **Search**. The one row reads
   **Abbott, Jane**. Press its **Create CMR** button.
5. In the list labelled **Disease**, choose **Lyme disease**. In
   the list labelled **Investigating agency**, choose **Summit County**. Press
   **Save & Continue**. The report's own form opens, its header bar reading
   **Abbott, Jane** for the patient and **Lyme disease** for the
   disease.
6. In the application bar, press **Saved views**. Under **New saved view**, in
   the box labelled **Name**, type `Lyme disease by agency`. Tick the
   columns **last_name** and **agency**. In the list labelled **Condition**,
   choose **Lyme disease**. Press **Save view**. The list of views
   has a row for **Lyme disease by agency**.
7. In the application bar, press **Reports**. The browser tab reads
   **Reports — Survsys** and the heading reads **Reports**. Below it, under
   the heading **Saved view report**, in the list labelled **Saved view**,
   choose **Lyme disease by agency**. The list labelled **Group by** now
   reads **last_name**, the view's first column.
8. In the list labelled **Group by**, choose **agency**, then press **Run**
   under it.
9. A table opens, headed **agency** and **Count**. It has two rows: **Davis
   County** counting **1**, then **Summit County** counting **1**.
10. Press **Export CSV** beside **Run**. A file downloads, named
    `saved-view-` followed by the view's number and `-counts.csv`. Open it:
    its first line reads `agency,count`, then `Davis County,1`, then `Summit
    County,1`.

## What you should see

One ad-hoc report over a saved view:

- On the **Reports** screen, your saved view, and its own columns to group
  by.
- The counts of the cases the view selects, one row per agency.
- The same counts in the downloaded file, under a header of the column you
  grouped by and `count`.

You now know how to run an ad-hoc report. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
