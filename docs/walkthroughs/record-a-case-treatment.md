# Record a case treatment

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RecordACaseTreatmentTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I record the treatment a case was given — chosen from
> the treatments listed for the case's disease, with the dates it was started
> and stopped and its dose — so that the case carries what was done for the
> patient, and anyone who opens the report reads the same treatment I
> recorded.

Treatments on a case are inferred from the legacy documentation, read-only
here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| Treatment tracking | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *8. Treatments & Vaccines*, the row *Treatment tracking* | The drug or regimen given, with its dates, recorded per CMR. |
| Condition-specific treatments | the same table, the row *Condition-specific treatments* | The treatment list a case chooses from is filtered by its disease. |
| EpiTrax → **Admin** → **Treatments** | `../nedss/docs/functional/epitrax-core-workflows.md`, *Key Admin Screens*, the row *Treatments* | The treatment reference data a case's treatments are chosen from. |

**A treatment is listed first.** A case's treatment is chosen from the
treatments listed for its disease, so this walkthrough first adds one on the
**Treatments** screen, as the *Record a treatment* walkthrough does, and then
opens a case and records it.

Two differences from the legacy system are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **Treatments are a tab of their own.** The case morbidity report carries a
  **Treatments** tab: a table of the treatments recorded on the case, and a
  form under it that adds one.
- **The list is the disease's, unless you ask for all.** The **Treatment**
  list offers the treatments listed for the case's disease; the switch beside
  it offers every treatment instead. This walkthrough leaves it off.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person the case is opened on, **Rodriguez, Maria**, is one of the seven
   the people seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`),
   and the condition, **Lyme disease**, is one of the seven the conditions
   seed inserts (`Survsys.IntegrationTests/Fixtures/conditions.sql`); run both
   against your database if you brought it up by hand.
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

This walkthrough **adds a treatment, opens a case and records the treatment on
it**. Perform it twice and you have two treatments named Amoxicillin and two
cases; the **Treatment** list then offers the name twice. The end-to-end suite
empties the treatments and the cases on every run, so a run always begins with
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
   developer machine, <http://localhost:5000>. In the side menu, under **Reference data**, choose **Treatments**, then press **New
   treatment**. The screen is headed **New treatment**.
2. In the box labelled **Name**, type `Amoxicillin`. Leave **Active** ticked
   and **Default** unticked, and under **Conditions** tick **Lyme disease**.
3. Press **Save**. You are back on the **Treatments** screen, and the
   treatment's row reads, left to right: **Amoxicillin**, **Yes**, **No** and
   **1**.
4. In the side menu, under **Cases**, choose **New CMR**. In the box labelled **Last
   name**, type `Rodriguez`, then press **Search**.
5. The line above the table reads **2 person(s) found, showing 2.** Press
   **Create CMR** on the row reading **Rodriguez, Maria**.
6. Open the **Disease** list and choose **Lyme disease**. Open the
   **Investigating agency** list and choose **Wasatch County**, then press
   **Save & Continue**.
7. The report's own form opens. In the header bar, **Patient name** reads
   **Rodriguez, Maria** and **Disease** reads **Lyme disease**. Note the
   address in your browser's address bar: it ends in `/event/edit/` and a
   number, and step 11 comes back to it.
8. Press **Edit** at the top of the report, which opens it for editing, then
   press the **Treatments** tab. Its table, headed **Treatments**, has the
   columns **Treatment**, **Started**, **Stopped**, **Dose** and **Comment**,
   and reads **No treatments are recorded for this case.** Under it is the
   form headed **Add a treatment**.
9. Open the **Treatment** list and choose **Amoxicillin**. In the box
   labelled **Start date**, enter `2026-10-08`; in **Stop date**, enter
   `2026-10-29`; in **Dose**, type `500 mg three times daily`. Leave
   **Comment** empty.
10. Press **Add treatment**. A green message reads **The treatment record has
    been saved.**, the form is empty again, and the table has one row,
    reading, left to right: **Amoxicillin**, **2026-10-08**, **2026-10-29**,
    **500 mg three times daily** and an empty **Comment**, with **Edit** and
    **Remove** buttons.
11. Go back to the report's address — the one you noted in step 7 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database, and press the **Treatments** tab. The table has the
    one row, exactly as in step 10. That is how you know the treatment
    reached the database.

## What you should see

A treatment recorded on a case, chosen from its disease's list:

- The treatment **Amoxicillin**, listed for **Lyme disease**.
- A case on **Rodriguez, Maria** for **Lyme disease**, whose **Treatments**
  tab records **Amoxicillin** started **2026-10-08**, stopped **2026-10-29**,
  at **500 mg three times daily**.
- The same row read back from the database when the report is opened again.

You now know how to record the treatment a case was given. If what you saw
differs from what is written above, the walkthrough has failed: say so rather
than adjusting what you did, because the promise this document makes is that
it is performable exactly as written.
