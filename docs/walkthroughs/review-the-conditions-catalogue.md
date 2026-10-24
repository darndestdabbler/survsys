# Review the conditions catalogue

The first walkthrough. Perform it exactly as written: every step below is
performed, in this order, by
`Survsys.E2ETests/ReviewTheConditionsCatalogueTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something to
work around.

## User story

> As a surveillance administrator, I open the conditions catalogue to see which
> conditions may be reported, and which of them carry a CDC code — so that I
> know what the system will let an investigator file a case against, and what
> the state would notify the CDC about.

This is the first thing an administrator does in the legacy system before any
case work begins, and it is inferred from two legacy screens, both read-only
here and never modified:

| Legacy screen | Where it is documented | What it shows |
|---|---|---|
| EpiTrax administration → **Conditions** | `../nedss/docs/screenshots/session-007/13-admin-conditions.png` | The catalogue of conditions by name, each with a code, a CDC code, and an active flag. The CDC code column is blank for a condition with no code, and reads *not notifiable* for one the state does not notify on. |
| NMI → **Conditions** (`/conditions`, `v_conditions.php`) | `../nedss/docs/functional/nmi-ui-overview.md` | The same catalogue from the notification side: the disease-to-guide mapping, which is what decides whether a condition reaches the CDC at all. |

Two differences from the legacy screens are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- Survsys shows **Active** as `Yes` or `No`, where EpiTrax showed `true` or
  `false`.
- Survsys shows an **em dash** (`—`) where a condition has no CDC code, so that
  an absent code reads as absent rather than as an empty cell you might take for
  a rendering fault.

The legacy screen's name, code and CDC-code filter boxes, its sortable column
headings and its pager are not part of this walkthrough, and Survsys does not
have them. Its **Add Condition** and **Edit** buttons are there as Survsys's
**New condition** button and each condition's name, which opens the
condition to edit; this walkthrough reads the catalogue and changes nothing,
so it presses neither.

## Before you start

You need all five of these. The fourth is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. Started
   without that switch, it refuses to serve a database below its migration
   level, which is the behaviour you want everywhere but a fresh machine.
2. **The seven conditions this walkthrough names, in that database.**
   `Survsys.IntegrationTests/Fixtures/conditions.sql` is the seed — seven real
   rows from the legacy EpiTrax Flyway seed — and running it against your
   database inserts them. The end-to-end suite seeds them itself; a database
   you brought up by hand does not have them until you do this.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a developer
   machine: `dotnet run --project Survsys.Web --urls http://localhost:5000`,
   where `Survsys.Web/appsettings.Development.json` already points
   `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The application
   tier's address is the one you open**, and on a developer machine that is
   <http://localhost:5000>. On a deployed site it is the address of the
   Survsys web site, which your deployment supplies.

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

That suite starts both tiers on ports it finds free, seeds the conditions, and
performs the steps below in a real browser. It records video of every test it
runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a developer
   machine, <http://localhost:5000>. (The conditions screen is also at
   `/conditions`, so <http://localhost:5000/conditions> opens the same screen.)
2. Look at the top of the window. A coloured application bar runs across it,
   reading **Survsys**. The browser tab reads **Conditions — Survsys**.
3. Look below the application bar for the heading **Conditions**, with a
   **New condition** button under it. The heading is how you know you are on
   the right screen. If a spinning circle is under the button, the screen is
   still asking the API tier for the catalogue; wait for it to be replaced.
4. Read the line of small text under the **New condition** button. It reads
   **7 condition(s), showing 7.** — the first number is how many conditions the
   catalogue holds, the second how many of them this page is showing you.
5. Read the three column headings of the table below that line, left to right:
   **Name**, **CDC code**, and **Active**.
6. Read down the **Name** column. There are seven rows, in alphabetical order by
   name:

   | Name | CDC code | Active |
   |---|---|---|
   | Bartonellosis | — | Yes |
   | Giardiasis | 11570 | Yes |
   | Hepatitis A | 10110 | Yes |
   | Influenza, animal isolates | 11070 | No |
   | Listeriosis | 10640 | Yes |
   | Lyme disease | 11080 | Yes |
   | Malaria | 10130 | Yes |

7. Find the row whose **Name** is **Malaria**. Read across it: its **CDC code**
   is **10130**, and its **Active** is **Yes**. This is the ordinary case — a
   condition that may be reported, and that the state notifies the CDC about
   under code 10130.
8. Find the row whose **Name** is **Bartonellosis**. Its **CDC code** is an em
   dash, **—**, because Bartonellosis has no CDC code: it is a real condition
   that may be reported, and it is not nationally notifiable. Its **Active**
   is **Yes**.
9. Find the row whose **Name** is **Influenza, animal isolates**. Its **Active**
   is **No**: it is in the catalogue, and it has a **CDC code** of **11070**,
   but it is not currently available to report a new case against.

## What you should see

The conditions catalogue, on one screen, with nothing to click to reach it:

- The heading **Conditions**, under an application bar reading **Survsys**.
- **7 condition(s), showing 7.**
- A table of seven rows under the headings **Name**, **CDC code** and
  **Active**, in alphabetical order by name.
- **Malaria** carries CDC code **10130** and is **Active: Yes** — reportable and
  nationally notifiable.
- **Bartonellosis** shows **—** for its CDC code — reportable, not nationally
  notifiable.
- **Influenza, animal isolates** shows **Active: No** — in the catalogue, but
  not available for a new report.

You now know which conditions this installation will accept a case against, and
which of them would reach the CDC. If what you saw differs from what is written
above, the walkthrough has failed: say so rather than adjusting what you did,
because the promise this document makes is that it is performable exactly as
written.
