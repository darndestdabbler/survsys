# Enter a laboratory result

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/EnterALaboratoryResultTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I enter a laboratory result on a case morbidity report
> — the specimen that was collected, the test run on it and what that test
> found — so that the case carries the laboratory evidence it rests on, and
> anyone who opens the report reads the same specimen I recorded.

This is the **Laboratory** tab of the legacy report form and its **Add Lab**
form, and it is inferred from one legacy screen, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Laboratory** tab → **Add Lab** | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *4. Lab Entry — Form Structure*, and the screenshots `../nedss/docs/screenshots/session-010/01-laboratory-tab.png`, `02-add-lab-form.png`, `03-lab-form-filled.png` and `04-after-lab-save.png` | A table of the report's laboratory records, and an inline form that takes the specimen — performing lab, ordering facility and clinician, collection date, specimen source, accession number, whether it was sent to the state — and under it one test with its type, organism, result, value, units, reference range, status, date and comment. |

**The report is opened first.** Laboratory results live on the case
morbidity report, because that is where the legacy flow records them: the
Laboratory tab is a tab of the report form, and there is no other screen for
them. So this walkthrough begins by opening a report on An Nguyen — the same
two steps the *Create a case morbidity report* walkthrough describes — and
then enters a result on it.

Four differences from the legacy form are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **A specimen is recorded whole, in three levels.** The entry form takes
  the specimen, then any number of tests under it, each with any number of
  results under that, and one **Record specimen** button records them all
  together. The legacy form took one test with one result per **Add Lab**.
  Once recorded, a specimen is listed and not edited: Survsys has no screen
  to change or remove a recorded specimen yet.
- **The coded fields are chosen from lists, and the table shows what is
  stored.** Specimen source, test type, test status, organism and the result
  code are lists over the reference code lists an administrator keeps on the
  **Code lists** screen, seeded from the legacy lookup tables. The test types
  offered are the ones listed for the report's disease, or every test type
  where none is listed for it. The specimens table shows the number a chosen
  source has on its list — `558` for **Serum**. Lab facility, provider,
  diagnostic facility and reporting facility are still boxes that take the
  legacy identifier; this walkthrough leaves them empty, and the specimens
  table shows a dash for each.
- **Nothing on a specimen is required.** The legacy form required a
  performing lab — and in the legacy development environment that
  requirement could not be met, because its facility table was empty, which
  is why the legacy save was never seen to succeed. Survsys records a
  specimen with whatever was entered, and refuses only a date that is not a
  date and a test with more than one result marked primary.
- **The newest specimen is listed first.** What you just recorded is the
  top row of the table, so it is the row you read.

The legacy form's antimicrobial susceptibility rows, which the Survsys form
also takes, are not part of this walkthrough; nor is the **Clinical** tab
beside the Laboratory tab.

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
   conditions, Hepatitis A among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   An Nguyen among them; run the first and then the second against your
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

This walkthrough **creates a report and records a specimen on it**. Perform
it twice and you have two reports on An Nguyen, each with one specimen; that
is what the legacy system does too, and nothing here prevents it. It changes
nothing about the person, so the *Find a person* walkthrough reads the same
seven people before and after it. The end-to-end suite empties the specimens
and the reports along with the people it reseeds on every run, so a run
always begins with none.

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
2. In the box labelled **Last name**, type `Nguyen`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Nguyen, An**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Nguyen,
   An**.
6. Open the **Disease** list and choose **Hepatitis A**. The list closes,
   and the **Disease** box now reads **Hepatitis A**.
7. Open the **Investigating agency** list and choose **Salt Lake County**.
   The list closes, and the **Investigating agency** box now reads **Salt
   Lake County**.
8. Press **Save & Continue**.
9. The report now exists, and the screen is its own form. In the header bar,
   **Patient name** reads **Nguyen, An** and **Disease** reads **Hepatitis
   A**. Under the CDC notification panel, the tab strip shows eleven tabs,
   left to right: **Demographic**, **Clinical**, **Laboratory**,
   **Investigation**, **Administrative**, **Forms**, **Vaccines**,
   **Treatments**, **Encounters**, **Contacts** and **Notes**. Note the
   address in
   your browser's address bar: it ends in `/event/edit/` and a number, and
   step 15 comes back to it.
10. Press **Edit** at the top of the report, which opens it for editing, then press the **Laboratory** tab. The tab opens on two panels. The first,
    headed **Specimens**, is a table with the columns **Accession**,
    **Collected**, **Source**, **Lab facility**, **Provider**,
    **Diagnostic**, **Reporting**, **Sent to state** and **Actions**, and
    its one row
    reads **No specimens recorded yet.** The second, headed **Record a
    specimen**, is the entry form: the specimen's boxes, then a **Tests**
    section with an **Add test** button and nothing under it, then an
    **Antimicrobial susceptibility** section, then a **Record specimen**
    button.
11. In the box labelled **Accession number**, type `A2026-0417`. In the box
    labelled **Collection date**, enter the date `2026-10-03`. Open the
    **Specimen source** list and choose **Serum**. Leave the other specimen
    boxes empty and **Sent to state lab** untouched.
12. Under **Tests**, press **Add test**. A test block appears above the
    button, with the boxes **Test type**, **Test status**, **LOINC code**,
    **Reference range** and **Test date**, and under them a **Results**
    section with an **Add result** button and nothing under it. Open the
    **Test type** list and choose **IgM Antibody**. In the box labelled
    **LOINC code**, type `20575-7`. In the box labelled **Test
    date**, enter the date `2026-10-04`. Leave the other test boxes empty.
13. Under that test's **Results**, press **Add result**. A result row
    appears above the button, with the boxes **Organism**, **Value**,
    **Result code**, **Units** and **Comment** and a **Primary** tick box.
    In the box labelled **Value**, type `Positive`, and tick **Primary**.
    Leave the other result boxes empty.
14. Press **Record specimen** at the bottom of the form. A green message
    appears at the top of the tab, reading **The specimen has been
    recorded.** The **Specimens** table now has one row, reading, left to
    right: **A2026-0417**, **2026-10-03**, **558** — the number Serum has
    on the specimen source list — a dash in each of the five other
    columns, and **Edit** and **Delete** buttons under **Actions**. The entry form is empty again: **Accession number** is
    blank and there is no test block under **Tests**.
15. Go back to the report's address — the one you noted in step 9 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database rather than from what you typed, and press the
    **Laboratory** tab. The **Specimens** table has the one row, reading
    **A2026-0417**, **2026-10-03**, **558** and five dashes, exactly as in
    step 14.
    That is how you know the specimen reached the database.

## What you should see

A laboratory result on An Nguyen's case morbidity report, entered and read
back:

- A report on **Nguyen, An** for **Hepatitis A**, opened in the two steps,
  ending on its form with eleven tabs, **Laboratory** among them.
- The **Laboratory** tab with no specimens yet, and an entry form that takes
  a specimen, a test under it and a result under that.
- After entering accession number **A2026-0417**, collected **2026-10-03**
  from **Serum**, with one **IgM Antibody** test coded **20575-7** on
  **2026-10-04** and one primary result
  reading **Positive**, **Record specimen** answers **The specimen has been
  recorded.** and lists the specimen as the table's one row.
- Opening the report's address again and pressing **Laboratory** reads the
  same one row back from the database.

You now know how to record the laboratory evidence a case rests on, on the
report the case is filed against. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
