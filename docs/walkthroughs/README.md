# Walkthroughs

One document per user story, written as numbered steps you perform against a
running Survsys. Perform them in any order; each one stands alone.

| Walkthrough | The user story | Performed by |
|---|---|---|
| [Review the conditions catalogue](review-the-conditions-catalogue.md) | A surveillance administrator opens the conditions catalogue to see which conditions may be reported, and which of them carry a CDC code. | `Survsys.E2ETests/ReviewTheConditionsCatalogueTests.cs` |

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
