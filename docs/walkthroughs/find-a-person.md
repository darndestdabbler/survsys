# Find a person

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/FindAPersonTests.cs`, and a step you cannot perform is a
defect in the application or in this document — never something to work
around.

## User story

> As an investigator, I look up whether a person is already on file before I
> do any case work on them — so that a report is opened on the person the
> system already knows, and not on a second copy of them.

This is the first thing an investigator does in the legacy system before
opening a case morbidity report, and it is inferred from one legacy screen,
read-only here and never modified:

| Legacy screen | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → **People** (`/nedss/people`) | `../nedss/docs/functional/epitrax-core-workflows.md`, section *People*, and the screenshot `../nedss/docs/screenshots/session-007/05-people-list.png` | The person search form — last name, first name, middle name, date of birth, phone and birth sex, with **Search** and **Clear** — and under it a table of the people found, or *No People Found.* The legacy documentation calls its purpose "standalone person management — find/view/edit people independent of CMR context", and notes that it is the same search form the New CMR screen opens with. |

Three differences from the legacy screen are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- Survsys shows **one page of results and no pager**. A search that matches
  more than a page says so under the count, and narrowing the search is what
  you do about it. None of the searches below matches more than a page.
- Survsys shows an **em dash** (`—`) where a person has no date of birth or
  no birth sex on file, so that an absent value reads as absent rather than
  as an empty cell you might take for a rendering fault.
- **A person's name opens their own screen.** On each row the name is a
  link to the person's screen, with their demographic record and their
  cases. This walkthrough reads the results and follows no link.

The legacy form's free-text *Person name* box, its *Phone*, *Morbidity* and
*Contact* columns and its *Create Person and CMR* button are not part of this
walkthrough. The last of those belongs to the
walkthrough that creates a report; the rest Survsys does not have yet.

A person's **ID** is the number the database assigned when the person was
put on file, so it differs from one installation to another. This walkthrough
therefore never tells you what an ID will read, only that the column is there.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The seven people this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/persons.sql` is the seed — one real
   person copied from the legacy database snapshot and six written for the
   suite — and running it against your database inserts them, after
   `Survsys.IntegrationTests/Fixtures/conditions.sql` has inserted the seven
   conditions the other walkthroughs name. The end-to-end suite seeds both
   itself; a database you brought up by hand does not have them until you
   run both.
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
   bar across the top of the window, press **People**. (The people screen is
   also at `/people`, so <http://localhost:5000/people> opens the same
   screen.)
2. The browser tab reads **People — Survsys**, and below the application bar
   is the heading **People**. The heading is how you know you are on the
   right screen.
3. Read the line of small text directly under the heading. It reads
   **Search for a person by name, date of birth, telephone or birth sex.
   Leave every box empty to list everyone on file.**
4. In the box labelled **Last name**, type `Rodriguez`, then press
   **Search**. A search matches a last name that begins with what you typed.
5. Read the line of small text that appears above the table. It reads
   **2 person(s) found, showing 2.** — the first number is how many people
   matched, the second how many of them this page is showing you.
6. Read the six column headings of the table, left to right: **ID**,
   **Last, First**, **Birth date**, **Birth sex**, **Score** and **Why**.
   Then read down the **Last, First** column. There are two rows, in
   alphabetical order by last name and then first name:

   | Last, First | Birth date | Birth sex | Score | Why |
   |---|---|---|---|---|
   | Rodriguez, Carlos | 1960-01-20 | M | 30 (None) | Last name matches |
   | Rodriguez, Maria | 1985-06-15 | F | 30 (None) | Last name matches |

   A search with criteria scores each row against them: a last name alone
   agrees on one field, which is worth 30 of 100 and too little to call the
   row the same person. Each row's **ID** is the number this installation
   gave the person; it is there, and it is not something this document can
   tell you.
7. Press **Clear**. The **Last name** box is empty again, and the count line
   and the table are gone.
8. With every box empty, press **Search**. The count line reads
   **7 person(s) found, showing 7.** — every person on file — and the table
   has seven rows, in alphabetical order by last name and then first name:

   | Last, First | Birth date | Birth sex |
   |---|---|---|
   | Abbott, Jane | — | F |
   | Doe | — | UNK |
   | Nguyen, An | 1992-02-29 | M |
   | Okafor, Chidi | 1978-11-03 | M |
   | Rodriguez, Carlos | 1960-01-20 | M |
   | Rodriguez, Maria | 1985-06-15 | F |
   | Zimmerman, Paul | 1950-07-04 | M |

   Two rows show what the screen does with what nobody has said. **Doe** has
   no first name on file, so the name column reads the last name alone; no
   date of birth, so **Birth date** is an em dash, **—**; and a birth sex of
   **UNK**, the legacy code for *unknown*. **Abbott, Jane** is an infant
   reported with an age but no birthday, so her **Birth date** is an em dash
   too.
9. In the box labelled **Last name**, type `Nobody`, then press **Search**.
   The table is replaced by the message **No people found.**, because no
   person on file has a last name beginning with that.

## What you should see

The people on file, found by searching for them, on one screen:

- The heading **People**, under an application bar reading **Survsys**, with
  the search form under it.
- Searching for the last name **Rodriguez** finds **2 person(s) found,
  showing 2.**: **Rodriguez, Carlos** and **Rodriguez, Maria**, in that
  order, with their dates of birth and birth sexes as printed above, each
  scored **30 (None)** because **Last name matches**.
- **Clear** empties the form and takes the results away.
- Searching with every box empty finds **7 person(s) found, showing 7.**, in
  alphabetical order by last name and then first name, with an em dash for
  a date of birth nobody has given and **UNK** for a birth sex nobody knows.
- Searching for the last name **Nobody** reads **No people found.**

You now know how to find out whether a person is on file before you do any
case work on them. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because
the promise this document makes is that it is performable exactly as written.
