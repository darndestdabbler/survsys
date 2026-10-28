# Send a case to IBIS

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/SendACaseToIbisTests.cs`, and a step you cannot perform
is a defect in the application or in this document — never something to work
around.

## User story

> As an investigator, I send a case to IBIS from its report and read the date
> IBIS accepted it — so that the state's epidemiology reporting system counts
> the case.

Sending a case to IBIS is inferred from the legacy EpiTrax, read-only here and
never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → IBIS integration | `../nedss/docs/functional/phase-1.6-feature-matrix.md`, *12. Reporting & Export* | A case is sent to IBIS, the external epidemiology reporting system. |
| EpiTrax → case fields | `../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt`, the `person_condition` row | A case carries `sent_to_ibis` and `ibis_updated_at`: whether it was sent to IBIS, and the date IBIS last took it. |

Two differences from the legacy are deliberate, and this walkthrough describes
what Survsys shows rather than what EpiTrax showed:

- **A case is sent from its report, one at a time.** The report's **IBIS**
  panel sends the case when you press **Send to IBIS**, shown only to a user
  who may edit cases.
- **A case is recorded as sent only when IBIS accepts it.** If IBIS does not
  answer in time, or refuses the case, the panel says so and the case is not
  marked as sent.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The
   person this walkthrough reports on, **Okafor, Chidi**, is one the people
   seed inserts (`Survsys.IntegrationTests/Fixtures/persons.sql`), and the
   condition, **Malaria**, one the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`). Run both, in that
   order, against your database if you brought it up by hand.
2. **The `Survsys.Api` tier running, pointed at an IBIS.** On a developer
   machine: `dotnet run --project Survsys.Api --urls http://localhost:5100`,
   with `IbisTransmission__Endpoint` set to the address IBIS takes cases at.
   Point it at a test IBIS, or at a stand-in of your own that answers `200
   OK`, never at the production one: the case is really sent.
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

That suite starts both tiers on ports it finds free, with the API tier's IBIS
endpoint pointed at a stand-in the suite runs on this machine's loopback, which
accepts every case, so nothing leaves your machine. It brings the database to
migration level and seeds it, and performs the steps below in a real browser.
It records video of every test it runs, and writes the recordings to
`Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files.

## Steps

1. Open your browser and go to the application tier's address — on a
   developer machine, <http://localhost:5000>. In the side menu, under **Cases**, choose **New CMR**.
2. Under the heading **New case morbidity report**, in the box labelled
   **Last name**, type `Okafor`, then press **Search**.
3. The one row reads **Okafor, Chidi**. Press its **Create CMR** button.
4. In the list labelled **Disease**, choose **Malaria**. In the list labelled
   **Investigating agency**, choose **Davis County**. Press **Save &
   Continue**.
5. The report's own form opens, headed **Case morbidity report**. Its header
   bar reads **Okafor, Chidi** for the patient and **Malaria** for the
   disease.
6. Press **Edit** at the top of the report, then, in the panel headed
   **IBIS**, press **Send to IBIS**.
7. A green message in the panel reads **IBIS accepted this case on** followed
   by today's date, as `yyyy-mm-dd`.

## What you should see

One case sent to IBIS:

- On the report, the **IBIS** panel and its **Send to IBIS** button.
- Once you press it, the date IBIS accepted the case — today — and the case
  recorded as sent.

You now know how to send a case to IBIS. If what you saw differs from what is
written above, the walkthrough has failed: say so rather than adjusting what
you did, because the promise this document makes is that it is performable
exactly as written.
