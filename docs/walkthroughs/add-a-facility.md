# Add a facility

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/AddAFacilityTests.cs`, and a step you cannot perform is a
defect in the application or in this document — never something to work
around.

## User story

> As a surveillance administrator, I add a facility — a hospital, laboratory
> or clinic — under the health department whose jurisdiction it is in, with
> its address — so that the facility is master data a case can later name, and
> it is filed under the agency that owns it.

This is administration, not case work: facilities are reference data, and
adding one is inferred from two legacy sources, both read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → Admin → **Facilities** (`/nedss/admin/*`) | `../nedss/docs/functional/epitrax-core-workflows.md`, *Admin Pages → Key Admin Screens*, the **Facilities (admin)** row, and the screenshot `../nedss/docs/screenshots/session-007/23-admin-facilities-admin.png` | *Facility master data management* — the admin screen on which a facility is added and edited, through the admin section's shared list-and-edit pattern. |
| EpiTrax → **Facilities** search | `../nedss/docs/functional/epitrax-core-workflows.md`, the facilities search section, and the screenshot `../nedss/docs/screenshots/session-007/06-facilities.png` | The legacy facilities list, whose results table carries *Facility ID, Facility, Facility types, Agency* — a facility under the investigating agency it sits beneath, which is the pairing Survsys keeps. |

Four differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **A facility must name the agency it sits under**, chosen from the agencies
  on file. The screen refuses to save a facility with no agency, with the
  complaint **Choose the agency this facility sits under.**; this walkthrough
  always chooses one, so you never see it.
- **Survsys reads an address before it stores it.** The **State** is chosen
  from the list of states — the reference code list an administrator keeps
  on the **Code lists** screen — and stored as its two-letter code, so
  **Utah** is stored as `UT`; the **Zip** is five digits or a ZIP+4, stored
  as its five-digit zip; a blank optional box is stored as nothing. A value
  that is not a state code or not a zip code is refused rather than saved.
- **The facilities list is one page**, in name order, with the columns
  **Name**, **Agency**, **City** and **Zip** — the agency by name, and an em
  dash (`—`) where a facility has no city or no zip on file. The legacy list
  carried more columns and the richest search page in the system; Survsys
  shows the four that find a facility.
- **The facility you add is the only one on file.** A fresh Survsys has no
  facilities — the migration seeds the agencies but no facility — so after
  you add one the list holds exactly it. (The end-to-end suite clears the
  facilities table before each run for the same reason, so the one it adds is
  the one it reads back.)

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   sixteen agencies this walkthrough's facility sits under are inserted by
   that migration, so a database at migration level has them with no fixture
   to run.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do.
   On a fresh database the only user is the administrator the API tier
   creates on its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. That
   administrator holds the Administrator role, which has every privilege.
   Open the application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level, clears the facilities table, and performs the steps below in
a real browser. It records video of every test it runs, and writes the
recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as
`.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Reference data**, choose **Facilities**. (The facilities
   screen is also at `/facilities`.)
2. The browser tab reads **Facilities — Survsys**, and below the application
   bar is the heading **Facilities**. Above the list is a **New facility**
   button.
3. Press **New facility**.
4. The browser tab reads **New facility — Survsys**, and the heading reads
   **New facility**. Below it is the facility's form.
5. Fill the form in, box by box:
   - **Name**: `Davis County Health Clinic`
   - **Short name**: `DCHC`
   - **Agency**: choose **Davis County** from the list.
   - **Street**: `22 South State Street`
   - **City**: `Clearfield`
   - **State**: choose **Utah** from the list.
   - **Zip**: `84015`
   - **Phone**: `801-525-5000`
6. Press **Save**. The screen goes back to the **Facilities** list, under the
   heading **Facilities**, which is how you know the save succeeded.
7. Read the line of small text above the list. It reads **1 facility(ies),
   showing 1.** — the facility you added is the only one on file.
8. Read the four column headings of the table, left to right: **Name**,
   **Agency**, **City** and **Zip**. The one row reads:

   | Name | Agency | City | Zip |
   |---|---|---|---|
   | Davis County Health Clinic | Davis County | Clearfield | 84015 |

9. Press **Davis County Health Clinic** to open it and read back what the
   save stored.
10. The heading reads **Edit facility**, and the form reads back what you
    entered: **Name** *Davis County Health Clinic*, **Short name** *DCHC*,
    **Agency** *Davis County*, **Street** *22 South State Street*, **City**
    *Clearfield*, **State** *Utah*, **Zip** *84015* and **Phone**
    *801-525-5000*.

## What you should see

A facility added under its agency, and read back:

- The heading **Facilities**, under an application bar reading **Survsys**,
  with a **New facility** button.
- After you fill the form for **Davis County Health Clinic** under **Davis
  County** and press **Save**, the list comes back reading **1 facility(ies),
  showing 1.** with the one row above: the facility's name, its agency
  **Davis County**, its city **Clearfield** and its zip **84015**.
- Opening the facility reads the whole record back under the heading **Edit
  facility**, with the **State** reading **Utah**.

You now know how to add a facility under the agency that owns it, with its
address. If what you saw differs from what is written above, the walkthrough
has failed: say so rather than adjusting what you did, because the promise
this document makes is that it is performable exactly as written.
