# Survsys user guide

What each screen is for and how the work is done on it, as Survsys 1.0.0 has
it. Every section below describes a screen you can open today, and nothing in
this guide describes something you cannot.

The quickest way to learn a screen is not this guide but
[the walkthroughs](../walkthroughs/README.md), which you perform rather than
read; each section here links the ones that perform its screens.

| Section | Phase |
|---|---|
| [Signing in, and who may do what](#signing-in-and-who-may-do-what) | 4 |
| [Reference data — the conditions catalogue](#reference-data--the-conditions-catalogue) | 0 |
| [Persons and case records](#persons-and-case-records) | 1 |
| [Clinical, laboratory and workflow](#clinical-laboratory-and-workflow) | 2 |
| [Investigation, contacts, notes and tasks](#investigation-contacts-notes-and-tasks) | 3 |
| [Reference data administration and reporting](#reference-data-administration-and-reporting) | 4 |
| [Inbound laboratory messages](#inbound-laboratory-messages) | 5 |
| [CDC notification](#cdc-notification) | 6 |
| [Outbreaks, forms, treatments and audit](#outbreaks-forms-treatments-and-audit) | 7 |

**Two things hold on every screen.** A coded field — a gender, a race, a
place type, a test type, a disposition — is a plain box holding the number or
code the legacy EpiTrax database holds, not a list of names, because the
reference tables behind the legacy lists are not in Survsys yet. And a date is
typed as `yyyy-mm-dd`; anything else is refused with **Give** the field **as
yyyy-mm-dd, or leave it empty.** Lists that run long show 50 rows a page, with
**Previous** and **Next** under them.

---

## Signing in, and who may do what

Survsys asks everyone to sign in, and sends you to its **Sign in** page until
you do: type your **User name** and **Password** and press **Sign in**. A
session lasts eight hours; **Sign out**, at the right-hand end of the
application bar beside your name, ends it sooner.

What you may do is set by the **roles** you hold, and each role is a set of
five privileges:

| Privilege | What it lets you do |
|---|---|
| `view_cases` | Read people, cases and everything filed under them; the queues, outbreaks, staged messages, notifications, the export log, tasks and saved views. |
| `edit_cases` | Create and change cases and what is filed under them, outbreaks, supplemental forms and treatments. |
| `manage_reference_data` | Change the conditions, agencies, facilities and field visibility. |
| `manage_users` | Add and change users and roles. |
| `run_reports` | Run the ad-hoc reports. |

The application bar shows only the buttons your privileges open, in this
order: **Conditions**, **People**, **New CMR**, **Work queue**, **Agency
board**, **Outbreaks**, **Staged messages**, **Notifications**, **Export
log**, **Tasks**, **Saved views**, **Reports**, **Agencies**, **Facilities**,
**Field visibility**, **Forms**, **Treatments**, **Users** and **Roles**. The
API tier checks the privilege on every request, so a screen opened by its
address without the privilege shows what it was refused rather than the data.

**Users** (`/users`) lists every user by user name, display name, roles and
whether they **May sign in**. **New user** clears the form; enter a **User
name**, **Display name** and **Password**, tick their roles, and press **Save
user**. Editing a user leaves the password as it was unless you type a new
one. Untick **May sign in** to stop someone signing in without deleting them.
**Roles** (`/roles`) is the same for roles: a **Name** and a tick for each
privilege, saved with **Save role**.

On a new installation the only user is the administrator the API tier creates
on its first start, who holds the Administrator role and every privilege; the
[technical guide](../technical/README.md#deployment-and-configuration) says
how it is set.

## Reference data — the conditions catalogue

The conditions catalogue is the list of conditions this installation knows
about: what an investigator can file a case against, and which of those the
state notifies the CDC about.

Survsys opens on it. The screen shows every condition by **Name**, with its
**CDC code** and whether it is **Active**:

- **CDC code** is the code the CDC knows the condition by. A condition that has
  none — one that is reportable to the state but not nationally notifiable —
  shows an em dash (**—**) rather than an empty cell.
- **Active** is **Yes** for a condition that may be reported and **No** for one
  that is kept in the catalogue but is not currently available for a new report.

With `manage_reference_data`, **New condition** adds a condition and pressing a
condition's name opens it to edit: its **Name**, **CDC code**, **Active**, and
the lead-in text shown above its contacts, exposure places and treatments.
**Save** returns to the catalogue. The catalogue has no filters or sorting.

**Walk through it:**
[Review the conditions catalogue](../walkthroughs/review-the-conditions-catalogue.md).

---

## Persons and case records

**Finding a person.** **People** (`/people`) searches everyone on file by
**Last name**, **First name**, **Middle name**, **Date of birth**, **Phone**
and **Birth sex**; press **Search** (or Enter), and **Clear** to start again.
Every box empty lists everyone. The line above the table counts what was
found, and a search matching too many people asks you to narrow it. A person
is read here and on their reports; there is no person screen of its own.

**Opening a case morbidity report** is two steps, as it was in EpiTrax.
**New CMR** opens step 1, the same search: press **Create CMR** on the
person's row, or, when a search finds nobody, **Create person and CMR** to
create them from what you typed (it needs at least a last name). Step 2 takes
the date **First reported to public health**, the **Disease** and the
**Investigating agency**; **Save & Continue** creates the report and opens it.
Each report gets a nine-digit record number: the year, then its place among
that year's reports.

**The report form** (`/event/edit/` and the report's number) has:

- A **header** reading the patient's name, the record number, the event type,
  the disease, the investigating agency, the event date, the MMWR week and the
  case status.
- A **Case status** panel: type `C`, `P`, `S` or `NC` (confirmed, probable,
  suspected, not a case) and press **Save status**.
- A **CDC notification** panel, described under
  [CDC notification](#cdc-notification).
- Nine **tabs**: Demographic, Clinical, Laboratory, Investigation,
  Administrative, Forms, Vaccines, Contacts and Notes. Each tab saves on its
  own, with its own button and its own green message.

**The Demographic tab** records the person the report is about: their name;
parents or guardians and aliases (each **Add**ed as a row); date of birth, or
days old for a newborn, with their current age and age at onset worked out
for you; any number of addresses, telephone numbers and email addresses;
birth sex, current gender, primary language, ethnicity, country of birth; and
any number of race codes. **Save & Continue** saves the person whole and
answers **The demographics have been saved.** A last name is required. What
you save here is the person's, so every report on them reads it.

**Walk through it:** [Find a person](../walkthroughs/find-a-person.md),
[Create a case morbidity report](../walkthroughs/create-a-case-morbidity-report.md),
[Record demographics](../walkthroughs/record-demographics.md).

## Clinical, laboratory and workflow

**The Clinical tab** records the disease onset date, the date diagnosed,
whether treatment was given, and whether the patient died — the date of death
appearing once the answer is **Yes**. **Save & Continue** answers **The
clinical data has been saved.**

**The Laboratory tab** lists the report's specimens and records new ones. A
specimen carries its accession number, collection date, source, the lab,
provider, diagnostic and reporting facilities, and whether it was sent to the
state lab. Under it go any number of **tests** (type, status, LOINC code,
reference range, test date), under each test any number of **results**
(organism, value, result code, units, comment, and whether it is the primary
result), and any **antimicrobial susceptibility** results. **Record specimen**
writes the specimen with everything under it, and answers **The specimen has
been recorded.** A recorded specimen is listed and not edited.

**The workflow** takes a case through four states, always forward: **Assigned
to LHD**, **Accepted**, **Under investigation**, **Closed**.

- **Agency board** (`/queue/agency`): choose an **Agency** to see its whole
  queue, including the cases shared to it read-only. Its **Assign a report**
  panel assigns a report, by its id, to the agency that will investigate it;
  its **Share a report read-only** panel lets another agency see it.
- **Work queue** (`/queue`): choose an **Agency** to see the cases it
  investigates, each with the one move it can make next — **Accept**, then
  **Begin investigation**, then **Close**. A closed case offers none.

**Walk through it:**
[Enter a laboratory result](../walkthroughs/enter-a-laboratory-result.md),
[Assign and accept a case](../walkthroughs/assign-and-accept-a-case.md),
[Close an investigation](../walkthroughs/close-an-investigation.md).

## Investigation, contacts, notes and tasks

**The Investigation tab** records whether the patient is a food handler, a
health care worker, in group living or associated with a day care; where the
case was imported from, its transmission mode, its risk factors, and two
free boxes of other data. **Save & Continue** answers **The investigation data
has been saved.**

**The Contacts tab** records the exposure events a case is thought to have
come from, and the people exposed alongside the patient. **Record an
exposure** takes the event's name, start and end and a description; its
**Place**; any number of **contacts**, each a person on file by their id,
with a contact type, disposition and dates; and any number of **sources**.
**Record exposure** writes it all at once, and answers **The exposure has been
recorded.** The **Exposures** table lists every exposure, newest first, with
its contacts and sources under it. A recorded exposure is not edited.

**The Notes tab** holds three things:

- **Attachments**: choose a **Category** (Correspondence, Laboratory results,
  Letter, X-ray or Other) and a file — PDF, text, CSV, Word, PNG, JPEG, GIF or
  TIFF, up to 30 MB — and press **Attach file**. Attachments are listed with
  their category, size, type and when they were attached.
- **Notes**: a note type (clinical or administrative), its text, and
  optionally who wrote it and the exposure it is about; **Add note**. Tick a
  note's **Strikethrough** to strike it out: it stays on the record, struck.
- **Tasks**: a task's name, due date, priority, who it is assigned to, and
  whether it repeats daily, weekly or monthly until a date; **Add task**. A
  repeating task is added as one dated task per occurrence. **Complete**
  closes a task; the **Show** filter switches between **To do**, **Done** and
  **All tasks**.

**Tasks** (`/tasks`) is the same task list across every case, with the report
each task belongs to; tasks are added from a case's Notes tab.

**Walk through it:**
[Log an exposure event](../walkthroughs/log-an-exposure-event.md),
[Add a contact to a case](../walkthroughs/add-a-contact-to-a-case.md),
[Attach a document](../walkthroughs/attach-a-document.md),
[Work a task list](../walkthroughs/work-a-task-list.md).

## Reference data administration and reporting

These screens need `manage_reference_data`, except saved views
(`view_cases`) and reports (`run_reports`).

- **Agencies** (`/agencies`): the health departments. Open one to set its
  **Short name** and the **Jurisdiction zip codes** it covers, separated by
  commas, spaces or new lines; **New agency** adds one.
- **Facilities** (`/facilities`): **New facility** adds one under the
  **Agency** that owns it, with its address and phone; open one to change it.
- **Field visibility** (`/core-elements`): which fields the Clinical,
  Investigation and Administrative tabs show. Each field has a **Hidden by
  default** switch, which saves as soon as you flip it. An **override** shows
  or hides one field for a condition, an agency, or both; **Delete** removes
  it.
- **Saved views** (`/saved-views`): a named selection of case morbidity
  reports — the **Columns** it exports, and the **Condition**, **Investigating
  agency**, **Workflow state**, **Case status** and onset dates it selects by.
  A filter left at **Any**, or a date left empty, selects every report.
  **Save view** saves it; each view in the list has **Export CSV**, which
  downloads the selected reports as a CSV file with the chosen columns, and
  **Edit** and **Delete**. A view is your own.
- **Reports** (`/reports`): counts of case morbidity reports, grouped by
  condition, agency, case status, workflow state or MMWR year, optionally
  between two onset dates; press **Run**.

**Walk through it:**
[Configure an agency](../walkthroughs/configure-an-agency.md),
[Add a facility](../walkthroughs/add-a-facility.md),
[Save a view](../walkthroughs/save-a-view.md),
[Export a dataset](../walkthroughs/export-a-dataset.md).

## Inbound laboratory messages

Electronic laboratory reports arrive as HL7 files, which Survsys reads and
stages for review. A message that cannot be read is staged as an exception,
with what was wrong with it.

**Staged messages** (`/staged-messages`) is the queue, oldest first, with each
message's source, when it was received, its review state, its QA flag and any
exception. The **Review state** list narrows it to the messages **Queued for
review**, **Held for review**, or held as an **Exception**.

Pressing a message's source opens it: its details, then the raw message as it
arrived. To **promote it to a case**, search for the person it is about and
press **Use this person**; then give the date first reported, the disease and
the investigating agency, and press **Promote to a case**. Survsys opens the
case, says so, and links to it by its record number. A promoted message shows
**This message has already been promoted to a case.** and offers no second
promotion, which is how a duplicate case is prevented.

Choosing the person is the reviewer's: Survsys does not match a message to a
person on file by itself, and does not merge people.

**Walk through it:**
[Review a staged message](../walkthroughs/review-a-staged-message.md),
[Promote a message to a case](../walkthroughs/promote-a-message-to-a-case.md),
[Resolve a duplicate](../walkthroughs/resolve-a-duplicate.md).

## CDC notification

A case is queued for notification to the CDC whenever it changes, and can be
queued by hand from the **CDC notification** panel on its report: **Queue for
notification** answers **This case was queued for notification at** and the
time.

- **Notifications** (`/notification-queue`): the cases waiting, oldest first,
  each with its patient, condition and when it was queued. **Hold** keeps a
  case back; **Release** lets it go again.
- **Export log** (`/export-log`): the history of exports, newest first, each
  with its record number, when it was queued and sent, and its outcome; an
  export not yet sent reads a dash. Pressing a record number opens the export,
  with its guide and the HL7 message generated for it, whole.

Survsys 1.0.0 queues cases and shows the export log; it does not yet generate
the messages from the queue or send them to the CDC itself. What the export
log shows is what has been logged against each case.

**Walk through it:**
[Queue a case for notification](../walkthroughs/queue-a-case-for-notification.md),
[Review a generated message](../walkthroughs/review-a-generated-message.md),
[Inspect the export log](../walkthroughs/inspect-the-export-log.md).

## Outbreaks, forms, treatments and audit

**Outbreaks** (`/outbreaks`) lists every outbreak, newest first. **New
outbreak** takes its number, name, date, condition, owning agency, status
(**Active** or **Completed**) and description; **Save** returns to the list.
Opening an outbreak shows those fields again, and four panels:

- **Agency access**: the agencies, besides the owner, that may see it;
  **Save access**.
- **Outbreak report**: one box per report section, each saved on its own.
- **Activity log**: **Add entry** records what happened, with its author, and
  the log lists it with the time.
- **Linked cases**: the cases linked to the outbreak, each by its record
  number, person and condition.

A case is linked to an outbreak from its report: on the **Administrative**
tab, choose it in the **Outbreak name** list and press **Save & Continue**;
choose **(none)** to unlink it. The Administrative tab also records the case
statuses by id, whether the case is outbreak associated, the investigation's
start, completion and outcome, an event name, and when results were reported
and the CDC verbally notified.

**Supplemental forms** are the condition-specific questions a case asks.
**Forms** (`/forms`) builds them: a form has a name, its condition and an
**Auto-assign** tick, and is made of sections, each holding questions. A
question has its words, an answer type — **Text**, or **Coded** with the
**Choices** you add — whether it is required, and help text; sections and
questions are reordered with **Up** and **Down**. A form ticked
**Auto-assign** is given to every case opened on its condition from then on.
On a case, the **Forms** tab lists the forms assigned to it; **Open** shows
one, and **Save answers** saves what you entered.

**Treatments** (`/treatments`) is the list of treatments, each with whether it
is active or a default, and the conditions it is given for, ticked on its own
screen. The case itself records only whether treatment was given, on its
Clinical tab.

**The Vaccines tab** records the vaccines administered to the case's person:
the vaccine, chosen from the catalogue, its dose number, the date
administered, lot number, expiration date and a comment. **Add vaccine** saves
one; **Edit** and **Remove** change one already recorded.

**Audit** is kept by the database, not shown on a screen: every row that is
inserted, changed or deleted writes an audit entry, and every time a user
opens a report writes an access record of who opened it and when. Survsys has
no screen to read them yet.

**Walk through it:**
[Open an outbreak and link cases](../walkthroughs/open-an-outbreak-and-link-cases.md),
[Complete a supplemental form](../walkthroughs/complete-a-supplemental-form.md),
[Record a treatment](../walkthroughs/record-a-treatment.md).
