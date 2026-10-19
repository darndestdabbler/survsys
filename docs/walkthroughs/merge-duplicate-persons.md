# Merge duplicate persons

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/MergeDuplicatePersonsTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a user trusted to merge people, I find the people on file who are likely
> the same person, compare two of them side by side, and merge them into the
> record I keep, so that everything recorded about that person — their cases
> among it — is under one record.

This is inferred from the legacy documentation, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → person merge | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, the *Person merge/dedup* row | Duplicate person records are merged into one. |
| EMSA → person matching | `../nedss/docs/business-logic/EMSA_PERSON_MATCHING.md`, *Phase 3: EMSA Match Classification Algorithm* | How two records are judged to be one person: a score from the fields they share, at or above 95 the same person, below 70 not, and between them a probable match a person confirms. |

**How a duplicate comes about.** A person search finds people whose names
begin with what was typed, so a search that gives a middle initial the
record on file does not have finds nobody — and the creation screen then
offers to create the person. That is how this walkthrough makes its
duplicate: Paul Zimmerman is on file with no middle name, and a second report
searched for as **Paul J Zimmerman** creates a second record for him.

**What a merge does.** The record you keep keeps its own demographics. Every
case, laboratory result, contact, vaccine, identifier, former name and address
of the other record moves onto it, and the other record stays on file only as
a pointer to the one kept, so it is found by no search and paired with nobody.
The **Duplicate persons** screen and the merge screen need the
**merge_persons** privilege; the Administrator role holds it.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves.
2. **The person and the diseases this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Giardiasis and Hepatitis A among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Paul Zimmerman among them, born 1950-07-04; run the first and then the
   second against your database. The seeded people include no two that the
   matcher takes for one person, so the duplicate this walkthrough makes is
   the only one listed. The end-to-end suite seeds both itself.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
5. **Signed in, as a user whose role holds every privilege**, the
   merge_persons privilege among them. On a fresh database the only user is
   the administrator the API tier creates on its first start from
   `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, type that user name and password on the
   **Sign in** page, and press **Sign in**; then begin at step 1.

This walkthrough **adds a person and merges them away**, and opens two
reports, so **to perform it again, or another walkthrough after it, against
the same database, reseed it first**: empty the person tables and their
reports, then run `conditions.sql` and `persons.sql` again. The end-to-end
suite reseeds exactly that way before this walkthrough and again after it.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, seeds the conditions and
the people, and performs the steps below in a real browser, recording video
of every test to `Survsys.E2ETests/bin/<configuration>/net10.0/videos`.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. In the box labelled **Last name**, type `Zimmerman`; in the box labelled
   **First name**, type `Paul`; then press **Search**. The line above the
   table reads **1 person(s) found, showing 1.**, and the one row's **Last,
   First** reads **Zimmerman, Paul**. Press **Create CMR** on that row.
3. Open the **Disease** list and choose **Hepatitis A**; then open the
   **Investigating agency** list and choose **Salt Lake County**. Press
   **Save & Continue**. The report's form opens, and in its header bar
   **Patient name** reads **Zimmerman, Paul** and **Disease** reads
   **Hepatitis A**.
4. Press **New CMR** in the application bar again. The heading reads **New
   case morbidity report**.
5. This time, as a second report about him might, give his middle initial:
   type `Zimmerman` in **Last name**, `Paul` in **First name**, `J` in
   **Middle name** and `1950-07-04` in **Date of birth**; then press
   **Search**. Nobody on file has that middle name, so the screen reads **No
   people found.**, and under it is a **Create person and CMR** button.
6. Press **Create person and CMR**. Under the heading, the line begins
   **Step 2 of 2. Reporting on Zimmerman, Paul** — a second record for him.
7. Open the **Disease** list and choose **Giardiasis**; then open the
   **Investigating agency** list and choose **Utah State**. Press **Save &
   Continue**. The report's form opens, and in its header bar **Patient
   name** reads **Zimmerman, Paul** and **Disease** reads **Giardiasis**.
8. In the application bar, press **Duplicate persons**. The heading reads
   **Duplicate persons**, and the table has one row: **Person** reads
   **Zimmerman, Paul**, born **1950-07-04**; **Possible duplicate** reads
   **Zimmerman, Paul**, born **1950-07-04**; **Score** reads **80**,
   **Verdict** reads **Probable** and **Why** reads **Last name matches;
   First name matches; Birth date matches**.
9. Press **Zimmerman, Paul** in the row's **Person** column. The merge
   screen opens, its heading reading **Merge persons**, with the two records
   side by side: on the left **Zimmerman, Paul**, the record on file, and on
   the right **Zimmerman, Paul J**, the one step 6 created.
10. Under the left-hand record, **Zimmerman, Paul**, press **Keep this
    record**. The button now reads **Keeping this record**.
11. Press **Merge**. Paul Zimmerman's own screen opens: the heading reads
    **Zimmerman, Paul**, **Middle name** reads **—**, and under **Cases** are
    both reports — the **Hepatitis A** report opened in step 3 and the
    **Giardiasis** report opened on the second record in step 7.
12. In the application bar, press **Duplicate persons** again. The screen now
    reads **No duplicate persons were found.**

## What you should see

Two records for one man, found and merged into one:

- A report on **Zimmerman, Paul** for **Hepatitis A**, on the record on file.
- A search with his middle initial finding nobody, and a second record for
  him created with a report for **Giardiasis**.
- The **Duplicate persons** screen pairing the two, scoring them **80**, a
  **Probable** match, because their last name, first name and date of birth
  agree.
- The two compared on the merge screen, the record on file kept, and its own
  screen listing both reports after the merge.
- The **Duplicate persons** screen finding no duplicate afterwards.

You now know how to find duplicate people and merge them into one. If what
you saw differs from what is written above, the walkthrough has failed: say
so rather than adjusting what you did, because the promise this document
makes is that it is performable exactly as written.
