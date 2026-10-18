# Open an outbreak and link cases

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/OpenAnOutbreakAndLinkCasesTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As an investigator, I open an outbreak — its number, its name, the date it
> began, the disease and the agency that owns it — and link a case to it from
> the case's own report, so that the outbreak lists the cases it is made of
> and anyone who opens it reads the same cases I linked.

This is the legacy **Outbreaks** screen and the **Outbreak name** picker on
the report form's **Administrative** tab, and it is inferred from two legacy
screens, read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **Outbreaks** (`/nedss/outbreak/outbreaks/`) | `../../../nedss/docs/functional/epitrax-core-workflows.md`, *6. Outbreaks*, and `../../../nedss/docs/functional/phase-1.6-feature-matrix.md`, *5. Outbreaks* | Every outbreak by its number, name, disease, agency and status, with **+ Add Outbreak**; outbreak management is "create, track, link CMRs". |
| EpiTrax → the CMR edit form → **Administrative** tab → **Case / Outbreak** | `../../../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Administrative Tab* | The **Outbreak name** dropdown, writing `person_condition.outbreak_id`, with "No associated outbreak" as its default. |

Three differences from the legacy screens are deliberate, and this
walkthrough describes what Survsys shows rather than what EpiTrax showed:

- **The outbreaks screen is one table.** EpiTrax split its screen into
  *Followed Outbreaks* and *Outbreaks*; Survsys has no following yet, so it
  lists every outbreak once, newest first.
- **A case is linked from its report, and read from the outbreak.** The
  outbreak's own screen lists its linked cases but has nothing to link one
  with: the link is the report's, as it was in the legacy schema, so it is
  set where the legacy system set it.
- **The empty choice reads (none).** The legacy picker's "No associated
  outbreak" is **(none)** in Survsys, and choosing it clears the link.

The outbreak's agency access, its report and its activity log are on the same
screen and are not part of this walkthrough.

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
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people, An
   Nguyen among them; run the first and then the second against your
   database. The end-to-end suite seeds both itself. The sixteen
   investigating agencies, Utah County among them, are built into the API
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

This walkthrough **opens an outbreak, creates a report and links the report to
it**. Perform it twice and you have two outbreaks of the same name, each with
one case; nothing here prevents it, and the outbreak you open is the one whose
row you read in step 6 and whose name you press in step 17. The end-to-end
suite empties the outbreaks and the reports on every run, so a run always
begins with none.

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
   bar across the top of the window, press **Outbreaks**. The screen is
   headed **Outbreaks**, with a **New outbreak** button above a table whose
   columns are **Number**, **Name**, **Condition**, **Owning agency** and
   **Status**.
2. Press **New outbreak**. The screen is headed **New outbreak**.
3. In the box labelled **Number**, type `OB-2026-014`. In the box labelled
   **Name**, type `Provo church supper hepatitis A`. In the box labelled
   **Outbreak date**, enter the date `2026-09-12`.
4. Open the **Condition** list and choose **Hepatitis A**. The list closes,
   and the **Condition** box now reads **Hepatitis A**.
5. Open the **Owning agency** list and choose **Utah County**. The list
   closes, and the **Owning agency** box now reads **Utah County**. Leave
   **Status** at **Active** and **Description** empty.
6. Press **Save**. You are back on the **Outbreaks** screen, and the
   outbreak's row reads, left to right: **OB-2026-014**, **Provo church
   supper hepatitis A**, **Hepatitis A**, **Utah County** and **Active**.
7. In the application bar, press **New CMR**. In the box labelled **Last
   name**, type `Nguyen`, then press **Search**.
8. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Nguyen, An**. Press **Create CMR** on
   that row.
9. Under the heading, the line begins **Step 2 of 2. Reporting on Nguyen,
   An**.
10. Open the **Disease** list and choose **Hepatitis A**, then open the
    **Investigating agency** list and choose **Utah County**. Each list
    closes, and its box reads what you chose.
11. Press **Save & Continue**. The report now exists, and the screen is its
    own form: in the header bar, **Patient name** reads **Nguyen, An** and
    **Disease** reads **Hepatitis A**. Note the **Record number** in the
    header bar, and the address in your browser's address bar: it ends in
    `/event/edit/` and a number, and step 15 comes back to it.
12. Press the **Administrative** tab. Its **Case / Outbreak** section has a
    list labelled **Outbreak name**.
13. Open the **Outbreak name** list and choose **Provo church supper
    hepatitis A**. The list closes, and the box now reads **Provo church
    supper hepatitis A**.
14. Press **Save & Continue** at the bottom of the tab. A green message
    appears at the top of the tab, reading **The administrative data has been
    saved.**
15. Go back to the report's address — the one you noted in step 11 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database, and press the **Administrative** tab. The **Outbreak
    name** box reads **Provo church supper hepatitis A**.
16. In the application bar, press **Outbreaks**.
17. In the outbreak's row, press its name, **Provo church supper hepatitis
    A**. The screen is headed **Edit outbreak**, and its **Name** box reads
    **Provo church supper hepatitis A**.
18. The panel headed **Linked cases** is a table with the columns **Record
    number**, **Person** and **Condition**, and it has one row, reading the
    record number you noted in step 11, **Nguyen, An** and **Hepatitis A**.
    That is how you know the link reached the database.

## What you should see

An outbreak opened, and a case linked to it and read back from both sides:

- The outbreak **OB-2026-014**, **Provo church supper hepatitis A**, for
  **Hepatitis A**, owned by **Utah County**, **Active**, listed on the
  **Outbreaks** screen once **Save** is pressed.
- A report on **Nguyen, An** for **Hepatitis A**, whose **Administrative**
  tab's **Outbreak name** is set to the outbreak, saved with **The
  administrative data has been saved.**, and still set when the report is
  opened again.
- The outbreak's own screen listing that report under **Linked cases**, by its
  record number, its person and its condition.

You now know how to open an outbreak and link the cases it is made of. If what
you saw differs from what is written above, the walkthrough has failed: say so
rather than adjusting what you did, because the promise this document makes is
that it is performable exactly as written.
