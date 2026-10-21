# Maintain a LOINC mapping

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/MaintainALoincMappingTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance administrator, I map a laboratory test's LOINC code to the
> condition it reports, map a sending lab's own code for that test to the
> LOINC, correct the lab's mapping, and take both away again, so that the
> intake translates each lab's results to the right condition without a
> release.

This is the vocabulary the legacy message pipeline translates through, and it
is inferred from EMSA's vocabulary maintenance:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → the vocabulary | `../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md` | A three-tier vocabulary: a master LOINC carries the condition (and organism) a test reports, and each sending lab's child LOINC — the lab's own local code — resolves to a master. |
| EMSA → the message pipeline | `../nedss/docs/business-logic/EMSA_MESSAGE_PIPELINE.md`, *Vocabulary Resolution During Hydration* | Each result is translated through the lab's child code, then the master, to its condition before any rule reads it. |

One difference from the legacy screens is deliberate, and this walkthrough
describes what Survsys shows rather than what EMSA showed: **the vocabulary is
one screen with a tab per table.** The **Vocabulary** screen holds **Master
LOINC**, **Child LOINC**, **Organisms**, **Specimens** and **Results**, each
with its form above its table; a master's **Lab codes** column lists the
lab codes that resolve to it.

## Before you start

You need all five of these. The second is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **A sending lab and the vocabulary this walkthrough starts from, in that
   database.** Run `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql` and then
   `Survsys.E2ETests/Fixtures/elr-intake.sql` against it. The last puts the
   sending lab **ARUP Laboratories**, short name **ARUP_LAB**, on file, and
   three master LOINC codes — **22314-9**, **5061-8** and **6463-4**. The
   end-to-end suite seeds them itself, into the database it runs the intake
   walkthroughs against; a database you brought up by hand does not have them
   until you run them. This walkthrough needs no intake turned on.
3. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
4. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
5. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **adds, changes and then deletes** a master LOINC and a child
LOINC, so the vocabulary is as it was before you began. Perform it as many
times as you like.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers, seeds them, and performs the steps below in a
real browser. It records video of every test it runs, and writes the
recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as
`.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **Vocabulary**.
2. The browser tab reads **Vocabulary — Survsys**, and the heading reads
   **Vocabulary**. Below it are the tabs **Master LOINC**, **Child LOINC**,
   **Organisms**, **Specimens** and **Results**, with **Master LOINC** open.
   Its table lists the three master LOINC codes on file, **22314-9**,
   **5061-8** and **6463-4**, and its form is headed **New master LOINC**.
3. In the form, type `88017-0` in the box labelled **LOINC** and `Listeria
   monocytogenes DNA [Presence] in Specimen` in the box labelled **Concept
   name**. Open the **Condition** list and choose **Listeriosis**, which the
   list shows with its number after it, as **Listeriosis (#**_n_**)**. Press
   **Save**.
4. The table now has a row whose **LOINC** reads **88017-0**, whose
   **Concept** reads **Listeria monocytogenes DNA [Presence] in Specimen**
   and whose **Condition** reads **Listeriosis**. Its **Lab codes** is empty:
   no lab's code resolves to it yet.
5. Press the **Child LOINC** tab. Its form is headed **New child LOINC**. Open
   the **Sending lab** list and choose **ARUP Laboratories (ARUP_LAB)**. Type
   `LISPCR` in the box labelled **Lab's code**. Open the **Master LOINC** list
   and choose the entry beginning **88017-0**. Type `Listeria PCR` in the box
   labelled **Lab's concept name**. Press **Save**.
6. The table has a row reading **ARUP_LAB** for **Lab**, **LISPCR** for
   **Lab's code**, **88017-0** for **Master LOINC**, **Listeria PCR** for
   **Concept** and **No** for **Archived**.
7. Press the **Master LOINC** tab. The **88017-0** row's **Lab codes** now
   reads **ARUP_LAB: LISPCR**: the lab's code resolves to the master, and
   through it to Listeriosis.
8. Press the **Child LOINC** tab, and press **Edit** on the **LISPCR** row.
   The form is headed **Edit child LOINC**, and **Lab's code** reads
   **LISPCR**. Replace what **Lab's concept name** holds with `Listeria
   monocytogenes PCR`, and press **Save**. The form is headed **New child
   LOINC** again, and the **LISPCR** row's **Concept** reads **Listeria
   monocytogenes PCR**.
9. Press **Delete** on the **LISPCR** row. The row is gone from the table.
10. Press the **Master LOINC** tab, and press **Delete** on the **88017-0**
    row. The row is gone, and the table lists the three master LOINC codes it
    began with, **22314-9**, **5061-8** and **6463-4**.

## What you should see

A LOINC mapping made, corrected and taken away on one screen:

- A master LOINC, **88017-0**, mapped to **Listeriosis**.
- ARUP_LAB's own code, **LISPCR**, mapped to that master, and shown against
  it in the master's **Lab codes**.
- The lab's mapping corrected in place by **Edit** and **Save**.
- Both deleted, the child first — a master a lab's code still names is not
  deleted — leaving the vocabulary as it began.

You now know how to keep the vocabulary the intake translates through. If what
you saw differs from what is written above, the walkthrough has failed: say so
rather than adjusting what you did, because the promise this document makes is
that it is performable exactly as written.
