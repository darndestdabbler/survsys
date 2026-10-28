# Complete a supplemental form

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/CompleteASupplementalFormTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I complete the supplemental form a disease asks for on
> the case I am working — answering its questions, in their sections, in
> words or by choosing from its answers — so that the case carries the
> condition-specific detail its investigation needs, and anyone who opens the
> report reads the answers I gave.

A supplemental form is built once, for a condition, and given to every new
case on that condition automatically. This walkthrough builds one first,
because the end-to-end suite starts with none, and then completes it on a
case. It is inferred from two legacy screens, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **Admin** → **Forms** | `../../../nedss/docs/functional/epitrax-core-workflows.md`, *Key Admin Screens*, the row *Forms* (screenshot `19-admin-forms`) | The dynamic form builder: a form for a condition, its sections and their questions. |
| EpiTrax → the CMR edit form → its condition's supplemental form | `../../../nedss/docs/functional/phase-1.6-feature-matrix.md`, *7. Forms & Questionnaires* | Nested sections and questions, text answers and coded values per question, and the form "auto-assigned when CMR condition selected". |

Three differences from the legacy screens are deliberate, and this
walkthrough describes what Survsys shows rather than what EpiTrax showed:

- **The questions are written on the form.** EpiTrax kept a question bank on
  an admin screen of its own and drew a form's questions from it; Survsys
  writes each question, and a coded question's choices, inside the form that
  asks it.
- **A form is assigned when the case is opened.** A form marked
  **Auto-assign** is given to every case created on its condition from then
  on. Changing a case's disease later does not assign another form, and a
  form built after a case was opened is not given to that case.
- **A case's forms are on a Forms tab of their own.** The report form lists
  the forms assigned to the case there, and opens one to complete it.

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
   conditions, Lyme disease among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Paul Zimmerman among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself. The sixteen
   investigating agencies, Summit County among them, are built into the API
   tier and need no seeding.
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

This walkthrough **builds a form, creates a report and answers the form on
it**. Perform it twice and you have two forms of the same name for
Lyme disease, and the second report is given both; the end-to-end suite
empties the forms and the reports on every run, so a run always begins with
none.

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
   developer machine, <http://localhost:5000>. In the side menu, under **Reference data**, choose **Forms**. The screen is headed
   **Forms**, with a **New form** button above a table whose columns are
   **Name**, **Condition** and **Auto-assign**.
2. Press **New form**. The screen is headed **New form**, and its
   **Sections** panel reads **No sections yet. Add one to begin.**
3. In the box labelled **Name**, type `Lyme disease tick exposure`. Open
   the **Condition** list and choose **Lyme disease**; the list closes, and
   the **Condition** box reads **Lyme disease**. Tick **Auto-assign**.
4. Press **Add section**. In the new section's box labelled **Section
   title**, type `Tick exposure`.
5. Press **Add question** in that section. In the new question's box
   labelled **Question**, type `Where was the patient when the tick bite
   happened?`. Leave its **Answer type** at **Text**.
6. Press **Add question** again. In the second question's box labelled
   **Question**, type `Was a tick found attached to the patient?`. Open
   its **Answer type** list and choose **Coded**. A **Choices** list and an
   **Add choice** button appear under it.
7. Press **Add choice**, and in the new choice's **Code** box type `Y` and in
   its **Label** box type `Yes`. Press **Add choice** again, and in the second
   choice's **Code** box type `N` and in its **Label** box type `No`.
8. Press **Save**. You are back on the **Forms** screen, and the form's row
   reads, left to right: **Lyme disease tick exposure**, **Lyme disease**
   and **Yes**.
9. In the side menu, under **Cases**, choose **New CMR**. In the box labelled **Last
   name**, type `Zimmerman`, then press **Search**. The line above the table
   reads **1 person(s) found, showing 1.**, and the one row's **Last, First**
   reads **Zimmerman, Paul**. Press **Create CMR** on that row.
10. Under the heading, the line begins **Step 2 of 2. Reporting on Zimmerman,
    Paul**. Open the **Disease** list and choose **Lyme disease**, then open
    the **Investigating agency** list and choose **Summit County**. Each list
    closes, and its box reads what you chose.
11. Press **Save & Continue**. The report now exists, and the screen is its
    own form: in the header bar, **Patient name** reads **Zimmerman, Paul**
    and **Disease** reads **Lyme disease**. Note the address in your
    browser's address bar: it ends in `/event/edit/` and a number, and step
    16 comes back to it.
12. Press **Edit** at the top of the report, which opens it for editing, then press the **Forms** tab. Its **Assigned forms** table has one row,
    **Lyme disease tick exposure**, with an **Open** button.
13. Press **Open**. The form opens below the table under its name,
    **Lyme disease tick exposure**, with the section **Tick exposure**,
    a box labelled **Where was the patient when the tick bite happened?**
    and a list labelled **Was a tick found attached to the patient?**, both
    empty, and a **Save answers** button.
14. In the box labelled **Where was the patient when the tick bite
    happened?**, type `Hiking near Park City`. Open the list labelled **Was a
    tick found attached to the patient?** and choose **Yes**; the list
    closes, and the box reads **Yes**.
15. Press **Save answers**. A green message appears above the questions,
    reading **The answers have been saved.**
16. Go back to the report's address — the one you noted in step 11 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database, press the **Forms** tab, and press **Open** on
    **Lyme disease tick exposure**. The box **Where was the patient when the
    tick bite happened?** reads **Hiking near Park City**, and the list **Was
    a tick found attached to the patient?** reads **Yes**. That is how you
    know the answers reached the database.

## What you should see

A supplemental form built for a condition, given to a new case on it, and
completed:

- The form **Lyme disease tick exposure**, for **Lyme disease**,
  auto-assigned, with one section, **Tick exposure**, holding a text question
  and a coded question whose choices are **Yes** and **No**.
- A report on **Zimmerman, Paul** for **Lyme disease** whose **Forms** tab
  already lists that form when the report is first opened.
- The form opened on the case, answered **Hiking near Park City** and **Yes**,
  saved with **The answers have been saved.**, and read back the same when the
  report is opened again.

You now know how to complete the supplemental form a disease asks for on a
case. If what you saw differs from what is written above, the walkthrough has
failed: say so rather than adjusting what you did, because the promise this
document makes is that it is performable exactly as written.
