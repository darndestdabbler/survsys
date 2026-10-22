# Brief — `survsys`

> The operator's answer, given when session 1 asked for it. This file is the
> brief; `solution-plan.md` is what was made of it. Where the two disagree,
> this file is what was asked for.

---

## Purpose

`survsys` is a port of the three legacy disease-surveillance systems that sit
side by side under `../nedss`, into **one** application:

| Legacy system | Path | What it is |
|---|---|---|
| **EpiTrax** | `../nedss/legacy/` | Java EE case management — Wildfly, JSF, EJB, Hibernate |
| **EMSA** | `../nedss/legacy-EMSA/` | PHP inbound ELR message staging — rules engine, vocabulary, Mirth integration |
| **NMI** | `../nedss/legacy-NMI/` | PHP outbound CDC notification — rulesets, XML generation, Mirth integration |

The three systems' **functionality** is the target; their architecture is not.
They are treated as a *living specification* — read, mined and used as a
behavioural reference — and never translated line by line. No legacy source is
modified: all three are read-only reference material.

## Who uses it

Public-health staff doing disease surveillance: the people who create and
investigate case morbidity reports (CMRs), enter and review laboratory results,
work case queues across agencies and jurisdictions, review staged inbound
electronic laboratory reports, and send notifications onward to CDC.

During the porting work itself there is one further user: **the human
operator**, whose part is deliberately small — see *minimal operator input*
below. Their job is to perform the walkthroughs once those have been thoroughly
tested.

## What it must do

Each of these is the operator's own requirement.

1. **Stack.** MudBlazor on **.NET 10**, with **Entity Framework Core** against
   **SQL Server**.
2. **An API service layer that mediates all communication with the database.**
   Nothing else talks to SQL Server.
3. **Do not over-engineer.** Keep it as simple as possible. This is a stated
   constraint on the design, not a preference — simplicity and clarity are part
   of how the work is measured.
4. **Three kinds of test:** unit, integration, and end-to-end. The end-to-end
   tests run via **Playwright**.
5. **Build incrementally**, so that after some initial sessions users will be
   able to **see and interact with the database**. Visible, interactive value
   early rather than a long invisible build-out.
6. **Walkthrough documentation at every release** — step-by-step instructions
   for the user, organised around **user stories that demonstrate application
   capabilities and features**. The user stories are **inferred from the legacy
   code and documentation**.
7. **Every walkthrough is tested by the end-to-end tests**, to ensure it can be
   completed. The operator's words: this is *extremely important*, and it is
   **unacceptable to have a walkthrough that the user cannot complete exactly as
   it is written**.
8. **Screen recordings** from the end-to-end testing, showing **user clicks and
   text input**, to support the walkthroughs. **OBS Studio** is installed on the
   host if that is helpful.
9. **User and technical documentation built as the work goes along.** The
   operator expects this to take some time to build, and that is accepted.
10. **Minimal required input from the human operator** during the porting work.
11. The **`Dabbler.*` packages** published at
    <https://www.nuget.org/packages?q=Dabbler&includeComputedFrameworks=true&prerel=true>
    are available to use **if they are helpful**. Offered, not mandated.

## Out of scope

- **No legacy source is modified**, moved or copied. EpiTrax, EMSA and NMI are
  read-only.
- **Mirth Connect is dropped** as a dependency rather than carried over; its
  functions are absorbed into the application.
- **The NMI export queue to Mirth** is replaced by direct transmission.
- **The NETSS legacy fixed-width export** is to be confirmed with CDC as still
  required before it is dropped — it is not assumed either way.
- Legacy architecture: Java EE patterns (EJB, JNDI, JAAS, EAR packaging) and PHP
  framework patterns are not carried over, and legacy
  package/namespace structure is not mirrored.

## Success looks like

A released application whose **walkthroughs the operator can complete unaided,
exactly as written** — every step performing as documented, with nothing
assumed, missing or out of order.

That is the operator's own measure, and it is the one that decides whether a
release is good: a walkthrough that cannot be completed as written is a failure
of the release, not of the reader.

## Existing plans and notes

The legacy repository has already been explored and documented at length. That
material is the specification to mine, and it is why the user stories can be
inferred rather than invented:

| Where | What is there |
|---|---|
| `../nedss/docs/functional/` | Ten functional documents, including `phase-1.6-feature-matrix.md`, which inventories **92 features** across 14 categories as **85 port, 4 simplify, 3 drop** |
| `../nedss/docs/business-logic/` | Twelve documents: classification rules, CT site requirements, the EMSA↔EpiTrax contract, the EMSA message pipeline, person matching, the rules engine, the vocabulary system, HL7 field mappings, the Mirth replacement spec, and the NMI export pipeline, rulesets and XML generation |
| `../nedss/docs/data-model/epitrax-table-catalog.md` | The legacy table catalogue |
| `../nedss/docs/db-snapshots/` | Before/after table dumps from legacy UI operations — usable as integration-test fixtures |
| `../nedss/docs/screenshots/` | Captured legacy screen states, by sub-session |
| `../nedss/tests/` | Playwright specs that drove the legacy exploration |

`../nedss/PROJECT_PLAN.md` and `../nedss/CLAUDE.md` are **that** repository's own
port plan. They are read as reference — they record decisions already reasoned
through — but they are **not adopted as this repository's plan**. This
repository's plan is `solution-plan.md`, made from this brief.

## Stack

| Concern | Choice |
|---|---|
| UI | **MudBlazor** |
| Platform | **.NET 10** |
| API | **ASP.NET Core** — the only tier that reaches the database |
| Data access | **Entity Framework Core** |
| Database | **SQL Server** |
| Unit & integration tests | **xUnit** |
| End-to-end tests | **Playwright**, with video recording |
| HL7 v2 | **nHapi** — parsing and generation, not hand-rolled |
| Screen recording | Playwright video; **OBS Studio** available on the host |
| Optional components | `Dabbler.*` packages, where they remove code |

**SDK pinning.** This host has **SDK 10.0.201** and **SDK 11.0.100-preview.2**
both installed, and the preview is the newest. The skeleton session must
therefore pin **.NET 10** in a `global.json` at the repository root, or builds
will silently drift onto the preview SDK.
