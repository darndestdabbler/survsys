# Record demographics

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RecordDemographicsTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I record a patient's demographics on their case
> morbidity report — their name, age, addresses, telephones, email addresses,
> race and the rest — and save them, so that the case carries who the patient
> is and how to reach them.

This is the first tab of the legacy report form, and it is inferred from one
legacy screen and one legacy database snapshot, both read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Demographic** tab | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *1. Demographic (37 fields)*, and the screenshots `../nedss/docs/screenshots/session-008/07-cmr-tab-1-Demographic.png`, `../nedss/docs/screenshots/session-010/06-demographics-filled.png` and `07-after-demographics-save.png` | Six sections — **Name**, **Age**, **Addresses**, **Telephones**, **Email Addresses** and **Demographics** — and a **Save & Continue** button. |
| The legacy database after a demographics save | `../nedss/docs/db-snapshots/session-010/03-after-demographics-save.txt` | The one person these snapshots hold, Maria Elena Rodriguez, with her address, telephone and email exactly as the legacy form saved them. She is the person this walkthrough records against, copied value for value into `Survsys.IntegrationTests/Fixtures/persons.sql`. |

**The report is opened first.** Demographics live on the case morbidity
report, because that is where the legacy flow records them: the Demographic
tab is the first tab of the report form, and there is no other screen for
them. So this walkthrough begins by opening a report on Maria Rodriguez — the
same two steps the *Create a case morbidity report* walkthrough describes —
and then records her demographics on it.

Three differences from the legacy tab are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **The coded fields are lists, showing each code by its name.** Birth sex,
  current gender, primary language, ethnicity, country of birth, state,
  county, residence type and telephone type are chosen from the reference
  code lists an administrator keeps on the **Code lists** screen, seeded from
  the legacy lookup tables. What is stored is the legacy code — `F`, `es`,
  `H`, `MEX`, `UT`, `SL` — and what the list shows is its name: **Female**,
  **Spanish**, **Hispanic or Latino**, **Mexico**, **Utah**, **Salt Lake**.
- **Race is one list per race**, with **Add race** and **Remove**, rather
  than the legacy multi-select; the races are shown in alphabetical order by
  code.
- **One Save & Continue saves the whole tab.** Every section is sent as one
  record and saved together; there is no save per address or per telephone,
  and no *Save as Address at Diagnosis* button or *Other Addresses* link.

The legacy tab's *Age at event date*, its dropdowns and its address-at-
diagnosis button are not part of this walkthrough. Survsys does not have them
yet.

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
   Maria Rodriguez among them with the address, telephone, email, races,
   alias and guardian this walkthrough reads; run the first and then the
   second against your database. The end-to-end suite seeds both itself; a
   database you brought up by hand does not have them until you run both.
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

This walkthrough **changes Maria Rodriguez's record**: it gives her a new
unit number, a second telephone and a second email address, and it opens a
report on her. It changes nothing in her name, date of birth or birth sex,
so the *Find a person* walkthrough reads the same seven people before and
after it, whichever order the walkthroughs are performed in. Steps 12 and 13
read her record as the fixture seeds it, so **to perform this walkthrough
again against the same database, reseed it first**: empty the person tables
and their reports, then run `conditions.sql` and `persons.sql` again, which
puts her record back as those steps read it. Running `persons.sql` on its own
would add a second Maria Rodriguez rather than restore the first. The
end-to-end suite reseeds exactly that way on every run.

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
2. In the box labelled **Last name**, type `Rodriguez`; in the box labelled
   **First name**, type `Maria`; then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Rodriguez, Maria**.
4. Press **Create CMR** on that row.
5. Under the heading, the line begins **Step 2 of 2. Reporting on Rodriguez,
   Maria**.
6. Open the **Disease** list and choose **Giardiasis**. The list closes, and
   the **Disease** box now reads **Giardiasis**.
7. Open the **Investigating agency** list and choose **Utah State**. The
   list closes, and the **Investigating agency** box now reads **Utah
   State**.
