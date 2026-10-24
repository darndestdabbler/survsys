# Correct a laboratory result

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/CorrectALaboratoryResultTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I correct a laboratory result recorded on a case
> morbidity report — its value and its result code — when the laboratory
> sends a correction, so that the case rests on the result the laboratory
> stands by, and anyone who opens the report reads the corrected result and
> not the one it replaced.

This is the **Laboratory** tab of the legacy report form, edited rather than
added to, and it is inferred from the legacy documentation, read-only here and
never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Laboratory** tab | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *4. Lab Entry — Form Structure* | The laboratory chain on a report: a specimen, the test run on it, and that test's organism, result, value, units and comment. |
| CMR editing | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *CMR editing (all tabs)* | Every tab of the report, Lab among them, is edited as well as entered. |

**A result is recorded first.** There must be a result to correct, so this
walkthrough opens a report on Jane Abbott and records a specimen on it with
one test and one result, as the *Enter a laboratory result* walkthrough does,
and then corrects that result.

Two differences from the legacy form are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **A specimen is corrected whole.** Pressing **Edit** on a specimen's row
  opens it in the entry form, headed **Edit specimen**, with its tests and
  each test's results in it, and one **Save changes** button saves the
  specimen, its tests and its results together.
- **The specimens table lists the specimen, not its results.** A result is
  read in the form a specimen's **Edit** opens, which is where this
  walkthrough reads the corrected one back.

## Before you start

You need all five of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves.
2. **The person and the disease this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Hepatitis A among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Jane Abbott among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
5. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. On
   a fresh database the only user is the administrator the API tier creates on
   its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, sign in, then begin at step 1.

This walkthrough **creates a report and records a specimen on it**. Perform it
twice and you have two reports on Jane Abbott, each with one specimen. The
end-to-end suite empties the specimens and the reports on every run, so a run
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
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. In the box labelled **Last name**, type `Abbott`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row reads **Abbott, Jane**. Press **Create CMR** on that row.
4. Under the heading, the line begins **Step 2 of 2. Reporting on Abbott,
   Jane**.
5. Open the **Disease** list and choose **Hepatitis A**.
6. Open the **Investigating agency** list and choose **Salt Lake County**,
   then press **Save & Continue**.
7. The report's own form opens. In the header bar, **Patient name** reads
   **Abbott, Jane** and **Disease** reads **Hepatitis A**. Note the address in
   your browser's address bar: it ends in `/event/edit/` and a number, and
   step 15 comes back to it.
8. Press **Edit** at the top of the report, which opens it for editing, then
   press the **Laboratory** tab. Its **Specimens** table reads **No specimens
   recorded yet.**, and under it is the entry form headed **Record a
   specimen**.
9. In the box labelled **Accession number**, type `B2026-0522`. In the box
   labelled **Collection date**, enter the date `2026-10-06`. Open the
   **Specimen source** list and choose **Serum**.
10. Under **Tests**, press **Add test**. Open the **Test type** list and
    choose **IgM Antibody**. In the box labelled **Test date**, enter the
    date `2026-10-07`.
11. Under that test's **Results**, press **Add result**. In the box labelled
    **Value**, type `Negative`; open the **Result code** list and choose
    **Negative / Non-reactive**; and tick **Primary**.
12. Press **Record specimen**. A green message reads **The specimen has been
    recorded.**, and the **Specimens** table has one row, whose
    **Accession** reads **B2026-0522** and **Collected** reads
    **2026-10-06**.
13. The laboratory has sent a correction: the result was positive. Press
    **Edit** on the specimen's row. The entry form is now headed **Edit
    specimen** and holds the specimen as recorded: **Accession number** reads
    **B2026-0522**, and under its one test the one result reads **Negative**
    in **Value** and **Negative / Non-reactive** in **Result code**, with
    **Primary** ticked.
14. In the box labelled **Value**, replace `Negative` with `Positive`. Open
    the **Result code** list and choose **Positive / Reactive**. Press **Save
    changes**. A green message reads **The specimen has been saved.**, the
    entry form is headed **Record a specimen** again, and the **Specimens**
    table still has the one row, **B2026-0522**.
15. Go back to the report's address — the one you noted in step 7 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database. Press **Edit** at the top of the report, then the
    **Laboratory** tab, then **Edit** on the specimen's row. The form headed
    **Edit specimen** reads the corrected result: **Positive** in **Value**,
    **Positive / Reactive** in **Result code**, and **Primary** still
    ticked. That is how you know the correction reached the database. Press
    **Cancel** to close the form without saving.

## What you should see

A laboratory result on Jane Abbott's case morbidity report, corrected and read
back:

- A specimen **B2026-0522**, collected **2026-10-06** from **Serum**, with one
  **IgM Antibody** test and one primary result recorded as **Negative**,
  **Negative / Non-reactive**.
- The same result, opened with the specimen's **Edit** and saved with **Save
  changes**, now **Positive**, **Positive / Reactive**.
- The report opened again from its address, reading the corrected result back
  from the database.

You now know how to correct a laboratory result on the report it is filed
against. If what you saw differs from what is written above, the walkthrough
has failed: say so rather than adjusting what you did, because the promise
this document makes is that it is performable exactly as written.
