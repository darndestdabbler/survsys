# Record a treatment

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RecordATreatmentTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As a surveillance administrator, I record a treatment — its name, whether it
> is in use, and the diseases it is given for — so that the treatment list
> each disease offers is the one its investigators should choose from, and
> anyone who opens the treatment reads the same diseases I listed it for.

This is the legacy **Treatments** admin screen, and it is inferred from the
legacy documentation, read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **Admin** → **Treatments** | `../../../nedss/docs/functional/epitrax-core-workflows.md`, *Key Admin Screens*, the row *Treatments* (screenshot `17-admin-treatments`) | The treatment reference data. |
| Condition-specific treatments | `../../../nedss/docs/functional/phase-1.6-feature-matrix.md`, *8. Treatments & Vaccines* | "Treatment list filtered by disease": each treatment listed for the conditions it is given for. |

One difference from the legacy system is deliberate, and one boundary is worth
knowing; this walkthrough describes what Survsys shows rather than what
EpiTrax showed:

- **A treatment's diseases are ticked on the treatment.** The conditions it is
  listed for are a row of tick boxes on its own screen, one per condition, and
  **Save** writes the treatment and its list together.
- **This is the catalogue, not a case's treatment.** This walkthrough
  records the treatment itself, in the list a disease's treatments are drawn
  from. Which treatment was given on a case, at what dates and dose, is
  recorded on the case morbidity report's **Treatments** tab, which is its
  own walkthrough, *Record a case treatment*.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The diseases this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the seven
   conditions, Bartonellosis and Lyme disease among them; run it against your
   database. The end-to-end suite seeds it itself.
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

This walkthrough **adds a treatment**. Perform it twice and you have two
treatments of the same name; nothing here prevents it, and the treatments
list shows both. The end-to-end suite empties the treatments on every run, so
a run always begins with none.

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
   developer machine, <http://localhost:5000>. In the side menu, under **Reference data**, choose **Treatments**. The screen is
   headed **Treatments**, with a **New treatment** button above a table whose
   columns are **Name**, **Active**, **Default** and **Conditions**.
2. Press **New treatment**. The screen is headed **New treatment**. **Active**
   is already ticked and **Default** is not, and under the heading
   **Conditions** there is one tick box for each of the seven conditions,
   none of them ticked.
3. In the box labelled **Name**, type `Doxycycline`. Leave **Active** ticked
   and **Default** unticked.
4. Under **Conditions**, tick **Bartonellosis** and **Lyme disease**.
5. Press **Save**. You are back on the **Treatments** screen, and the
   treatment's row reads, left to right: **Doxycycline**, **Yes**, **No** and
   **2**.
6. Press the treatment's name, **Doxycycline**. The screen is headed **Edit
   treatment**, and it is read afresh from the database: the **Name** box
   reads **Doxycycline**, **Active** is ticked, **Default** is not, and under
   **Conditions** exactly **Bartonellosis** and **Lyme disease** are ticked.
   That is how you know the treatment and its list reached the database.

## What you should see

A treatment recorded, and listed for the diseases it is given for:

- The treatment **Doxycycline**, active and not a default, listed for
  **Bartonellosis** and **Lyme disease**.
- Its row on the **Treatments** screen once **Save** is pressed, counting the
  **2** conditions it is listed for.
- Its own screen, opened from that row, with the same two conditions ticked
  and no other.

You now know how to record a treatment and the diseases it is given for. If
what you saw differs from what is written above, the walkthrough has failed:
say so rather than adjusting what you did, because the promise this document
makes is that it is performable exactly as written.
