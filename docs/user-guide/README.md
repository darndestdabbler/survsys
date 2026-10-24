# Survsys user guide

What each screen is for and how the work is done on it, as Survsys 1.0.0 has
it. Every section below describes a screen you can open today, and nothing in
this guide describes something you cannot; where a screen stops short of what
you might expect, the section says so.

The quickest way to learn a screen is not this guide but
[the walkthroughs](../walkthroughs/README.md), which you perform rather than
read; each section here links the ones that perform its screens.

| Section | Phases |
|---|---|
| [Signing in, and who may do what](#signing-in-and-who-may-do-what) | 4, 8, 12 |
| [Reference data — the conditions catalogue](#reference-data--the-conditions-catalogue) | 0, 10–12 |
| [People](#people) | 1, 9 |
| [Case records](#case-records) | 1, 8 |
| [Clinical, laboratory and workflow](#clinical-laboratory-and-workflow) | 2, 8 |
| [Investigation, contacts, notes and tasks](#investigation-contacts-notes-and-tasks) | 3, 8 |
| [Classification on save](#classification-on-save) | 12 |
| [Reference data administration and reporting](#reference-data-administration-and-reporting) | 4, 10, 12 |
| [Inbound laboratory messages](#inbound-laboratory-messages) | 5, 10 |
| [Electronic case reports](#electronic-case-reports) | 13 |
| [CDC notification](#cdc-notification) | 6, 11 |
| [Outbreaks, forms, treatments and vaccines](#outbreaks-forms-treatments-and-vaccines) | 7, 8 |
| [Audit and system settings](#audit-and-system-settings) | 12 |

**Three things hold on every screen.** A coded field — a gender, a race, a
place type, a test type, a disposition — is a list, showing each code by its
name, over the reference code lists kept on the **Code lists** screen; a value
on file that is not on its list is still shown, as itself, so nothing on file
is lost. A test type list offers the test types listed for the report's
condition, or all of them where none is listed. A Yes/No/Unknown answer is a
list of those three. A date is typed as `yyyy-mm-dd`; anything else is
refused with **Give** the field **as yyyy-mm-dd, or leave it empty.** And
lists that run long show 50 rows a page, with **Previous** and **Next** under
them.

---

## Signing in, and who may do what

Survsys asks everyone to sign in, and sends you to its **Sign in** page until
you do: type your **User name** and **Password** and press **Sign in**. A
session lasts eight hours; **Sign out**, at the right-hand end of the
application bar beside your name, ends it sooner. Five wrong passwords in a
row lock the account for fifteen minutes; the lockout ends by itself, and
the page gives the same answer for it as for a wrong password.

What you may do is set by the **roles** you hold, and each role is a set of
privileges:

| Privilege | What it lets you do |
|---|---|
| `view_cases` | Read people, cases and everything filed under them; the queues, outbreaks, staged messages, eCR documents, notifications, the export log, tasks and saved views. |
| `edit_cases` | Create and change cases and what is filed under them, and outbreaks; send a case to IBIS. |
| `manage_reference_data` | Change the conditions, agencies, facilities, field visibility, code lists, vocabulary, towns and zip codes, vaccines, treatments, forms, the notification set-up and the eCR mappings and senders. Reading them needs only to be signed in. |
| `manage_users` | Add and change users and roles; read the audit and access audit; keep the system settings. |
| `run_reports` | Run the reports. |
| `route_cases` | Route a case to another agency. |
| `assign_investigators` | Name a case's investigator. |
| `approve_cases` | The state's review: approve, reject and reopen. |
| `merge_persons` | Find duplicate people and merge them. |
| `all_jurisdictions` | See every agency's cases. |

**Which cases you see.** Each user is given one or more agencies on the
**Users** screen. You see a case when its investigating agency is one of
yours, when it is shared with one of yours, or when you hold
`all_jurisdictions`; any other case answers as though it were not there, on
its own screen, in the case search and on everything filed under it. A
change to your agencies or roles takes effect when you next sign in. The work
queue, the agency board, the task list, saved-view exports and counts and the
case-count report list only the cases you see, and the queue of an agency
that is not yours is empty. A case shared with your agency is read-only to
you: it opens saying so, without **Edit** or **Delete**, and nothing on it
or filed under it can be changed.

The application bar shows only the buttons your privileges open, in this
order: **Conditions**, **People**, **Events**, **New CMR**, **Work queue**,
**Agency board**, **Outbreaks**, **Staged messages**, **Intake statistics**,
**eCR**, **eCR quality**, **Notifications**, **Export log**,
**Reconciliation**, **Tasks**, **Saved views**, **Reports**, **Duplicate
persons**, **Agencies**, **Facilities**, **Field visibility**, **Forms**,
**Treatments**, **Code lists**, **Vocabulary**, **eCR conditions**, **eCR
senders**, **Towns and zip codes**, **Vaccines**, **Notification guides**,
**PHIN variables**, **Value sets**, **Hepatitis lab setup**, **Users**,
**Roles**, **Audit**, **Access audit** and **System settings**. It wraps onto
more lines rather than run off the window. The API tier checks the privilege
on every request, so a screen opened by its address without the privilege
shows what it was refused rather than the data.

**Users** (`/users`) lists every user by user name, display name, roles,
agencies and whether they **May sign in**. **New user** clears the form;
enter a **User name**, **Display name** and **Password**, tick their roles
and **Agencies**, and press **Save user**. Editing a user leaves the password
as it was unless you type a new one. Untick **May sign in** to stop someone
signing in without deleting them. **Roles** (`/roles`) is the same for roles:
a **Name** and a tick for each privilege, saved with **Save role**.

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

With `manage_reference_data`, **New condition** adds a condition, **Export
conditions CSV** and **Import conditions CSV** move the catalogue in and out
(an import is previewed first, as every import is — see
[Reference data administration](#reference-data-administration-and-reporting)),
and pressing a condition's name opens it to edit:

- its **Name**, **CDC code**, **Active** and **Notification guide**;
- **Notified to CDC when the case status is**: the case statuses that send a
  case of this condition to the CDC;
- two intake switches, **Assign lab messages for out-of-state patients** and
  **Filter lab messages whose results are all preliminary**;
- the lead-in text shown above its contacts, exposure places and treatments;
- for a condition already saved, its **ELR intake rules** — the message
  filter and case-management rules a laboratory message is judged by, each
  with its conditions — saved with **Save rules**, and **Labs carried in the
  CDC notification**, the organisms and test types its notification includes
  or leaves out, saved with **Save lab overrides**.

**Save** returns to the catalogue. The catalogue has no filters or sorting.

**Walk through it:**
[Review the conditions catalogue](../walkthroughs/review-the-conditions-catalogue.md).

---

## People

**Finding a person.** **People** (`/people`) searches everyone on file by
**Last name**, **First name**, **Middle name**, **Date of birth**, **Phone**,
**Birth sex** and **Identifier**; press **Search** (or Enter), and **Clear**
to start again. Every box empty lists everyone. A name finds a person by
their current name, a former name or an alias, and an identifier matches its
value exactly. The table shows each person's **ID**, name, birth date and
birth sex and, for a search with criteria, a **Score** and **Why**: how
closely the person matches what you typed, as an exact, probable or no match,
and the reasons. A search matching more people than the screen shows asks you
to add to it.

**The person screen.** A person's name opens their own screen
(`/people/` and their id): their demographics, aliases, parents or guardians,
**former names**, **identifiers** (each with its type and assigning
authority), addresses, telephones and email addresses, and their **Cases**,
each opening its report. The screen is read-only; a person is changed on the
Demographic tab of any of their reports.

**What a save does to a person.** A change of name keeps the old name as a
former name, dated that day, so a search by it still finds them. An address
is tidied as it is saved — spacing, capitals, the state as its two-letter
code, the zip as five digits or zip plus four — and of the addresses with no
end date the one that began last is current, the others ended at its start.
A Connecticut address with a blank county or planning region has it filled
from its town, and a blank county from its zip. A Connecticut address carries
both its historical county and its planning region (labelled **Planning
region**, or as configured).

Where geocoding is turned on, an address saved with a street and no
**Latitude** or **Longitude** is geocoded — by the US Census Geocoder, or by
Google where Census finds no match — on the Demographic tab and when a lab
message or eCR adds it. Each address row and the person screen say where its
coordinates came from: **Census Geocoder**, **Google** or **Typed by hand**.
Coordinates you type are kept as typed by hand; change the street and the
address is geocoded again. If neither service finds it, the address saves
with no coordinates.

**Duplicate persons** (`/persons/duplicates`, with `merge_persons`) lists
pairs of people who may be one person, best first, each with its score,
verdict and reasons. Pressing a pair opens **Merge persons**: the two records
side by side. Press **Keep this record** on the one to keep, then **Merge**:
the other's cases, labs, contacts, identifiers, names and addresses move to
the kept person, whose own demographics are unchanged, and the other record
stays on file, merged, where no search finds it.

**Walk through it:** [Find a person](../walkthroughs/find-a-person.md),
[Find a person by a former name](../walkthroughs/find-a-person-by-a-former-name.md),
[Record a person's address history](../walkthroughs/record-a-persons-address-history.md),
[Merge duplicate persons](../walkthroughs/merge-duplicate-persons.md).

## Case records

**Opening a case morbidity report** is two steps, as it was in EpiTrax.
**New CMR** opens step 1, the person search: press **Create CMR** on the
person's row, or, when a search finds nobody, **Create person and CMR** to
create them from what you typed (it needs at least a last name). Step 2 takes
the **Event type** (**Morbidity** or **Assessment**), the date **First
reported to public health**, the **Disease** and the **Investigating
agency**; **Save & Continue** creates the report and opens it. Each report
gets a nine-digit record number: the year, then its place among that year's
reports. A contact event is not opened here: it is made from an exposure
(see [Contacts](#investigation-contacts-notes-and-tasks)).

**Finding a case.** **Events** (`/events`) searches every case you may see by
**Record #**, **Last name** and **First name** (each matching from its
start), **Disease**, **Investigating agency**, **Workflow state**, **Case
status** and an **Onset from**–**Onset to** range; **Show deleted cases**
adds the deleted ones, marked **(deleted)**. Every box empty lists every
case, newest first. Each row reads the record number, patient, disease,
agency, workflow state, status, MMWR week, onset date, county and planning
region, and the record number opens the report. Record numbers on the work
queue and the agency board open their reports too.

**The report form** (`/event/edit/` and the report's id) opens read-only.

- **Edit** takes the case for you, for thirty minutes, and opens it for
  editing; **Done** gives it back, as leaving the page does. Anyone else who
  opens the case meanwhile reads **Being edited by** your name **until** a
  time, and cannot edit it.
- **Delete** asks first, then takes the case out of the case search while
  keeping it; a deleted case says so and offers **Restore**.
- **Print** opens a printable view of the whole case, for the browser to
  print or save as PDF. Another user's private notes are not in it.
- **History of changes**, for those holding `manage_users`, opens the case's
  audit.
- The **header** reads the patient's name, the record number, the event type,
  the disease, the investigating agency, the event date, the MMWR week, the
  case status, the investigator, and the state and LHD case statuses; under
  it is a **Brief note** box kept on the case with **Save note**. A contact
  event also offers **Promote to morbidity report**.
- Four panels: **Case status** (choose a status and press **Save status**),
  **Workflow Options** (below), **CDC notification** (see
  [CDC notification](#cdc-notification)) and, for those holding `edit_cases`,
  **IBIS**, whose **Send to IBIS** sends the case's record number, condition,
  agency, status, onset and MMWR week to IBIS and answers **IBIS accepted this
  case on** a date.
- Eleven **tabs**: Demographic, Clinical, Laboratory, Investigation,
  Administrative, Forms, Vaccines, Treatments, Encounters, Contacts and
  Notes. Each tab saves on its own, with its own button and its own green
  message.

**The Demographic tab** records the person the report is about: their name;
parents or guardians and aliases (each **Add**ed as a row); date of birth, or
days old for a newborn, with their current age and age at onset worked out
for you; any number of addresses, telephone numbers and email addresses;
birth sex, current gender, primary language, ethnicity, country of birth;
any number of race codes; and their identifiers. **Save & Continue** saves
the person whole and answers **The demographics have been saved.** A last
name is required. What you save here is the person's, so every report on
them reads it.

**Walk through it:**
[Create a case morbidity report](../walkthroughs/create-a-case-morbidity-report.md),
[Find a case](../walkthroughs/find-a-case.md),
[Record demographics](../walkthroughs/record-demographics.md).

## Clinical, laboratory and workflow

**The Clinical tab** records the disease onset date, the date diagnosed,
whether treatment was given, whether the patient died — the date of death
appearing once the answer is **Yes** — and pregnancy, with its due date and
expected delivery facility. **Save & Continue** answers **The clinical data
has been saved.** Saving it works the MMWR week out again, from the onset
date or, where there is none, the date diagnosed.

**The Laboratory tab** lists the report's specimens and records new ones. A
specimen carries its accession number, collection date, source, the lab,
provider, diagnostic and reporting facilities, and whether it was sent to the
state lab. Under it go any number of **tests** (type, status, LOINC code,
reference range, test date), under each test any number of **results**
(organism, value, result code, units, comment, and whether it is the primary
result), and any **antimicrobial susceptibility** results. **Record specimen**
writes the specimen with everything under it, and answers **The specimen has
been recorded.** **Edit** on a specimen opens it to change, with its tests,
results and susceptibilities, each of which can be removed; **Delete** asks
first, and a specimen with tests still under it is not deleted.

**The workflow** takes a case through seven states:

| Move | From | To | Needs |
|---|---|---|---|
| **Accept** | Assigned to LHD | Accepted | `edit_cases` |
| **Investigate** | Accepted, Rejected by state | Under investigation | `edit_cases` |
| **Close** | Under investigation | Closed | `edit_cases` |
| **Route to agency** | Assigned to LHD, Accepted, Under investigation, Rejected by state | Assigned to LHD, at the agency chosen | `route_cases` |
| **Assign investigator** | Accepted, Under investigation | Under investigation, with the investigator chosen | `assign_investigators` |
| **Do not investigate** | Assigned to LHD, Accepted | Not investigated | `edit_cases` |
| **Reopen** | Closed, Not investigated, Approved by state | Under investigation | `approve_cases` |
| **Approve** | Closed | Approved by state | `approve_cases` |
| **Reject** | Closed | Rejected by state | `approve_cases` |

The report's **Workflow Options** panel shows the workflow status and the
investigator, a **Note** box, the **Route to agency** and **Investigator**
lists where those moves are offered, and one button per move the case's
state and your privileges allow. Every move is kept, with who made it, when
and its note, in the **Workflow history** at the foot of the Administrative
tab, newest first.

- **Agency board** (`/queue/agency`): choose an **Agency** to see its whole
  queue, including the cases shared to it. Its **Assign a report** panel
  assigns a report, by its id, to the agency that will investigate it; its
  **Share a report read-only** panel lets another agency see it.
- **Work queue** (`/queue`): choose an **Agency** to see the cases it
  investigates, each with the one move forward it is ready for —
  **Accept**, then **Begin investigation**, then **Close**. The other moves
  are on the report.

**Walk through it:**
[Enter a laboratory result](../walkthroughs/enter-a-laboratory-result.md),
[Correct a laboratory result](../walkthroughs/correct-a-laboratory-result.md),
[Assign and accept a case](../walkthroughs/assign-and-accept-a-case.md),
[Route a case to another agency](../walkthroughs/route-a-case-to-another-agency.md),
[Close an investigation](../walkthroughs/close-an-investigation.md).

## Investigation, contacts, notes and tasks

**The Investigation tab** records whether the patient is a food handler, a
health care worker, in group living or associated with a day care; where the
case was imported from, its transmission mode, its risk factors, two free
boxes of other data, and the patient's occupation. **Save & Continue**
answers **The investigation data has been saved.**

**The Administrative tab** records the LHD and state case statuses, whether
the case is outbreak associated and its **Outbreak name**, the
investigation's start, completion and outcome, an event name, and when
results were reported and the CDC verbally notified; it shows the record
number, MMWR week and year and when the record was created, and ends with
the **Workflow history**.

**The Contacts tab** records the exposure events a case is thought to have
come from, and the people exposed alongside the patient.

- **Record an exposure** takes the event's name, start and end and a
  description; its **Place**; any number of **contacts**, each with a contact
  type, disposition and dates; and any number of **sources**. **Record
  exposure** writes it all at once, and answers **The exposure has been
  recorded.**
- The **Exposures** table lists every exposure with its contacts and sources.
  **Edit** opens one to change — a contact's disposition and its date among
  it — and **Delete** asks first, refusing an exposure that still has
  contacts or sources.
- **Add contact** on an exposure finds the person by search, or creates them
  with **Create person and add as contact**, and makes them a **contact
  event** of their own: a report on the same condition and agency, reached by
  the **(contact event)** link beside them. On that report, **Promote to
  morbidity report** makes it a case of its own.
- **Import contacts** takes a CSV file of people for an exposure — a header
  row naming at least `last_name` and `birth_date`, and any of `first_name`,
  `middle_name`, `sex`, `street`, `unit`, `city`, `state`, `zip`, `county`,
  `phone` and `email` — and makes each good row a new person and a contact
  event. It answers how many were imported and refused, with each refused
  line and why.

**The Notes tab** holds three things:

- **Attachments**: choose a **Category** and a file — PDF, text, CSV, Word,
  PNG, JPEG, GIF or TIFF, up to 30 MB — and press **Attach file**.
  Attachments are listed with their category, size, type and the day they
  were attached. The screen does not download or remove an attachment.
- **Notes**: a note type (clinical or administrative), its text, optionally
  who wrote it and the exposure it is about, and **Private**; **Add note**. A
  private note is seen only by the user who wrote it. Tick a note's
  **Strikethrough** to strike it out: it stays on the record, struck. A note
  is not otherwise changed.
- **Tasks**: a task's name, due date, priority, category, who it is assigned
  to, and whether it repeats daily, weekly or monthly until a date; **Add
  task**. A repeating task is added as one dated task per occurrence.
  **Complete** closes a task; the **Show** filter switches between **To do**,
  **Done** and **All tasks**.

**Tasks** (`/tasks`) is the same task list across every case, with the report
each task belongs to; tasks are added from a case's Notes tab.

**Walk through it:**
[Log an exposure event](../walkthroughs/log-an-exposure-event.md),
[Add a contact to a case](../walkthroughs/add-a-contact-to-a-case.md),
[Promote a contact to a case](../walkthroughs/promote-a-contact-to-a-case.md),
[Attach a document](../walkthroughs/attach-a-document.md),
[Work a task list](../walkthroughs/work-a-task-list.md).

## Classification on save

Every save of a case — on its creation, its workflow moves and its Clinical,
Laboratory, Investigation, Administrative and Encounters tabs, though not
**Save status** — runs Connecticut's classification rules over it:

- **Out of state.** A case with no status whose patient's current address is
  outside Connecticut becomes **Out of state**, and keeps its agency. One
  whose address comes back to Connecticut has the status cleared.
- **Jurisdiction.** A case with no agency is given the agency its patient's
  zip code belongs to.
- **By disease.** Chlamydia, gonorrhea, hepatitis C and COVID-19 cases are
  confirmed, made probable or made not a case by their labs (and, for
  COVID-19, symptoms and an epidemiological link); an influenza-associated
  hospitalization is checked for an encounter at a facility, which stands
  for its hospitalization; syphilis and measles are left to the
  investigation. A rule sets a status only where the case has none, so a
  status a person chose is never overwritten.
- **Contacts.** A chlamydia, gonorrhea or syphilis contact event is closed
  once the LHD has accepted it.

What the rules found is shown in a blue box under the tab's green message,
the header's case status changes without a reload, and each finding is kept
as an administrative note beginning **Classification:**, struck out once it
no longer holds.

**Walk through it:**
[See a case classified on save](../walkthroughs/see-a-case-classified-on-save.md).

## Reference data administration and reporting

These screens need `manage_reference_data`, except saved views
(`view_cases`) and reports (`run_reports`). **Every import is previewed:**
choose a CSV file and the screen says how many rows it would add, change and
refuse, with each refused row and why; nothing is saved until you press
**Confirm import**. A row is matched to an existing one by its key, and a row
the file leaves out is kept. Every **Export** downloads the whole table, and
reads back in unchanged.

- **Agencies** (`/agencies`): the health departments. Open one to set its
  **Short name**, the **Jurisdiction zip codes** it covers (separated by
  commas, spaces or new lines, with their own export and import), and its
  **Notification e-mail addresses**; **New agency** adds one.
- **Facilities** (`/facilities`): **New facility** adds one under the
  **Agency** that owns it, with its short name, address and phone; open one
  to change it. A laboratory that sends messages is a facility whose short
  name is its sending facility.
- **Field visibility** (`/core-elements`): which fields the Clinical,
  Investigation and Administrative tabs show. Each field has a **Hidden by
  default** switch, which saves as soon as you flip it. An **override** shows
  or hides one field for a condition, an agency, or both; **Delete** removes
  it.
- **Code lists** (`/codes`): the 29 reference code lists every coded field is
  chosen from. Choose a **List** to see its codes, retired ones included;
  **Edit** on a row, or **New code**, fills the form — **Code**,
  **Description**, **Sort order** and **Active** — and **Save** saves it. A
  code is never deleted: clear **Active** to retire it, and it is no longer
  offered but still names the records that hold it. On the test type list
  the form also ticks the conditions a test type is offered for; the county
  list carries each county's state, FIPS code and kind.
- **Towns and zip codes** (`/places`): the 169 Connecticut towns, each with
  its county and planning region, and the zip codes, each with its county —
  what a Connecticut address's county and planning region are filled from.
- **Vaccines** (`/vaccines`) and **Treatments** (`/treatments`): the
  catalogues a case's vaccines and treatments are chosen from. A vaccine is
  retired, never deleted; a treatment is ticked for the conditions it is
  offered for.
- **Saved views** (`/saved-views`): a named selection of case morbidity
  reports — the **Columns** it exports, and the **Condition**, **Investigating
  agency**, **Workflow state**, **Case status** and onset dates it selects by.
  A filter left at **Any**, or a date left empty, selects every report.
  **Save view** saves it; each view in the list has **Export CSV**, which
  downloads the selected reports as a CSV file with the chosen columns, and
  **Edit** and **Delete**. A view is your own.
- **Reports** (`/reports`): counts of case morbidity reports, grouped by
  condition, agency, case status, workflow state or MMWR year, optionally
  between two onset dates; press **Run**. Under it, **Saved view report**
  counts what one of your saved views selects, grouped by any of its
  columns, and **Export CSV** downloads the counts.

**Walk through it:**
[Configure an agency](../walkthroughs/configure-an-agency.md),
[Add a facility](../walkthroughs/add-a-facility.md),
[Save a view](../walkthroughs/save-a-view.md),
[Export a dataset](../walkthroughs/export-a-dataset.md),
[Run an ad-hoc report](../walkthroughs/run-an-ad-hoc-report.md).

## Inbound laboratory messages

Electronic laboratory reports arrive as HL7 v2.5.1 messages, from a drop
folder or over MLLP, and each order in a message is staged for review as a
message of its own. As it is staged Survsys checks the sending laboratory is
on file and, for a sender held to the Connecticut profile, that the message
conforms; translates each result through the vocabulary to its condition,
organism and interpretation; runs the condition's intake rules; and chooses
the investigating agency from the patient's zip code. A message that cannot
be read, comes from an unknown sender, does not conform or maps to no
condition is held as an **Exception**, with what was wrong.

**Where a message goes next.** Where automatic assignment is configured, a
message whose patient is one person on file without doubt, and whose
condition's rules allow it, is **assigned** to a new case or, where the
person has an open case on the condition, used to update that case — its
labs added, knitted onto the lab already there, or set aside as a duplicate.
A message in doubt is **Held for review**. One whose condition does not open
cases is **Graylisted** until a case appears for it, and released for review
when one does. An out-of-state patient's message is held **Out of state**.

**Staged messages** (`/staged-messages`) is the queue, oldest first, with each
message's source, when it was received, its review state, its QA flag and any
exception. The **Review state** list narrows it to one of its fifteen states.
Tick messages (or **Select page**) and press **Move** (to the state chosen in
**Move to**), **Retry** (translate a held message again against the
vocabulary and rules now on file) or **Delete**.

Pressing a message's source opens it: its details, then the raw message as it
arrived. To **promote it to a case**, use the proposed match — the person on
file who best matches the patient, with **Use this person**, or **Create new
person** — or search for the person and press **Use this person** on their
row; then give the date first reported, the disease and the investigating
agency, and press **Promote to a case**. Survsys opens the case with the
message's specimen, tests and results on it and the message's address as the
person's current one, says so, and links to it by its record number. A
promoted message shows **This message has already been promoted to a case.**
and offers no second promotion.

**Intake statistics** (`/staged-messages/statistics`) counts the messages
received each day between two dates, by sending facility and review state.
**Vocabulary** (`/vocabulary`, with `manage_reference_data`) keeps the
master and laboratory-specific LOINC codes, organisms, specimens and results
the translation reads, each with export and import. Where it is configured,
each agency's notification e-mail addresses are sent a list of the messages
assigned to its cases.

**Walk through it:**
[Review a staged message](../walkthroughs/review-a-staged-message.md),
[Promote a message to a case](../walkthroughs/promote-a-message-to-a-case.md),
[Resolve a duplicate](../walkthroughs/resolve-a-duplicate.md),
[Find a message assigned automatically](../walkthroughs/find-a-message-assigned-automatically.md),
[Update an existing case from a laboratory message](../walkthroughs/update-an-existing-case-from-a-laboratory-message.md),
[Release a graylisted message](../walkthroughs/release-a-graylisted-message.md),
[Maintain a LOINC mapping](../walkthroughs/maintain-a-loinc-mapping.md).

## Electronic case reports

Electronic initial case reports (eICRs) and their reportability responses
(RRs) arrive as CDA documents in a test or a production drop folder. Each is
validated against the CDA schema and the HL7 schematron, and stored with what
validation found, whatever it found. An RR's conditions are kept when the
responsible agency is this one and the determination is reportable or may be
reportable; a kept condition whose code maps to a catalogue condition and is
reportable is **Reportable**, and any other **Needs review**.

Once an eICR and a reportable RR for it are both in, Survsys finds the
patient — one person on file without doubt, or a new person where nobody
matches — and opens a case on each reportable condition, or adds to the
person's open case on it, writing their demographics, address, encounter,
problems, onset date, pregnancy, occupation, medications and travel. A later
version of the eICR updates what an earlier one wrote, unless someone has
changed it since. A document whose patient is in doubt is held for review;
Survsys has no screen for settling a held document.

- **eCR** (`/ecr`): the documents, newest first, by **Feed**, with **Needs
  review** to show only those waiting. Each row reads its kind, document id,
  version, feed, when it was received, its validation errors and warnings,
  and how many of its conditions wait for a reviewer. A document opens to the
  eICR as the HL7 stylesheet renders it, the RR's conditions and their
  routing, the cases it opened or the reason it is held, and its validation
  findings.
- **eCR quality** (`/ecr/quality`): for a feed and a date range, each
  sender's and condition's eICRs and RRs, their errors and warnings, how long
  an RR took, and the eICRs and RRs that have no partner.
- **eCR conditions** (`/ecr/condition-mappings`, with
  `manage_reference_data`): the codes an RR names, each mapped to a catalogue
  condition or left unmapped, with export and import.
- **eCR senders** (`/ecr/senders`): the healthcare organisations that send
  eICRs, by custodian id, with their onboarding stage and whether their
  results are taken as labs.

**Walk through it:**
[Review an eICR and its reportability response](../walkthroughs/review-an-eicr-and-its-reportability-response.md),
[Open a case from an eICR](../walkthroughs/open-a-case-from-an-eicr.md),
[Link an eICR to an existing case](../walkthroughs/link-an-eicr-to-an-existing-case.md),
[Map a reportable condition](../walkthroughs/map-a-reportable-condition.md).

## CDC notification

A case is queued for notification to the CDC whenever a change reaches it —
its labs, clinical, investigation, administrative or demographic data, its
status or its workflow — when its status is one its condition's print
criteria name. It can be queued by hand from the **CDC notification** panel
on its report: **Queue for notification** answers **This case was queued for
notification at** and the time. Where the queue worker is configured, Survsys
takes each case off the queue, builds its HL7 notification under its
condition's guide, sends it to the CDC (or writes it as a file for a PHINMS
or AIMS sender), and logs the outcome.

- **Notifications** (`/notification-queue`): the cases waiting, oldest first,
  each with its patient, condition and when it was queued. **Hold** keeps a
  case back; **Release** lets it go again; **Remove** takes it off. **Queue
  records** queues cases by record number, **Queue MMWR range** the cases of
  a span of MMWR weeks, and **Process now** sends the queue at once.
- **Export log** (`/export-log`): the history of exports, newest first, each
  with its record number, when it was queued and sent, its outcome and
  detail. **Resend** queues a case again; **Retract**, on an accepted export,
  queues a retraction. Pressing a record number opens the export: its
  messages, by severity and PHIN variable, and the HL7 message it sent,
  whole.
- **Reconciliation** (`/reconciliation-cutoff`): the MMWR year and week
  reconciliation runs to, and the verification totals it counts.
- **Notification guides**, **PHIN variables**, **Value sets** and
  **Hepatitis lab setup** (with `manage_reference_data`): the guides a
  notification is built under, with their PHIN variables, rule sets, linked
  guides and header defaults, a guide's export and import as JSON; the PHIN
  value sets and units of measure; and the grouping of hepatitis labs.

**Walk through it:**
[Queue a case for notification](../walkthroughs/queue-a-case-for-notification.md),
[Send a case to CDC](../walkthroughs/send-a-case-to-cdc.md),
[Review a generated message](../walkthroughs/review-a-generated-message.md),
[Inspect the export log](../walkthroughs/inspect-the-export-log.md),
[Retract a notification](../walkthroughs/retract-a-notification.md),
[Configure a notification guide](../walkthroughs/configure-a-notification-guide.md),
[Send a case to IBIS](../walkthroughs/send-a-case-to-ibis.md).

## Outbreaks, forms, treatments and vaccines

**Outbreaks** (`/outbreaks`) lists every outbreak, newest first. **New
outbreak** takes its number, name, date, condition, owning agency, status
(**Active** or **Completed**) and description; **Save** returns to the list.
Opening an outbreak shows those fields again, and its panels:

- **Agency access**: the agencies, besides the owner, that may see it;
  **Save access**.
- **Outbreak report**: one box per report section, each saved on its own.
- **Activity log**: **Add entry** records what happened, with its author, and
  the log lists it with the time.
- **Linked cases**: the cases linked to the outbreak, each by its record
  number, person and condition.
- **Attachments**, as on a case.

A case is linked to an outbreak from its report: on the **Administrative**
tab, choose it in the **Outbreak name** list and press **Save & Continue**;
choose **(none)** to unlink it.

**Supplemental forms** are the condition-specific questions a case asks.
**Forms** (`/forms`) builds them: a form has a name, its condition and an
**Auto-assign** tick, and is made of sections, each holding questions. A
question has its words, an answer type — **Text**, or **Coded** with the
**Choices** you add — whether it is required, and help text; sections and
questions are reordered with **Up** and **Down**. A form ticked
**Auto-assign** is given to every case opened on its condition from then on.
On a case, the **Forms** tab lists the forms assigned to it; **Open** shows
one, and **Save answers** saves what you entered.

**The Treatments tab** records the treatments given on the case: the
treatment, from its condition's list or, with **Show every treatment, not
only this condition's**, the whole catalogue; its start and stop dates, dose
and a comment. **Add treatment** saves one; **Edit** and **Remove** change
one already recorded.

**The Encounters tab** records the case's healthcare encounters: the date,
type, facility and a description, added, edited and removed the same way.

**The Vaccines tab** records the vaccines administered to the case's person:
the vaccine, chosen from the catalogue, its dose number, the date
administered, lot number, expiration date, manufacturer, information source
and a comment. **Add vaccine** saves one; **Edit** and **Remove** change one
already recorded.

**Walk through it:**
[Open an outbreak and link cases](../walkthroughs/open-an-outbreak-and-link-cases.md),
[Complete a supplemental form](../walkthroughs/complete-a-supplemental-form.md),
[Record a treatment](../walkthroughs/record-a-treatment.md),
[Record a case treatment](../walkthroughs/record-a-case-treatment.md).

## Audit and system settings

These screens need `manage_users`.

- **Audit** (`/audit`): every row Survsys inserts, changes or deletes is
  audited with who did it, when, and its values before and after. Give a
  **Table**, and optionally a row's **Key**, and press **Show**. A case's
  **History of changes** opens it at that case.
- **Access audit** (`/access-audit`): who signed in, opened a case, searched
  for cases or exported a saved view, newest first.
- **System settings** (`/system-settings`): named settings kept in the
  database, each with its value and a description, added, changed and
  deleted here. Survsys 1.0.0 reads none of them itself; its own settings are
  in its configuration, which the technical guide describes.

**Walk through it:**
[Review a case's audit history](../walkthroughs/review-a-cases-audit-history.md).
