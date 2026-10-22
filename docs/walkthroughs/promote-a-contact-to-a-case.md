# Promote a contact to a case

Perform it exactly as written: every step below is performed, in this order,
by `Survsys.E2ETests/PromoteAContactToACaseTests.cs`, and a step you cannot
perform is a defect in the application or in this document — never something
to work around.

## User story

> As an investigator, I add a person exposed alongside my patient as a
> contact, which opens a contact event of their own, and when the contact
> turns out to have the disease too I follow the link to their contact event
> and promote it to a morbidity report, so that the contact becomes a case in
> their own right without anything about them being typed again.

Contact events and their promotion are inferred from the legacy EpiTrax,
read-only here and never modified:

| Legacy source | Where it is documented | What it shows |
|---|---|---|
| EpiTrax → the CMR edit form → **Contacts** tab | `../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md`, *Contacts Tab* — *Layout* and *Key Discovery* | A contact is found by a person search and linked to the case; contacts are full person records, and a contact can have a case of their own — contact to case promotion. |
| EpiTrax → **Workflow Options** | `../nedss/docs/functional/epitrax-cmr-deep-editing.md`, *5. Workflow Transitions*, *All workflow states (from database)* | The workflow state `promote_to_morbidity`: promoted from contact to morbidity. |
| EpiTrax → the CMR header bar | `../nedss/docs/functional/epitrax-cmr-workflow.md`, *CMR Edit Form Structure*, *Header Bar* | The report's **Event type**, Morbidity or Contact, shown on every tab. |

Three differences from the legacy system are deliberate, and this walkthrough
describes what Survsys shows rather than what EpiTrax showed:

- **A contact is added to an exposure.** The **Contacts** tab lists the case's
  exposures, and each exposure's **Add contact** opens a person search; a
  person found is added with **Add as contact**, which opens their contact
  event under the case's disease and investigating agency.
- **The contact links to their contact event.** In the exposure's contacts,
  the person is followed by a **(contact event)** link to the contact's own
  report.
- **Promotion is a button on the contact event.** A report whose **Event
  type** is **Contact** carries **Promote to morbidity report** above its
  tabs. Promotion keeps the record number, the person, the disease and the
  agency, and makes the report a morbidity report.

## Before you start

You need all four of these.

1. **A reachable SQL Server**, holding a Survsys database at this build's
   migration level. `Survsys.Api` is the only tier permitted to touch the
   database, and it is what brings an empty one up to level: start it with
   `Survsys__ApplyMigrations=true` and it migrates and then serves. The two
   people this walkthrough names, **Okafor, Chidi** and **Zimmerman, Paul**,
   are among the seven the people seed inserts
   (`Survsys.IntegrationTests/Fixtures/persons.sql`), and the condition,
   **Hepatitis A**, is one of the seven the conditions seed inserts
   (`Survsys.IntegrationTests/Fixtures/conditions.sql`); run both against your
   database if you brought it up by hand.
2. **The `Survsys.Api` tier running.** On a developer machine:
   `dotnet run --project Survsys.Api --urls http://localhost:5100`.
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

This walkthrough **opens a case, an exposure on it and a contact event, and
promotes the contact event**. Perform it twice and you have two of each. The
end-to-end suite empties the cases and the exposures on every run, so a run
always begins with none.

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
2. In the box labelled **Last name**, type `Okafor`, then press **Search**.
3. The line above the table reads **1 person(s) found, showing 1.**, and the
   one row reads **Okafor, Chidi**. Press **Create CMR** on that row.
4. Open the **Disease** list and choose **Hepatitis A**. Open the
   **Investigating agency** list and choose **Summit County**, then press
   **Save & Continue**.
5. The report's own form opens. In the header bar, **Patient name** reads
   **Okafor, Chidi**, **Event type** reads **Morbidity**, **Disease** reads
   **Hepatitis A** and **Investigating agency** reads **Summit County**. Note
   the record number.
6. Press **Edit** at the top of the report, which opens it for editing, then
   press the **Contacts** tab. Its **Exposures** table reads **No exposures
   recorded yet.**
7. In the entry form under it, type `Family dinner` in the box labelled
   **Name**, enter `2026-09-20` in **Exposure start** and `2026-09-20` in
   **Exposure end**, and press **Record exposure**. A green message reads
   **The exposure has been recorded.**, and the **Exposures** table has one
   row, **Family dinner**, whose **Contacts** cell is a dash.
8. Press **Add contact** on that row. A panel headed **Add a contact to
   Family dinner** opens, with a **Contact type** list and a person search
   under it. Open the **Contact type** list and choose **Household**.
9. In the panel's box labelled **Last name**, type `Zimmerman`, then press
   **Search**. The one row reads **Zimmerman, Paul**; note the number in its
   **ID** column. Press **Add as contact** on that row.
10. A green message reads **The contact has been added as a contact event.**,
    the panel closes, and the **Family dinner** row's **Contacts** cell holds
    one contact: its **Person** reads the number you noted followed by the
    link **(contact event)**, and its **Type** reads **HOUSEHOLD**.
11. Press **(contact event)**. The address changes to the contact's own
    report, and the screen shows it: in the header bar, **Patient name**
    reads **Zimmerman, Paul**, **Event type** reads **Contact**, **Disease**
    reads **Hepatitis A** and **Investigating agency** reads **Summit
    County**, and the record number is not the one you noted in step 5. Note
    this report's address and its record number.
12. Press **Edit** at the top of the report. Above its tabs is the button
    **Promote to morbidity report**. Press it.
13. A green message reads **The contact event is now a morbidity report.**,
    the header bar's **Event type** reads **Morbidity**, and the record
    number, the patient, the disease and the agency are as they were in step
    11.
14. Go back to the contact's report address — the one you noted in step 11 —
    by entering it in the address bar again, so that the screen is read
    afresh from the database. **Patient name** reads **Zimmerman, Paul** and
    **Event type** reads **Morbidity**, with the record number you noted in
    step 11, and there is no **Promote to morbidity report** button. That is
    how you know the promotion reached the database.

## What you should see

A contact added to a case and promoted to a case of their own:

- A case on **Okafor, Chidi** for **Hepatitis A** under **Summit County**,
  with the exposure **Family dinner**.
- **Zimmerman, Paul** added to that exposure as a **Household** contact, with
  a **(contact event)** link to a report of their own, of event type
  **Contact**, for the same disease under the same agency.
- That contact event, promoted with **Promote to morbidity report**, read back
  from the database as a **Morbidity** report with the same record number.

You now know how to promote a contact to a case. If what you saw differs from
what is written above, the walkthrough has failed: say so rather than
adjusting what you did, because the promise this document makes is that it is
performable exactly as written.
