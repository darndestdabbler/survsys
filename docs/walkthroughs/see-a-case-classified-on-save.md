# See a case classified on save

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SeeACaseClassifiedOnSaveTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I record a positive laboratory result on a chlamydia
> case that has no status, and see the case confirmed as soon as I save it,
> with what the classification found shown beside the save — so that I know
> the case's status without working it out myself.

Classifying a case on save is inferred from the legacy EpiTrax, read-only here
and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → classification rules | `../nedss/docs/business-logic/CLASSIFICATION_RULES.md`, *3.1 Chlamydia* | A chlamydia case with a positive *Chlamydia trachomatis* laboratory result is confirmed. |
| EpiTrax → validation orchestrator | `../nedss/docs/business-logic/CLASSIFICATION_RULES.md`, *2. Validation Orchestrator* | The validation and classification rules run when a case is saved. |

Two differences from the legacy are deliberate, and this walkthrough describes
what Survsys shows rather than what EpiTrax showed:

- **The findings are shown beside the save that made them.** What the rules
  found on a save is shown on the tab you saved from, as well as written to
  the case's notes.
- **The header follows the save.** The report's header reads the case status
  the save left, without a reload of the page.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this walkthrough reports on, **Nguyen, An**, is one the people seed
   inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`). No seed inserts
   the condition, **Chlamydia trachomatis infection**: add it, active, on the
   **Conditions** screen, then, on the **Code lists** screen, open the test
   type **Culture** and tick the condition under its **Conditions**, so a
   culture may be recorded for it.
2. **The `Survsys.Api` tier running.** On a developer machine: `dotnet run
   --project Survsys.Api --urls http://localhost:5100`.
3. **The `Survsys.Web` tier running**, pointed at that API tier. On a
   developer machine: `dotnet run --project Survsys.Web --urls
   http://localhost:5000`, where `Survsys.Web/appsettings.Development.json`
   already points `Survsys:ApiBaseAddress` at `http://localhost:5100`. **The
   application tier's address is the one you open**, and on a developer
   machine that is <http://localhost:5000>.
4. **Signed in, as a user whose role holds every privilege.** Survsys asks
   everyone to sign in, and sends you to its **Sign in** page until you do. On
   a fresh database the only user is the administrator the API tier creates on
   its first start from `Survsys__Administrator__UserName` and
   `Survsys__Administrator__Password`, set where `Survsys.Api` runs. Open the
   application tier's address, sign in, then begin at step 1.

**To watch this walkthrough performed instead of performing it yourself**, run

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That suite starts both tiers on ports it finds free, brings the database to
migration level and seeds it, adds the chlamydia condition with Culture
approved for it, performs the steps below in a real browser, and seeds the
database again afterwards. It records video of every test it runs, and writes
the recordings to `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as
`.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the coloured application
   bar across the top of the window, press **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Nguyen`, then press **Search**. The one row reads
   **Nguyen, An**. Press its **Create CMR** button.
3. In the list labelled **Disease**, choose **Chlamydia trachomatis
   infection**. In the list labelled **Investigating agency**, choose **Salt
   Lake County**. Press **Save & Continue**.
4. The report's own form opens. Its header bar reads **Chlamydia trachomatis
   infection** for the disease and **Unknown** for the **Case status**: the
   case has no status yet.
5. Press **Edit** at the top of the report, then the **Laboratory** tab.
   Press **Add test**, and in the test's list labelled **Test type** choose
   **Culture**. Press **Add result** under it, and in the result's list
   labelled **Organism** choose **Chlamydia trachomatis**, and in its list
   labelled **Result code** choose **Positive / Reactive**.
6. Press **Record specimen**. Without reloading the page:
   - a green message reads **The specimen has been recorded.**;
   - the header bar's **Case status** now reads **C**, the code for
     Confirmed;
   - beside the save, a blue box reads **Chlamydia: a positive Chlamydia
     trachomatis lab confirms the case.**

## What you should see

One case classified on save:

- Before the save, a chlamydia case with no status.
- On recording a positive *Chlamydia trachomatis* culture, the case
  confirmed in the report's header, without a reload.
- Beside the save, the finding that confirmed it.

You now know how to see a case classified on save. If what you saw differs
from what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
