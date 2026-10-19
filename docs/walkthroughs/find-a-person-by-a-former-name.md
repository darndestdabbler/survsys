# Find a person by a former name

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/FindAPersonByAFormerNameTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, when a patient's last name changes I record the new
> name on their case, and I can still find them afterwards by the name they
> used to have, so that a report filed under the old name is matched to the
> person already on file rather than opened against a new one.

This is inferred from the legacy documentation, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → last name change detection | `../nedss/docs/business-logic/EMSA_PERSON_MATCHING.md`, *Last Name Change Detection* | The legacy system looked for a person on file who matched an incoming report on everything except the last name — first name and date of birth alike, last name different — and treated them as a potential name change. It was first written for marriage name changes. |
| EpiTrax → the CMR edit form → **Demographic** tab | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *1. Demographic (37 fields)* | The person's name is changed where the rest of their demographics are: on the Demographic tab of a report about them. |

**Survsys keeps the old name rather than guessing it.** Where the legacy
system inferred a name change by searching around the last name, Survsys
records one: when a person's name is changed and saved, the name they had is
kept as a **former name**, dated the day it changed, and the person search
finds a person by a former name as well as by their current one. This
walkthrough changes a name and then finds the person by the name they had.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves.
2. **The person and the disease this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Giardiasis among them, and
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
5. **Signed in, as a user whose role holds every privilege.** On a fresh
   database the only user is the administrator the API tier creates on its
   first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, type that user name and password on the
   **Sign in** page, and press **Sign in**; then begin at step 1.

This walkthrough **changes Jane Abbott's name** to Jane Kowalski and opens a
report on her, and every other walkthrough that lists the people on file
reads her as Abbott. So **to perform this walkthrough again, or any other
walkthrough after it, against the same database, reseed it first**: empty the
person tables and their reports, then run `conditions.sql` and `persons.sql`
again. The end-to-end suite reseeds exactly that way before this walkthrough
and again after it.

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
2. In the box labelled **Last name**, type `Abbott`; then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Abbott, Jane**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Abbott,
   Jane**.
6. Open the **Disease** list and choose **Giardiasis**; then open the
   **Investigating agency** list and choose **Utah State**.
7. Press **Save & Continue**. The report now exists, and the screen is its
   own form: in the header bar, **Patient name** reads **Abbott, Jane**, and
   the **Demographic** tab is open.
8. Press **Edit** at the top of the report, which opens it for editing. In
   the **Name** section, change **Last name** from `Abbott` to `Kowalski`.
9. Press **Save & Continue** at the bottom of the tab. A green message
   appears at the top of the tab, reading **The demographics have been
   saved.** Under the name boxes, **Former names** now lists one name:
   **Abbott, Jane Marie — changed** and today's date, written year-month-day
   — her whole name as it was, middle name and all.
10. In the application bar, press **People**. The heading reads **People**.
11. In the box labelled **Last name**, type `Abbott` — the name she used to
    have — and press **Search**.
12. The line above the table reads **1 person(s) found, showing 1.**, and the
    one row's **Last, First** reads **Kowalski, Jane**: she was found by her
    former name, and is listed by her current one. Her **Score** reads **0
    (None)** and **Why** reads **Last name differs**, because a score
    compares what you typed with the name she has now; it is the search, not
    the score, that looks through former names.
13. Press **Kowalski, Jane**, the name on that row. Her own screen opens: the
    heading reads **Kowalski, Jane**, **Name** reads **Kowalski, Jane**, and
    **Former names** reads **Abbott, Jane (until** today's date**)**. Under
    **Cases**, the Giardiasis report you opened in step 7 is listed.

## What you should see

Jane Abbott's change of name, recorded and then searched for:

- A report on **Abbott, Jane** for **Giardiasis**, opened in the two steps.
- Her last name changed to **Kowalski** on its **Demographic** tab and saved,
  with **Abbott, Jane Marie** kept under **Former names**, dated today.
- A person search by **Abbott** finding one person, **Kowalski, Jane**.
- Her own screen naming her **Kowalski, Jane**, listing **Abbott, Jane** as a
  former name until today, and listing the Giardiasis report under her.

You now know how to change a person's name without losing the name they were
known by, and how to find them by it. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
