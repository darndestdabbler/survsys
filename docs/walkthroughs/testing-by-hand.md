# Testing by hand

Every walkthrough in this folder, in an order you can perform them in, against
a Survsys the kit in `tools/uat/` resets and starts for you. You do not need
each walkthrough's **Before you start**: the kit's `reset` puts the rows those
sections name into a database of its own, and its `start` runs both tiers with
every setting they name.

The kit seeds exactly what the end-to-end harness seeds — its clearing and its
fixtures are the ones the harness calls — so a database you walk through is the
one the walkthroughs' tests walk through.

## What you need

- **A reachable SQL Server.** By default the kit uses `survsys_uat` on this
  host's own server, with Windows sign-in. To use another, set
  `SURVSYS_UAT_SQLSERVER` to a whole connection string. The kit refuses
  `survsys_e2e` and `survsys_e2e_intake`, which belong to the end-to-end suite.
- **The administrator you sign in as**, in two environment variables:
  `Survsys__Administrator__UserName` and `Survsys__Administrator__Password`.
  The API tier creates that user on a fresh database. Choose the values
  yourself; they are this database's only.
- **For *Send a case to IBIS* only**: `IbisTransmission__Endpoint` set to a
  test IBIS, or a stand-in of your own that answers `200 OK`, before you start.
  The case is really sent, so never point it at the production one.

In PowerShell, for example:

```
$env:Survsys__Administrator__UserName = 'administrator'
$env:Survsys__Administrator__Password = 'a-password-of-your-own'
```

## The commands

Run them from the repository root.

```
dotnet run --project tools/uat/Survsys.Uat -- reset
dotnet run --project tools/uat/Survsys.Uat -- start
```

- **`reset`** drops the database and creates it again. It brings the database
  to this build's migration level, creates the administrator, and seeds what
  the main harness seeds: the conditions, the people, the staged messages and
  the notification guide.
- **`start`** runs `Survsys.Api` on port 5100 and `Survsys.Web` on port 5000
  over that database. It writes cases sent to CDC as files into an outbox
  folder, and leaves the intakes off. It prints the address to open,
  <http://localhost:5000/>, and the outbox folder. Press Enter or Ctrl+C to
  stop both tiers.
- **`reset --intake`** and **`start --intake`** do the same as the intake
  harness does. They seed the conditions and the people, then the ELR and eCR
  reference data, with no staged message. They turn on the ELR drop directory,
  the eCR test feed and the assignment pass, each polled every two seconds,
  and the responsible agency the fixture RRs name. `start --intake` also
  prints the ELR and eCR drop directories. When a walkthrough says to drop a
  file, copy it into one of these.

The two seeds cannot share one database. The assignment pass takes every
staged message that is waiting for review, which includes the ones the main
seed puts there for you to review and promote. The intake seed's people and
cases would also show up in the main walkthroughs. That is why the intake group
comes last, with a reset of its own.

Stop the tiers before you run `reset`. It drops the database out from under
any connection still open to it, so a tier left running would fail at its next
read.

## The order

Each walkthrough has an **Again** mark. **Yes** means it reads only what it
created itself, so you can perform it again over the same database. **No**
means a second run would not read as written, and you need a reset first. When
the mark and a walkthrough's own closing paragraph disagree, the paragraph is
right.

Start with `reset` and then `start`, sign in as the administrator, and work
down the list.

### Reference data and administration

| Walkthrough | Again |
|---|---|
| [Review the conditions catalogue](review-the-conditions-catalogue.md) | Yes |
| [Configure an agency](configure-an-agency.md) | Yes |
| [Add a facility](add-a-facility.md) | No: it reads its facility as the only one on file |
| [Record a treatment](record-a-treatment.md) | No: the list then shows two of the same name |
| [Configure a notification guide](configure-a-notification-guide.md) | No: its PHIN variable must not be there yet |

### Views, exports and reports

These come before any walkthrough that opens a case. *Run an ad-hoc report*
counts Lyme disease cases on a database that has none yet, and *Export a
dataset* expects its Bartonellosis report to be the only one.

| Walkthrough | Again |
|---|---|
| [Save a view](save-a-view.md) | No: the list then holds two views of the same name |
| [Export a dataset](export-a-dataset.md) | No: its filter then selects two reports |
| [Run an ad-hoc report](run-an-ad-hoc-report.md) | No: its counts are of a database with no Lyme disease case |

### People and cases