8. Press **Save & Continue**.
9. The report now exists, and the screen is its own form. In the header bar,
   **Patient name** reads **Rodriguez, Maria** and **Disease** reads
   **Giardiasis**. Under the CDC notification panel, the tab strip shows
   eleven tabs, left to right: **Demographic**, **Clinical**, **Laboratory**,
   **Investigation**, **Administrative**, **Forms**, **Vaccines**,
   **Treatments**, **Encounters**, **Contacts** and **Notes**, and the
   **Demographic** tab is open. Note the
   address in your browser's
   address bar: it ends in `/event/edit/` and a number, and step 19 comes
   back to it.
10. Read the **Name** section at the top of the tab. **Last name** reads
    **Rodriguez**, **First name** reads **Maria** and **Middle name** reads
    **Elena**. Under them, **Parent/Guardian** has one row, reading **Carlos
    Rodriguez**, and **Aliases** has one row, reading **Maria Elena
    Garcia**.
11. Read the **Age** section. **Date of birth** reads **1985-06-15**, and
    **Current age** shows her age in whole years, counted from that date to
    today. (**Age at onset** shows the same number, because the report's
    event date is the day it was opened.)
12. Read the **Addresses** section. It has one address: **Street** reads
    **1234 Main Street**, **Unit number** reads **Apt 5B**, **City** reads
    **Salt Lake City**, **State** reads **Utah**, **Zip code** reads
    **84101** and **County** reads **Salt Lake**.
13. Read the **Telephones** section and the **Email Addresses** section. There
    is one telephone, whose **Area** reads **801** and whose **Phone** reads
    **5551234**, and one email address, whose **Email address** reads
    **maria.rodriguez@example.com**.
14. Read the **Demographics** section. **Birth sex** reads **Female**,
    **Current gender** reads **Female**, **Primary language** reads
    **Spanish**, **Ethnicity** reads **Hispanic or Latino** and **Country of
    birth** reads **Mexico**. Under them, **Race** has two rows, in
    alphabetical order by code: **American Indian** (`AA`), then **White**
    (`W`).
15. Press **Edit** at the top of the report, which opens it for editing. In the **Addresses** section, change **Unit number** from `Apt 5B` to
    `Apt 7C`.
16. In the **Telephones** section, press **Add telephone**. A second,
    empty telephone row appears under the first. In that new row, type
    `801` in **Area** and `5559876` in **Phone**.
17. In the **Email Addresses** section, press **Add email address**. A
    second, empty row appears under the first. In that new row, type
    `maria.e.rodriguez@example.com` in **Email address**.
18. Press **Save & Continue** at the bottom of the tab. A green message
    appears at the top of the tab, reading **The demographics have been
    saved.**
19. Go back to the report's address — the one you noted in step 9 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database rather than from what you typed. The report form opens
    on its **Demographic** tab, and in it **Unit number** reads **Apt 7C**;
    **Telephones** has two rows, the first with **Area** **801** and
    **Phone** **5551234**, the second with **Area** **801** and **Phone**
    **5559876**; and **Email Addresses** has two rows, reading
    **maria.rodriguez@example.com** and then
    **maria.e.rodriguez@example.com**. That is how you know the save reached
    the database.

## What you should see

Maria Rodriguez's demographics, on her case morbidity report, read and then
changed and saved:

- A report on **Rodriguez, Maria** for **Giardiasis**, opened in the two
  steps, ending on its form with the **Demographic** tab open.
- The tab already holding what is on file for her: her three names, her
  guardian and her alias, her date of birth with her age counted from it,
  her address, her telephone, her email address, her five coded fields and
  her two races, **American Indian** and **White**.
- After changing her unit number to **Apt 7C** and adding a telephone and an
  email address, **Save & Continue** answers **The demographics have been
  saved.**
- Opening the report's address again reads **Apt 7C**, two telephones and
  two email addresses back from the database.

You now know how to record who a patient is and how to reach them, on the
report the case is filed against. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
