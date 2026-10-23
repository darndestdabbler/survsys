# Map a reportable condition

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/MapAReportableConditionTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance administrator, when a reportability response finds a
> condition reportable whose code Survsys does not know, I find it waiting
> for review on the eCR queue, map its code to the condition in our catalogue
> it is reported as, and see the response's condition reportable — so that
> the condition codes reportability responses carry are kept mapped to the
> diseases we track.

It is inferred from Connecticut's eCR requirements, which Survsys's phase 13
was specified from; legacy EMSA never read the reportability response, so
there is no legacy screen to infer it from:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| CT DPH eCR | `../nedss/docs/business-logic/CT_SITE_REQUIREMENTS.md`, *2.5 Condition-to-Product Mapping* | The RR's conditions are SNOMED-coded and mapped to the surveillance system's diseases; several codes may map to one disease, and some map to none. |
| eCR requirements | `../planning/ecr-requirements.md`, *What survsys must do, by session*, items 5 and 6 | RCTC and SNOMED condition codes mapped to the conditions catalogue on a screen, and a reportable condition whose code is unmapped sent to review. |

Two things this walkthrough describes as Survsys does them:

- **A mapping is read when the response is read.** The RR's routing is not
  fixed when it arrives: a code mapped afterwards changes how the RR already
  on file is routed, which is what step 7 shows.
- **The RR arrives alone.** Its eICR never arrives in this walkthrough, so
  the RR's screen says so where the eICR would be shown.

## Before you start

You need all six of these. The second is the one people forget.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level, brought up by `Survsys.Api` started with
   `Survsys__ApplyMigrations=true` as the other walkthroughs describe.
2. **The eCR intake's test feed turned on**, which a Survsys starts with off.
   Start `Survsys.Api` with `EcrIntake__TestDirectory` set to a directory of
   your choosing — the **eCR drop directory** below — with
   `EcrIntake__PollIntervalSeconds` set to a few seconds, and with
   `EcrIntake__ResponsibleAgencyIds__0` set to
   `2.16.840.1.113883.4.6^8000000001`, the id the fixture RR gives the Utah
   Department of Health as the condition's responsible agency.
3. **The reference data this walkthrough reads, in that database.** Run
   `Survsys.IntegrationTests/Fixtures/conditions.sql`,
   `Survsys.IntegrationTests/Fixtures/persons.sql`,
   `Survsys.E2ETests/Fixtures/elr-intake.sql` and then
   `Survsys.E2ETests/Fixtures/ecr-intake.sql` against it. The first puts
   **Bartonellosis** in the conditions catalogue; the last maps three other
   SNOMED codes, and none for Bartonellosis. The end-to-end suite seeds them
   itself, into a database of its own.
4. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`, with the
   settings in item 2.
5. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`. **The application tier's address is the one you
   open**, and on a developer machine that is <http://localhost:5000>.
6. **Signed in, as a user whose role holds every privilege**, the way the
   other walkthroughs describe, and then begin at step 1.

This walkthrough **takes in one RR and adds one mapping**, of the SNOMED code
`50694005` to Bartonellosis. Performed a second time over the same database,
the RR's id is already on file and its code already mapped; start again from a
freshly seeded database to perform it again as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intakes turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Copy the file `Survsys.E2ETests/Fixtures/Cda/ecr-map-rr.xml` into the eCR
   drop directory. It is an RR finding **Bartonellosis**, SNOMED code
   `50694005`, reportable to the Utah Department of Health. Within a few
   seconds the file is gone from the drop directory: the intake has read and
   stored it and moved it into the directory's `.processed` folder.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **eCR**. The screen is headed
   **eCR documents**. Tick **Needs review**. The row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.8^e2e-map-rr** reads **RR** under **Kind** and
   **1** under **To review**.
3. Press **2.16.840.1.113883.9.9.9.9.8^e2e-map-rr**. The heading reads **RR
   2.16.840.1.113883.9.9.9.9.8^e2e-map-rr**, and under **eICR** the screen
   reads **The eICR this response is for has not been received.** Under
   **Reportability response** the one condition reads **Bartonellosis
   (50694005)** under **Condition** and **Reportable (RRVS1)** under
   **Determination**, but a dash under **Maps to** and **Needs review** under
   **Routing**: the RR found it reportable, and Survsys does not know which of
   its conditions the code is.
4. In the application bar, press **eCR conditions**. The screen is headed
   **eCR condition mapping**. Under **New code**, type
   `2.16.840.1.113883.6.96` — SNOMED CT's code system — in **Code system
   OID**, `50694005` in **Code** and `Bartonellosis` in **Display name**. Open
   the **Condition (empty for unmapped)** list and choose **Bartonellosis**.
   Press **Save**.
5. The form empties, and the table below it has a row reading
   **2.16.840.1.113883.6.96** under **Code system**, **50694005** under
   **Code**, **Bartonellosis** under **Display name** and **Bartonellosis**
   under **Condition**.
6. In the application bar, press **eCR**. The row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.8^e2e-map-rr** now reads **0** under **To
   review**.
7. Press **2.16.840.1.113883.9.9.9.9.8^e2e-map-rr**. The one condition now
   reads **Bartonellosis** under **Maps to** and **Reportable** under
   **Routing**.

## What you should see

A reportable condition Survsys did not know, mapped:

- The RR on the **eCR documents** queue under **Needs review**, its
  Bartonellosis condition mapped to nothing and waiting for review.
- The code `50694005` mapped to **Bartonellosis** on the **eCR condition
  mapping** screen.
- The same RR afterwards with nothing to review, its condition mapped to
  **Bartonellosis** and routed **Reportable**.

You now know how to map a reportable condition's code to the condition it is
reported as. If what you saw differs from what is written above, the
walkthrough has failed: say so rather than adjusting what you did, because the
promise this document makes is that it is performable exactly as written.
