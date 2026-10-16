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
every walkthrough in a browser and records each one. It needs a reachable SQL
Server; each walkthrough's own **Before you start** says what it needs to be
performed by hand instead.
