# Add a contact to a case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/AddAContactToACaseTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I add a contact to a case — the person who was exposed
> alongside the patient, what kind of contact they were, and how their
> follow-up was disposed of — so that the people this case reached are on the
> case, and anyone who opens the report reads the same contact I recorded.

This is the **Contacts** tab of the legacy report form, and it is inferred
from three legacy screens, read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Contacts** tab | `../../../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Contacts Tab*, and the screenshot `../../../nedss/docs/screenshots/session-012/12-contacts-tab-initial.png` | An **Adding Contacts** panel with a person-search autocomplete, a **+ New** button, a box for linking an orphan contact by record number and an **Import** button, above a data table of the case's contacts with their person, contact type, contact disposition and disposition date. |
| The same tab, with contacts on it | the same document, and `../../../nedss/docs/screenshots/session-012/15-contacts-tab-explored.png` | The contacts data table filled: one row per contact, carrying the person and the three coded values the follow-up records. |
| EpiTrax → the Contacts tab → **+ New** | the same document, *Key Discovery*, and `../../../nedss/docs/screenshots/session-012/13-new-contact-form.png` | **+ New** does not open a small dialog: it navigates to the full person creation page at `/nedss/morbidity/create`. A contact is a whole person record. |

**A contact hangs off an exposure.** The legacy *Key Discovery* is that a
contact is a full `person` record and that the link between that person and
the case is a row in `exposure_contact` — so the thing that carries a contact
is the exposure that links it, not the case directly. Survsys follows that:
an exposure is recorded whole, with its contacts under it, and the Contacts
tab lists each contact inside the exposure that links it. So this walkthrough
records an exposure in order to add the contact to it, with the exposure
reduced to a name and a date range; the *Log an exposure event* walkthrough is
the one about the event itself, with its place.

**A contact is an existing person, named by the identifier the person search
returns.** No person is created here, which is why this walkthrough begins on
the **People** screen, looking up the number the system holds for the person
who was exposed. The legacy **+ New** button goes to the whole person
creation page precisely because a contact is a person; Survsys has that
screen's search and not yet its creation, so a contact is someone already on
file. That number is assigned by the database, so it is not the same number on
every machine: you read it off the screen rather than knowing it in advance.

Four differences from the legacy tab are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The contact is entered with the exposure, and then not edited.** One
  **Record exposure** button writes the event and every contact under it
  together. There is no screen to add a contact to a stored exposure, and
  none to set a contact's disposition afterwards: the API tier has no
  endpoint for either.
- **The person is a plain number box, not an autocomplete.** The legacy
  panel searches as you type; here you look the number up on the People
  screen and type it. A number that is nobody's is refused by the API tier
  and shown as the entry error.
- **The coded fields are plain boxes holding the legacy values.** The
  contact's type and disposition are boxes that take the code or the number
  the legacy database holds — `contact_type` and `contact_disposition_type`
  — not dropdowns over a table of names. Those reference tables arrive with
  the sessions that own them.
- **Nothing on a contact is required but the person.** Survsys records a
  contact with whatever else was entered, and refuses only a date that is not
  a date, a time range that ends before it starts, and a contact that names
  no person.

The legacy panel's orphan-contact linking and its **Import** button are not
part of this walkthrough; batch contact import is the API tier's CSV endpoint
and has no screen in this release.

## Before you start

You need all four of these. The last one is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The two people and the disease this walkthrough names, in that
   database.** `Survsys.IntegrationTests/Fixtures/conditions.sql` seeds the
   seven conditions, Hepatitis A among them, and
   `Survsys.IntegrationTests/Fixtures/persons.sql` seeds the seven people,
   Paul Zimmerman — the patient — and Maria Rodriguez — the contact — among
   them; run the first and then the second against your database. The
   end-to-end suite seeds both itself; a database you brought up by hand does
   not have them until you run both. The sixteen investigating agencies are
   built into the API tier and need no seeding.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>. On a deployed site it is the
   address of the Survsys web site, which your deployment supplies.

This walkthrough **creates a report and records an exposure with one contact
on it**. Perform it twice and you have two reports on Paul Zimmerman, each
with one exposure and one contact; that is what the legacy system does too,
and nothing here prevents it. It changes nothing about either person, so the
*Find a person* walkthrough reads the same seven people before and after it.
The end-to-end suite empties the exposures, their contacts and the reports
along with the people it reseeds on every run, so a run always begins with
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
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **People**. The screen that opens
   is headed **People**, and the line under the heading says that leaving
   every box empty lists everyone on file.
2. In the box labelled **Last name**, type `Rodriguez`, then press
   **Search**.
3. The line above the table reads **2 person(s) found, showing 2.**, and the
   two rows' **Last, First** read **Rodriguez, Carlos** and **Rodriguez,
   Maria**, in that order. **Note the number in the ID column of the
   Rodriguez, Maria row** — step 14 types it. It is the identifier the
   system holds for her, assigned by the database, so it is a number you
   read here rather than one this document can name.
4. In the application bar, press **New CMR**. The screen that opens is
   headed **New case morbidity report**, and the line under the heading
   begins **Step 1 of 2.**
5. In the box labelled **Last name**, type `Zimmerman`, then press
   **Search**.
6. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Zimmerman, Paul**.
7. Press **Create CMR** on that row.
8. Under the heading, the line begins **Step 2 of 2. Reporting on Zimmerman,
   Paul**.
9. Open the **Disease** list and choose **Hepatitis A**. The list closes, and
   the **Disease** box now reads **Hepatitis A**.
10. Open the **Investigating agency** list and choose **Salt Lake County**.
    The list closes, and the **Investigating agency** box now reads **Salt
    Lake County**.
11. Press **Save & Continue**. The report now exists, and the screen is its
    own form. In the header bar, **Patient name** reads **Zimmerman, Paul**
    and **Disease** reads **Hepatitis A**. Note the address in your browser's
    address bar: it ends in `/event/edit/` and a number, and step 16 comes
    back to it.
12. Press the **Contacts** tab. The tab opens on two panels: **Exposures**,
    a table whose one row reads **No exposures recorded yet.**, and **Record
    an exposure**, the entry form, whose **Contacts** section has an **Add
    contact** button with nothing under it.
13. In the box labelled **Name**, type `Family dinner, 12 Oak Street`. In the
    box labelled **Exposure start**, enter the date `2026-09-10`, and in the
    box labelled **Exposure end**, enter the date `2026-09-12`. Leave
    **Description** and every box under **Place** empty.
14. Under **Contacts**, press **Add contact**. A contact block appears above
    the button, with the boxes **Person**, **Contact type**, **Disposition**,
    **Disposition date**, **From** and **To**, and a **Remove** button beside
    them. In the box labelled **Person**, type the number you noted at step
    3. In the box labelled **Contact type**, type `household`. In the box
    labelled **Disposition**, type `3`. In the box labelled **Disposition
    date**, enter the date `2026-09-22`. Leave **From** and **To** empty.
15. Press **Record exposure** at the bottom of the form. A green message
    appears at the top of the tab, reading **The exposure has been
    recorded.** The **Exposures** table now has one row: its **Name** reads
    **Family dinner, 12 Oak Street**, its **From** reads **2026-09-10**, its
    **To** reads **2026-09-12**, and its **Place** and **Sources** each read
    a dash. Its **Contacts** column is no longer a dash but a table of its
    own, with the columns **Person**, **Type**, **Disposition**,
    **Disposition date**, **From** and **To**, and one row reading, left to
    right: the number you noted at step 3, **household**, **3**,
    **2026-09-22**, and a dash in each of **From** and **To**. The entry
    form is empty again: **Name** is blank and there is no contact block
    under **Contacts**.
16. Go back to the report's address — the one you noted in step 11 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database rather than from what you typed, and press the
    **Contacts** tab. The **Exposures** table has the one row, and its
    **Contacts** table has the one contact row, reading the number you noted,
    **household**, **3**, **2026-09-22** and two dashes, exactly as in step
    15. That is how you know the contact reached the database.

## What you should see

A contact on Paul Zimmerman's case morbidity report, added and read back:

- Maria Rodriguez's identifier, read off the **People** screen, which is what
  names her as the contact: no person is created by this walkthrough.
- A report on **Zimmerman, Paul** for **Hepatitis A**, opened in the two
  steps, with its **Contacts** tab holding no exposures yet.
- After entering the exposure **Family dinner, 12 Oak Street**, running
  **2026-09-10** to **2026-09-12**, with one contact naming Maria's
  identifier as a **household** contact, disposition **3**, disposed
  **2026-09-22**, **Record exposure** answers **The exposure has been
  recorded.** and lists the exposure with that contact inside it.
- Opening the report's address again and pressing **Contacts** reads the same
  exposure and the same contact back from the database.

You now know how to put the people a case reached onto the case, through the
exposure that links them. If what you saw differs from what is written above,
the walkthrough has failed: say so rather than adjusting what you did,
because the promise this document makes is that it is performable exactly as
written.
