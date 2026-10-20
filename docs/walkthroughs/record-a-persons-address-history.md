# Record a person's address history

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/RecordAPersonsAddressHistoryTests.cs`, and a step you
cannot perform is a defect in the application or in this document — never
something to work around.

## User story

> As an investigator, I record where a patient lives now and where they lived
> before, each with the dates they lived there, so that the case carries the
> address the patient had when they fell ill as well as the one to reach them
> at today.

This is inferred from the legacy documentation, read-only here and never
modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → person addresses | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, the *Address management* row, and `../nedss/docs/functional/phase-1.6-cross-system-data-model.md`, `person_address` | A person's addresses are a history, each address with the dates it began and ended, rather than one address overwritten when they move. |
| EpiTrax → the CMR edit form → **Demographic** tab | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *1. Demographic (37 fields)* | Addresses are recorded in the **Addresses** section of the Demographic tab, with an *Earliest known* and a *Moved* date. |

Two things Survsys does on save are what this walkthrough shows:

- **One address is current and the rest are historic.** An address with no
  **Moved** date is current; a person has one current address, and the
  addresses they have left are historic by their dates.
- **Addresses are stored made regular.** The street, unit and city are saved
  in title case with their spaces tidied, the state as its two-letter code,
  and a nine-digit zip as ZIP+4 with its hyphen — so `45 east 400 south` and
  `841111234` are saved as **45 East 400 South** and **84111-1234**,
  however they were typed.

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
   Maria Rodriguez among them with the one address this walkthrough reads;
   run the first and then the second against your database. The end-to-end
   suite seeds both itself.
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

This walkthrough **changes Maria Rodriguez's record**: it dates her address
and gives her a second one, and it opens a report on her. The *Record
demographics* walkthrough reads her record as the fixture seeds it, so **to
perform this walkthrough again, or that one after it, against the same
database, reseed it first**: empty the person tables and their reports, then
run `conditions.sql` and `persons.sql` again. The end-to-end suite reseeds
exactly that way before this walkthrough and again after it.

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
2. In the box labelled **Last name**, type `Rodriguez`; in the box labelled
   **First name**, type `Maria`; then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row's **Last, First** reads **Rodriguez, Maria**. Press **Create CMR**
   on that row.
4. Open the **Disease** list and choose **Giardiasis**; then open the
   **Investigating agency** list and choose **Utah State**.
5. Press **Save & Continue**. The report now exists, and the screen is its
   own form: in the header bar, **Patient name** reads **Rodriguez, Maria**,
   and the **Demographic** tab is open. Note the address in your browser's
   address bar: it ends in `/event/edit/` and a number, and step 10 comes back
   to it.
6. Read the **Addresses** section. It has one address, her current one:
   **Street** reads **1234 Main Street** and **Zip code** reads **84101**, and
   its **Earliest known** and **Moved** are both empty.
7. Press **Edit** at the top of the report, which opens it for editing. In
   that address, type `2024-03-01` in **Earliest known**: the day she moved
   in.
8. Press **Add address**. A second, empty address appears under the first.
   In it — the address she lived at before — type `45 east 400 south` in
   **Street** and `salt lake city` in **City**, both in lower case; open its
   **State** list and choose **Utah**; type `841111234` in **Zip code**, all
   nine digits together; type `2019-06-01` in **Earliest known** and
   `2024-03-01` in **Moved**.
9. Press **Save & Continue** at the bottom of the tab. A green message
   appears at the top of the tab, reading **The demographics have been
   saved.**
10. Go back to the report's address — the one you noted in step 5 — by
    entering it in the address bar again, so that the screen is read afresh
    from the database. The **Addresses** section has two addresses. The
    first, her current one, reads **1234 Main Street**, **Earliest known**
    **2024-03-01** and an empty **Moved**. The second, her earlier one, reads
    **Street** **45 East 400 South**, **City** **Salt Lake City**, **Zip
    code** **84111-1234**, **Earliest known** **2019-06-01** and **Moved**
    **2024-03-01**: historic by its dates, and saved made regular.
11. In the application bar, press **People**. Type `Rodriguez` in **Last
    name** and `Maria` in **First name**, press **Search**, and press
    **Rodriguez, Maria**, the name on the one row.
12. Her own screen opens. Its **Addresses** table has two rows, in the order
    they were recorded: **1234 Main Street**, **Apt 5B**, **Salt Lake City**,
    **UT**, **84101**, **Salt Lake**, **—**; then **45 East 400 South**, **—**,
    **Salt Lake City**, **UT**, **84111-1234**, **—**, **—** — the county
    shown by its name, and the dash being what the screen shows where nothing
    was given, as in the **Planning region** column, which only a Connecticut
    address fills.

## What you should see

Maria Rodriguez's addresses, recorded as a history:

- A report on **Rodriguez, Maria** for **Giardiasis**, its **Demographic**
  tab holding the one address on file.
- That address dated from **2024-03-01**, and an earlier one added that she
  lived at from **2019-06-01** until she moved on **2024-03-01**, typed in
  lower case with a nine-digit zip.
- After the save, read afresh from the database: the current address with no
  **Moved** date, and the earlier one as **45 East 400 South**, **Salt Lake
  City**, **84111-1234**, with both its dates.
- Her own screen listing both addresses, the earlier one with its state as
  **UT** and its zip as **84111-1234**.

You now know how to record where a person lives and where they lived before.
If what you saw differs from what is written above, the walkthrough has
failed: say so rather than adjusting what you did, because the promise this
document makes is that it is performable exactly as written.
