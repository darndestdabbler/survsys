# Walkthroughs

One document per user story, written as numbered steps you perform against a
running Survsys. Perform them in any order; each one stands alone.

| Walkthrough | The user story | Performed by |
|---|---|---|
| [Review the conditions catalogue](review-the-conditions-catalogue.md) | A surveillance administrator opens the conditions catalogue to see which conditions may be reported, and which of them carry a CDC code. | `Survsys.E2ETests/ReviewTheConditionsCatalogueTests.cs` |
| [Find a person](find-a-person.md) | An investigator looks up whether a person is already on file before doing any case work on them. | `Survsys.E2ETests/FindAPersonTests.cs` |
| [Create a case morbidity report](create-a-case-morbidity-report.md) | An investigator opens a case morbidity report on a person already on file, in the legacy two steps, and ends on the report's own form. | `Survsys.E2ETests/CreateACaseMorbidityReportTests.cs` |
| [Record demographics](record-demographics.md) | An investigator records a patient's demographics on their case morbidity report, saves them, and reads them back. | `Survsys.E2ETests/RecordDemographicsTests.cs` |
| [Enter a laboratory result](enter-a-laboratory-result.md) | An investigator records a specimen, the test run on it and that test's result on a case morbidity report, and reads the specimen back. | `Survsys.E2ETests/EnterALaboratoryResultTests.cs` |
| [Assign and accept a case](assign-and-accept-a-case.md) | A state user assigns a case morbidity report to the local health department that will investigate it, and that department accepts it from its work queue. | `Survsys.E2ETests/AssignAndAcceptACaseTests.cs` |
| [Close an investigation](close-an-investigation.md) | A local health department investigator takes an accepted case through its investigation to closed, and reads the report still on file afterwards. | `Survsys.E2ETests/CloseAnInvestigationTests.cs` |
| [Log an exposure event](log-an-exposure-event.md) | An investigator logs the exposure event a case is thought to have come from — what it was, when it ran from and to, and where it happened — on the report's Contacts tab, and reads it back. | `Survsys.E2ETests/LogAnExposureEventTests.cs` |
| [Add a contact to a case](add-a-contact-to-a-case.md) | An investigator looks up the identifier of the person exposed alongside the patient, then adds them to the case as a contact of an exposure, with their contact type and disposition, and reads them back. | `Survsys.E2ETests/AddAContactToACaseTests.cs` |
| [Attach a document](attach-a-document.md) | An investigator attaches a file to a case morbidity report, filed under what kind of document it is, and reads it back on the report's Notes tab. | `Survsys.E2ETests/AttachADocumentTests.cs` |
| [Work a task list](work-a-task-list.md) | An investigator raises the follow-up a case needs as tasks, one of them repeating weekly, then works them off the task list screen and sorts them with its status filter. | `Survsys.E2ETests/WorkATaskListTests.cs` |
| [Configure an agency](configure-an-agency.md) | A surveillance administrator opens one of the health departments already on file and sets its short name and the zip codes of its jurisdiction, then reads the saved configuration back. | `Survsys.E2ETests/ConfigureAnAgencyTests.cs` |
| [Add a facility](add-a-facility.md) | A surveillance administrator adds a facility under the agency that owns it, with its address, and reads it back in the facilities list. | `Survsys.E2ETests/AddAFacilityTests.cs` |
| [Save a view](save-a-view.md) | A user defines a view of the case morbidity reports — its name, the columns it exports and the filters it selects by — saves it, reads it back in the list of views, and edits it to save over it. | `Survsys.E2ETests/SaveAViewTests.cs` |
| [Export a dataset](export-a-dataset.md) | A user creates a case morbidity report, saves a view that selects it, follows the view's Export CSV link, and reads the downloaded file — its header of the chosen columns and the report's row. | `Survsys.E2ETests/ExportADatasetTests.cs` |
| [Review a staged message](review-a-staged-message.md) | A surveillance reviewer opens the queue of inbound electronic lab reports, filters it to the ones held as exceptions, and opens one to read its source, received time, review state, QA flag, exception detail and the raw message it arrived as. | `Survsys.E2ETests/ReviewAStagedMessageTests.cs` |
| [Promote a message to a case](promote-a-message-to-a-case.md) | A surveillance reviewer opens a queued staged message, finds the person it is about, confirms the disease, investigating agency and first-reported date, promotes it, and reads back the notification and the link to the new case. | `Survsys.E2ETests/PromoteAMessageToACaseTests.cs` |
| [Resolve a duplicate](resolve-a-duplicate.md) | A surveillance reviewer promotes a staged message to a case, then returns to the same message and finds Survsys shows it is already a case and offers no second promotion, so no duplicate case is opened. | `Survsys.E2ETests/ResolveADuplicateTests.cs` |
| [Queue a case for notification](queue-a-case-for-notification.md) | An investigator opens a case morbidity report, queues it for CDC notification by hand from its CDC notification panel, reads the queued confirmation, and finds the case waiting on the notification queue. | `Survsys.E2ETests/QueueACaseForNotificationTests.cs` |
| [Review a generated message](review-a-generated-message.md) | A surveillance reviewer sends a case through the notification queue, opens its export from the CDC export log by its record number, and reads its record number, guide, queued and sent times, outcome and detail, then the message the queue worker generated for it, whole. | `Survsys.E2ETests/ReviewAGeneratedMessageTests.cs` |
| [Inspect the export log](inspect-the-export-log.md) | A surveillance reviewer sends a case through the notification queue, then opens the CDC export log and reads the case's row: its queued and sent times, its outcome, the send's detail, its processing id and queue. | `Survsys.E2ETests/InspectTheExportLogTests.cs` |
| [Open an outbreak and link cases](open-an-outbreak-and-link-cases.md) | An investigator opens an outbreak with its number, name, date, disease and owning agency, links a case morbidity report to it from the report's Administrative tab, and reads the case back among the outbreak's linked cases. | `Survsys.E2ETests/OpenAnOutbreakAndLinkCasesTests.cs` |
| [Complete a supplemental form](complete-a-supplemental-form.md) | An administrator builds a supplemental form for a condition, with a section, a text question and a coded question, set to auto-assign; an investigator opens a case on that condition, finds the form on its Forms tab, answers it, saves, and reads the answers back. | `Survsys.E2ETests/CompleteASupplementalFormTests.cs` |
| [Record a treatment](record-a-treatment.md) | A surveillance administrator adds a treatment, lists it for the diseases it is given for, saves it, and reads it back in the treatments list and on its own screen. | `Survsys.E2ETests/RecordATreatmentTests.cs` |
| [Find a case](find-a-case.md) | An investigator opens a case, then finds it again on the Events screen by its patient and disease, reads its row, and opens the case from its record number. | `Survsys.E2ETests/FindACaseTests.cs` |
| [Correct a laboratory result](correct-a-laboratory-result.md) | An investigator records a specimen with one test and one result on a case, then corrects that result's value and result code from the specimen's Edit, saves it, and reads the corrected result back from the database. | `Survsys.E2ETests/CorrectALaboratoryResultTests.cs` |
| [Route a case to another agency](route-a-case-to-another-agency.md) | A state user routes a case from the health department it was assigned to over to another from the report's Workflow Options, with a note, and reads the new agency and the move in the Administrative tab's workflow history. | `Survsys.E2ETests/RouteACaseToAnotherAgencyTests.cs` |
| [Promote a contact to a case](promote-a-contact-to-a-case.md) | An investigator adds a person exposed alongside the patient as a contact, which opens a contact event of their own, follows the link to that contact event, and promotes it to a morbidity report. | `Survsys.E2ETests/PromoteAContactToACaseTests.cs` |
| [Record a case treatment](record-a-case-treatment.md) | An investigator records the treatment a case was given, chosen from the treatments listed for its disease, with its start and stop dates and dose, on the report's Treatments tab, and reads it back. | `Survsys.E2ETests/RecordACaseTreatmentTests.cs` |
| [Find a person by a former name](find-a-person-by-a-former-name.md) | An investigator changes a patient's last name on their case's Demographic tab, then finds them on the People screen by the name they used to have, and reads the former name on their own screen. | `Survsys.E2ETests/FindAPersonByAFormerNameTests.cs` |
| [Record a person's address history](record-a-persons-address-history.md) | An investigator dates a patient's current address and adds the one they lived at before with the dates they lived there, saves, and reads both back — the earlier one historic by its dates and stored made regular. | `Survsys.E2ETests/RecordAPersonsAddressHistoryTests.cs` |
| [Merge duplicate persons](merge-duplicate-persons.md) | A user trusted to merge people finds two records for one person on the Duplicate persons screen, compares them side by side, keeps the record on file, and reads both records' cases under it. | `Survsys.E2ETests/MergeDuplicatePersonsTests.cs` |
| [Find a message assigned automatically](find-a-message-assigned-automatically.md) | A surveillance reviewer drops a lab's message into the ELR intake, finds it on the Staged messages queue assigned with no reviewer, and opens the case it made for a patient who was not on file from the Events screen. | `Survsys.E2ETests/FindAMessageAssignedAutomaticallyTests.cs` |
| [Update an existing case from a laboratory message](update-an-existing-case-from-a-laboratory-message.md) | An investigator reads a patient's open case with no laboratory result, drops a lab's message for that patient and disease into the ELR intake, and finds its specimen added to the same case rather than a second case opened. | `Survsys.E2ETests/UpdateAnExistingCaseFromALaboratoryMessageTests.cs` |
| [Release a graylisted message](release-a-graylisted-message.md) | A surveillance reviewer drops a lab's message whose rule opens no case into the ELR intake and finds it graylisted, opens a case for its patient and disease, and finds the message released to Held for review naming that case. | `Survsys.E2ETests/ReleaseAGraylistedMessageTests.cs` |
| [Maintain a LOINC mapping](maintain-a-loinc-mapping.md) | A surveillance administrator maps a LOINC code to a condition on the Vocabulary screen, maps a sending lab's own code to it, corrects the lab's mapping, and deletes both again. | `Survsys.E2ETests/MaintainALoincMappingTests.cs` |
| [Send a case to CDC](send-a-case-to-cdc.md) | A surveillance reviewer queues a case for CDC notification, processes the notification queue, finds the case gone from the queue and its export sent and accepted in the export log, and reads the message in the file written to the outbox. | `Survsys.E2ETests/SendACaseToCdcTests.cs` |
| [Retract a notification](retract-a-notification.md) | A surveillance reviewer sends a case to CDC, retracts its accepted export from the export log, processes the queue again, and reads the retraction's message, whose OBR carries the notification status X. | `Survsys.E2ETests/RetractANotificationTests.cs` |
| [Configure a notification guide](configure-a-notification-guide.md) | A surveillance administrator adds a PHIN variable to the catalogue, adds a notification guide, gives it that variable and its own sending facility in place of the default, saves both, and reads them back. | `Survsys.E2ETests/ConfigureANotificationGuideTests.cs` |

## The contract every entry here is held to

Taken from `../planning/solution-plan.md`, where it is stated in full. It is
the operator's non-negotiable, and it is why this index can promise that a
walkthrough listed here works.

1. **A walkthrough is one user story**, inferred from the legacy EpiTrax, EMSA
   and NMI code and documentation under `../../../nedss/` — never invented here.
2. **Each walkthrough has exactly one test named after it**, which performs its
   written steps in their written order and asserts the outcome the document
   promises. `Survsys.UnitTests/WalkthroughContractTests.cs` checks that this is
   true of every document in this folder, and that this index links every one of
   them, so a document whose test was forgotten fails the run of record rather
   than the operator.
3. **A step the test cannot perform is a defect in the application or in the
   document** — never a step the test skips, works around, or performs by a
   route the document does not describe.
4. **Every end-to-end run records video** of the clicks and the text entry,
   through Playwright's own recording. The recordings land in
   `Survsys.E2ETests/bin/<configuration>/net10.0/videos` as `.webm` files, one
   per test, and are artifacts of a run rather than files in this repository.
5. **No release ships a walkthrough whose test does not pass.** This suite is a
   release gate, not a report.

## Running them all

```
dotnet test Survsys.E2ETests/Survsys.E2ETests.csproj
```

That starts both tiers on ports it finds free, seeds the database, performs
every walkthrough in a browser and records each one. The four phase 10
walkthroughs — the three that drop a message into the ELR intake, and the
LOINC mapping, which needs the sending lab their seed puts on file — run
against a second pair of tiers over a database of their own, the first one's
name with `_intake` on the end, with the intake and its assignment pass turned
on. The whole run is the full walkthrough regression a release runs: it
performs all thirty-six walkthroughs above, every one of them, with nothing
selected out. It needs a reachable SQL
Server; each walkthrough's own **Before you start** says what it needs to be
performed by hand instead.
