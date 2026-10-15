# Survsys user guide

What each screen is for and how the work is done on it.

**This guide is a skeleton.** Every section Survsys will have is declared
below, each naming the phase of `../planning/solution-plan.md` that fills it. A
section is written only once the application does what it describes, so a
section marked *not yet built* describes nothing you can do today — and nothing
in this guide describes something you cannot.

To see what Survsys does today, the quickest route is not this guide: it is
[the walkthroughs](../walkthroughs/README.md), which you perform rather than
read.

| Section | Phase | State |
|---|---|---|
| [Reference data — the conditions catalogue](#reference-data--the-conditions-catalogue) | 0 | **Written** |
| Persons and case records | 1 | Not yet built |
| Clinical, laboratory and workflow | 2 | Not yet built |
| Investigation, contacts, notes and tasks | 3 | Not yet built |
| Reference data administration and reporting | 4 | Not yet built |
| Inbound laboratory messages | 5 | Not yet built |
| CDC notification | 6 | Not yet built |
| Outbreaks, forms, treatments and audit | 7 | Not yet built |

---

## Reference data — the conditions catalogue

The conditions catalogue is the list of conditions this installation knows
about: what an investigator will eventually be able to file a case against, and
which of those the state notifies the CDC about.

Survsys opens on it. The screen shows every condition by **Name**, with its
**CDC code** and whether it is **Active**:

- **CDC code** is the code the CDC knows the condition by. A condition that has
  none — one that is reportable to the state but not nationally notifiable —
  shows an em dash (**—**) rather than an empty cell.
- **Active** is **Yes** for a condition that may be reported and **No** for one
  that is kept in the catalogue but is not currently available for a new report.

The catalogue is read-only in this release: there is nothing on the screen to
change, add or filter. Administering the catalogue — adding a condition,
editing one, and the name and code filters the legacy screen carried — arrives
in phase 4.

**Walk through it:**
[Review the conditions catalogue](../walkthroughs/review-the-conditions-catalogue.md).

---

## Persons and case records

*Not yet built — phase 1.* Will cover: finding a person, person demographics,
addresses, telephone numbers and email, race and ethnicity, aliases, and
creating a case morbidity report.

## Clinical, laboratory and workflow

*Not yet built — phase 2.* Will cover: entering a laboratory result, clinical
and symptom details, and assigning, accepting and closing a case.

## Investigation, contacts, notes and tasks

*Not yet built — phase 3.* Will cover: adding a contact to a case, logging an
exposure event, attaching a document, notes, and working a task list.

## Reference data administration and reporting

*Not yet built — phase 4.* Will cover: administering conditions, agencies,
facilities and other reference data; saved views; and exporting a dataset.

## Inbound laboratory messages

*Not yet built — phase 5.* Will cover: reviewing a staged electronic
laboratory report, resolving a duplicate person, and promoting a message to a
case.

## CDC notification

*Not yet built — phase 6.* Will cover: queueing a case for notification,
reviewing the generated message, and inspecting the export log.

## Outbreaks, forms, treatments and audit

*Not yet built — phase 7.* Will cover: opening an outbreak and linking cases,
completing a supplemental form, and recording a treatment.