| Walkthrough | Again |
|---|---|
| [Find a person](find-a-person.md) | Yes |
| [Create a case morbidity report](create-a-case-morbidity-report.md) | Yes |
| [Assign and accept a case](assign-and-accept-a-case.md) | Yes |
| [Close an investigation](close-an-investigation.md) | Yes |
| [Route a case to another agency](route-a-case-to-another-agency.md) | Yes |
| [Find a case](find-a-case.md) | No: its search then finds two |
| [Review a case's audit history](review-a-cases-audit-history.md) | Yes |
| [See a case classified on save](see-a-case-classified-on-save.md) | Yes |
| [Record a case treatment](record-a-case-treatment.md) | No: the **Treatment** list then offers its name twice |

### Laboratory

| Walkthrough | Again |
|---|---|
| [Enter a laboratory result](enter-a-laboratory-result.md) | Yes |
| [Correct a laboratory result](correct-a-laboratory-result.md) | Yes |

### Contacts, exposures and outbreaks

| Walkthrough | Again |
|---|---|
| [Log an exposure event](log-an-exposure-event.md) | Yes |
| [Add a contact to a case](add-a-contact-to-a-case.md) | Yes |
| [Promote a contact to a case](promote-a-contact-to-a-case.md) | Yes |
| [Open an outbreak and link cases](open-an-outbreak-and-link-cases.md) | Yes |

### Notes, tasks, attachments and forms

| Walkthrough | Again |
|---|---|
| [Attach a document](attach-a-document.md) | Yes |
| [Remove an attachment](remove-an-attachment.md) | Yes |
| [Work a task list](work-a-task-list.md) | No: the task list then holds the first run's tasks too |
| [Complete a supplemental form](complete-a-supplemental-form.md) | No: a second report is given both forms |

### Staged messages

*Promote a message to a case* comes before *Resolve a duplicate*. Each one
promotes its own seeded message.

| Walkthrough | Again |
|---|---|
| [Review a staged message](review-a-staged-message.md) | Yes |
| [Promote a message to a case](promote-a-message-to-a-case.md) | No: its message is then already a case |
| [Resolve a duplicate](resolve-a-duplicate.md) | No: its message is then already a case |

### CDC and IBIS

**Process now** sends every case waiting, not only yours, so these walkthroughs
follow their own case by its record number. The outbox is the folder `start`
printed.

| Walkthrough | Again |
|---|---|
| [Queue a case for notification](queue-a-case-for-notification.md) | Yes |
| [Send a case to CDC](send-a-case-to-cdc.md) | Yes |
| [Inspect the export log](inspect-the-export-log.md) | Yes |
| [Review a generated message](review-a-generated-message.md) | Yes |
| [Retract a notification](retract-a-notification.md) | Yes |
| [Send a case to IBIS](send-a-case-to-ibis.md) | Yes, with `IbisTransmission__Endpoint` set before `start` |

### Sharing

This walkthrough signs you in as the user it adds. Sign out and sign back in as
the administrator before you go on.

| Walkthrough | Again |
|---|---|
| [See a case shared read-only](see-a-case-shared-read-only.md) | No: its role and user names are then taken |

### Walkthroughs that change the people on file

Each of these changes a seeded person, and the walkthroughs after it read that
person as seeded. **Stop the tiers, `reset`, and `start` again after each
one**, as the end-to-end suite reseeds after each.

| Walkthrough | Again |
|---|---|
| [Record demographics](record-demographics.md) | No: reset after it |
| [Record a person's address history](record-a-persons-address-history.md) | No: reset after it |
| [Find a person by a former name](find-a-person-by-a-former-name.md) | No: reset after it |
| [Merge duplicate persons](merge-duplicate-persons.md) | No: reset after it |

### The intake group

Stop the tiers, then run `reset --intake` and `start --intake`. Copy each
file a walkthrough drops into the drop directory `start --intake` printed: the
ELR drop for a lab's `.hl7` message, the eCR drop for an eICR or an RR. The
files themselves are under `Survsys.E2ETests/Fixtures/Hl7` and
`Survsys.E2ETests/Fixtures/Cda`. Each walkthrough here opens or changes a case
of its own, so the group can be performed in any order. Run `reset --intake`
again before you repeat one.

| Walkthrough | Again |
|---|---|
| [Maintain a LOINC mapping](maintain-a-loinc-mapping.md) | Yes |
| [Find a message assigned automatically](find-a-message-assigned-automatically.md) | No: its patient is then on file |
| [Update an existing case from a laboratory message](update-an-existing-case-from-a-laboratory-message.md) | No: its specimen is then on the case |
| [Release a graylisted message](release-a-graylisted-message.md) | No: its patient then has the case |
| [Review an eICR and its reportability response](review-an-eicr-and-its-reportability-response.md) | No: its documents' ids are then on file |
| [Map a reportable condition](map-a-reportable-condition.md) | No: its RR's id is then on file and its code mapped |
| [Open a case from an eICR](open-a-case-from-an-eicr.md) | No: its documents' ids are then on file |
| [Link an eICR to an existing case](link-an-eicr-to-an-existing-case.md) | No: its documents' ids are then on file |
| [Settle a held eICR](settle-a-held-eicr.md) | No: its documents' ids are then on file |

## When a step does not read as written

Then the walkthrough has failed, as each one says. Note which walkthrough it
was, the step, and what you saw instead. Do not adjust what you did to make it
read as written. If you performed the walkthrough out of the order above, or a
second time when its mark says **No**, reset and perform it again before you
report it.
