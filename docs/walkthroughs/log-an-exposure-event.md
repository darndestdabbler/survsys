# Log an exposure event

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/LogAnExposureEventTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I log the exposure event a case is thought to have come
> from — what it was, when it ran from and to, and where it happened — so that
> the case carries the event the investigation is about, and anyone who opens
> the report reads the same event I recorded.

This is the legacy **+ Exposure** panel of the report form's **Investigation**
tab, shown on the **Contacts** tab where its contacts are read, and it is
inferred from two legacy screens, read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Investigation** tab → **+ Exposure** | `../../../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Exposure Form*, and the screenshot `../../../nedss/docs/screenshots/session-012/05-exposure-form.png` | A panel that opens on the Investigation tab and writes the `exposure` table together with its related `exposure_place`, `exposure_contact` and `exposure_source` rows. |
| EpiTrax → the CMR edit form → **Contacts** tab | the same document, *Contacts Tab*, and the screenshot `../../../nedss/docs/screenshots/session-012/12-contacts-tab-initial.png` | A data table of what the exposures link — each contact's person, contact type, disposition and disposition date — above a panel for adding to them. |

**One tab, not two.** Survsys shows the exposure and what it links on one
**Contacts** tab, because an exposure and its contacts are written as one
thing: the API tier records an exposure whole, with its place, its contacts
and its sources, and has no endpoint for adding to a stored one. The legacy
system split the writing from the reading across two tabs; this walkthrough
logs the event, and the *Add a contact to a case* walkthrough logs one with a
contact on it.

Four differences from the legacy panel are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **An exposure is recorded whole, and then not edited.** One **Record
  exposure** button writes the event, its place, every contact under it and
  every source under it together. Once recorded, an exposure is listed and
  not changed: Survsys has no screen to alter or remove a recorded exposure,
  and none to set a contact's disposition afterwards.
- **The coded fields are chosen from lists.** The place type and state, the
  contact's type and disposition, and a source's type are lists over the
  reference code lists an administrator keeps on the **Code lists** screen,
  seeded from the legacy lookup tables. This walkthrough chooses the place
  type and leaves the rest empty.
- **Nothing on an exposure is required.** Survsys records an exposure with
  whatever was entered, and refuses only a date that is not a date, a time
  range that ends before it starts, and a contact that names no person.
- **The newest exposure is listed first.** What you just recorded is the top
  row of the table, so it is the row you read.

The legacy panel's contacts and sources, which the Survsys form also takes,
are not part of this walkthrough; the *Add a contact to a case* walkthrough is
where a contact is entered.

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
   conditions, Giardiasis among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Chidi Okafor among them; run the first and then the second against your
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

This walkthrough **creates a report and records an exposure on it**. Perform
it twice and you have two reports on Chidi Okafor, each with one exposure;
that is what the legacy system does too, and nothing here prevents it. It
changes nothing about the person, so the *Find a person* walkthrough reads the
same seven people before and after it. The end-to-end suite empties the
exposures and the reports along with the people it reseeds on every run, so a
run always begins with none.

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
2. In the box labelled **Last name**, type `Okafor`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Okafor, Chidi**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Okafor,
   Chidi**.
6. Open the **Disease** list and choose **Giardiasis**. The list closes, and
   the **Disease** box now reads **Giardiasis**.
7. Open the **Investigating agency** list and choose **Salt Lake County**.
   The list closes, and the **Investigating agency** box now reads **Salt
   Lake County**.
8. Press **Save & Continue**.
9. The report now exists, and the screen is its own form. In the header bar,
   **Patient name** reads **Okafor, Chidi** and **Disease** reads
   **Giardiasis**. Under the CDC notification panel, the tab strip shows
   eleven tabs, left to right: **Demographic**, **Clinical**, **Laboratory**,
   **Investigation**, **Administrative**, **Forms**, **Vaccines**,
   **Treatments**, **Encounters**, **Contacts** and **Notes**. Note
   the address in your browser's address bar: it ends in `/event/edit/` and a
   number, and step 14 comes back to it.
10. Press the **Contacts** tab. The tab opens on two panels. The first,
    headed **Exposures**, is a table with the columns **Name**, **From**,
    **To**, **Place**, **Contacts**, **Sources** and **Actions**, and its one
    row reads
    **No exposures recorded yet.** The second, headed **Record an
    exposure**, is the entry form: the exposure's own boxes, then a **Place**
    section, then a **Contacts** section with an **Add contact** button and
    nothing under it, then a **Sources** section with an **Add source**
    button and nothing under it, then a **Record exposure** button.
11. In the box labelled **Name**, type `Lakeside Community Pool outing`. In
    the box labelled **Exposure start**, enter the date `2026-09-18`, and in
    the box labelled **Exposure end**, enter the date `2026-09-20`. Leave
    **Description** empty.
12. Under **Place**, open the **Place type** list and choose **Pool**; in
    the box labelled **Place name**, type `Lakeside Community Pool`; in the
    box labelled **Street**, type `88 Lakeshore Drive`; and in the box
    labelled **City**, type `Provo`. Leave the other place boxes —
    **State**, **Zip**, **Latitude**, **Longitude** and **Phone** — empty.
13. Press **Record exposure** at the bottom of the form. A green message
    appears at the top of the tab, reading **The exposure has been
    recorded.** The **Exposures** table now has one row, reading, left to
    right: **Lakeside Community Pool outing**, **2026-09-18**,
    **2026-09-20**, **Lakeside Community Pool**, a dash in each of
    **Contacts** and **Sources**, and the buttons **Add contact**, **Edit**
    and **Delete** under **Actions**. The entry form is empty again: **Name** is
    blank, **Place name** is blank, and there is no contact under
    **Contacts** and no source under **Sources**.
14. Go back to the report's address — the one you noted in step 9 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database rather than from what you typed, and press the
    **Contacts** tab. The **Exposures** table has the one row, reading
    **Lakeside Community Pool outing**, **2026-09-18**, **2026-09-20**,
    **Lakeside Community Pool** and two dashes, exactly as in step 13. That
    is how you know the exposure reached the database.

## What you should see

An exposure event on Chidi Okafor's case morbidity report, logged and read
back:

- A report on **Okafor, Chidi** for **Giardiasis**, opened in the two steps,
  ending on its form with eleven tabs, **Contacts** among them.
- The **Contacts** tab with no exposures yet, and an entry form that takes an
  exposure, a place under it, and contacts and sources under that.
- After entering **Lakeside Community Pool outing**, running
  **2026-09-18** to **2026-09-20** at the pool **Lakeside Community Pool**, **88
  Lakeshore Drive**, **Provo**, **Record exposure** answers **The exposure
  has been recorded.** and lists the exposure as the table's one row, with a
  dash under **Contacts** and under **Sources** because this walkthrough
  entered neither.
- Opening the report's address again and pressing **Contacts** reads the same
  one row back from the database.

You now know how to log the exposure event an investigation is about, on the
report the case is filed against. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
