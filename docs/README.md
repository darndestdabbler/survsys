# Survsys documentation

A map of what is under `docs/`, who each part is for, and which part you want
first.

| Folder | Who it is for | What it holds |
|---|---|---|
| [`walkthroughs/`](walkthroughs/README.md) | **the operator, and anyone who wants to see Survsys work** | One document per user story, written as numbered steps you perform against a running Survsys. **Start here.** Every walkthrough is performed by a test of the same name, so a walkthrough that is in this folder is one the application can complete. |
| [`user-guide/`](user-guide/README.md) | people using Survsys | What each screen is for and how the work is done on it, section by section, for every screen Survsys 1.0.0 has. |
| [`technical/`](technical/README.md) | people building, running or deploying Survsys | How the tiers fit together, how to run it on a developer machine, what the test suites are, and how it is deployed. |
| [`planning/`](planning/) | the operator and the AI sessions | `brief.md`, the operator's requirements, and `solution-plan.md`, the agreed architecture and the phase-by-phase plan that delivers it. These are the agreement the work is held to, not documentation of the application. |
| `sessions/` | the framework | `session-plan.md`, `change-log.md` and the session state the router writes. Nothing here is hand-edited. |

## Where to start

- **To see what Survsys does today**, read
  [`walkthroughs/README.md`](walkthroughs/README.md) and perform the
  walkthrough it indexes.
- **To run Survsys on your own machine**, read
  [`technical/README.md`](technical/README.md).
- **To know what Survsys is going to be**, read
  [`planning/solution-plan.md`](planning/solution-plan.md).

## How this documentation is built

It was written as the work went along, one phase at a time, which the brief
asks for, and at 1.0.0 the user guide and the technical guide are complete:
every section each declared is written, and each names the phase that built
what it describes. **A section describes only what the application does** —
where something is built but not yet wired into the running application, the
guide says so rather than promising it.
