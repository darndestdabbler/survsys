# Open a case from an eICR

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/OpenACaseFromAnEicrTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As a surveillance reviewer, when a provider's eICR arrives for a patient
> who is not on file, with a reportability response finding a condition we
> track reportable, I find that Survsys has opened a case for that patient
> and condition, and I open it from the eICR — so that a reportable eICR
> becomes a case without anyone keying it in.

It is inferred from the legacy EMSA handling of clinical documents and from
Connecticut's eCR requirements, which Survsys's phase 13 was specified from:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EMSA → clinical documents | `../planning/ecr-requirements.md`, *Where survsys stands* | Legacy EMSA took an eICR as a clinical-document message, let its diagnosis drive the case rules, and wrote its clinical content onto the case. |
| eCR requirements | `../planning/ecr-requirements.md`, *What survsys must do, by session*, items 7 and 8 | The eICR's patient matched by the person matching, a new case opened where there is none, and the eICR's demographics, encounter, problems, medications, pregnancy, travel and occupation written to it. |
| CT DPH eCR | `../nedss/docs/business-logic/CT_SITE_REQUIREMENTS.md`, *2.5 Condition-to-Product Mapping* and *2.6 eICR → MIF Transformation* | A reportable condition is mapped to the disease it is reported as, and the case's content is sourced from the eICR. |

Two things this walkthrough describes as Survsys does them:

- **Survsys opens the case, not the reviewer.** An eICR is written to a case
  once an RR finds one of its conditions reportable and that condition's code
  is mapped. Its patient matches no one on file, so a person is added for
  them and a case opened on the mapped condition, under the agency their zip
  code belongs to. Where the match is not confident the eICR is held for a
  reviewer instead, who settles it on the eICR's screen with **Use this
  person** or **Create new person**; this walkthrough's patient is not that
  case.
- **The eICR's screen links to the case.** Under **Cases** it shows what
  became of it: an **Open case** link for each case it was written to.

Linking an eICR to a case that is already open is its own walkthrough, *Link
an eICR to an existing case*.

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
   `Survsys.E2ETests/Fixtures/ecr-intake.sql` against it. The third puts the
   zip code **84101** under **Salt Lake County**; the last maps the SNOMED code
   for Hepatitis A, `40468003`, to the catalogue's **Hepatitis A**. None of
   them puts **Tomas Lindqvist** on file. The end-to-end suite seeds them
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

This walkthrough **takes in two documents and opens one case**, a Hepatitis A
case on a new person, Tomas Lindqvist. Performed a second time over the same
database, the documents' ids are already on file and nothing more is opened;
start again from a freshly seeded database to perform it again as written.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers a second time over a database of their own with
the intakes turned on, seeds them, and performs the steps below in a real
browser. It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Copy the file `Survsys.E2ETests/Fixtures/Cda/ecr-open-eicr.xml` into the
   eCR drop directory, wait for it to be gone, then copy
   `Survsys.E2ETests/Fixtures/Cda/ecr-open-rr.xml` in after it. The first is
   an eICR for **Tomas Lindqvist**, born 1979-02-14, of 305 Temple Street,
   Salt Lake City, 84101; the second the RR that responds to it, finding
   **Hepatitis A** reportable. Within a few seconds of each copy the file is
   gone from the drop directory: the intake has read and stored it and moved
   it into the directory's `.processed` folder.
2. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Intake**, choose **eCR**. The screen is headed
   **eCR documents**, and the row whose **Document** reads
   **2.16.840.1.113883.9.9.9.9.9^e2e-open-eicr** reads **eICR** under
   **Kind**.
3. Press **2.16.840.1.113883.9.9.9.9.9^e2e-open-eicr**. The heading reads
   **eICR 2.16.840.1.113883.9.9.9.9.9^e2e-open-eicr**. Under **Reportability
   response** the one condition reads **Hepatitis A (40468003)** under
   **Condition**, **Hepatitis A** under **Maps to** and **Reportable** under
   **Routing**.
4. Under **Cases** is one link, reading **Open case**: the case the eICR was
   written to. If the section reads **No case has been written from this
   eICR.** instead, the intake is still writing it: reload the page until the
   link is there.
5. Press **Open case**. The browser goes to the new report's form, headed
   **Case morbidity report**. In the header bar, **Patient name** reads
   **Lindqvist, Tomas**, **Disease** reads **Hepatitis A**, **Investigating
   agency** reads **Salt Lake County**, and **Record #** is a new record
   number — nine digits beginning with the current year.

## What you should see

A reportable eICR turned into a case:

- The eICR on the **eCR documents** queue, its RR's **Hepatitis A** mapped
  and routed **Reportable**.
- One **Open case** link under the eICR's **Cases**.
- A new case morbidity report for **Lindqvist, Tomas**, on **Hepatitis A**,
  under **Salt Lake County**.

You now know how a reportable eICR becomes a case. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
