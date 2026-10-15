# Session plan

> **Purpose:** the numbered sessions this repository runs, in order. The
> first two set the project up; everything after them is the work.
> **Workflow:** Full

---

## Sessions

### Session 1: Author or import the solution plan

1. Register.
2. The brief is the prompt this session was started with, or
   `docs/planning/brief.md`: what the solution is — its purpose, who uses
   it, what it must do and what is out of scope, what success looks like,
   and whether a plan or notes already exist. With neither, ask the person
   who started the session for it and write their answer to that path
   before any work. The plan's substance is theirs: do not search
   neighbouring directories for one, and do not draft one from the folder
   name. Before any module is proposed, ask them how production is split:
   what runs separately in production, and which part may talk to the
   database? Offer the default -- an application tier that calls an API
   tier, and only the API tier talks to the database. Then create — or
   import — `docs/planning/solution-plan.md`: the
   objective a reader can act on; the *Production split* they answered;
   the *Handoff artifacts*, what each separately running part is handed
   over as, with the standard command for its form -- for .NET an IIS site
   package (`dotnet publish`) or a Windows Service (a Worker Service
   published for a service host), for Java a runnable jar or a war (`mvn
   package` writing into `{output}`, which `packaging.pack` requires --
   say through a property the POM's jar or war plugin takes as its
   output directory, `-Dhandoff.dir={output}`) or an image (Spring
   Boot's `build-image`), for the database a
   SQL migration script (`dotnet ef migrations script --idempotent`); the
   real form is produced later, and a tutorial produces the easiest one to
   test; the modules, with what each is
   responsible for, the contract each exposes (what must be true going
   in, what is guaranteed coming out, how it fails), the dependency
   direction, the reason for each cut and the cuts deferred — one module
   is a fine answer, and most small solutions are one; and the phases or
   feature areas with their key deliverables, each scoped to a handful of
   focused AI sessions. A repository that already has
   `docs/planning/project-plan.md` amends that file instead.
3. Where the plan has several modules, say in it how each is a project:
   a `.csproj` listed in the root solution file for .NET, or a Maven module
   listed in the parent `pom.xml`, each reaching a sibling by project
   reference -- a `<ProjectReference>`, or a dependency at
   `${project.version}`. The first numbered session, the skeleton, writes
   the projects.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

This session writes the plan and nothing else: it creates, edits or
deletes no code, project, build file, test, `dabbler.yaml` suite or
packaging.

**Creates:** `docs/planning/solution-plan.md`. A later revision is just
another plan session that amends the same file.

### Session 2: Challenge the plan, then break it into numbered sessions

1. Register.
2. Read `docs/planning/solution-plan.md` (or `docs/planning/project-plan.md`
   where that is the file this repository keeps its plan in) and challenge
   its cuts: for each
   module, whether it should exist, whether its contract is a promise
   something can prove, and whether the dependency direction hides the
   decisions most likely to change; decide each deferred cut or say why it
   stays deferred. Record the answers in the plan's rationale.
3. Break the plan into numbered sessions appended to this file. Each
   session is a focused unit of
   work one AI coding session can complete: one
   `### Session <N>: <title>` heading, and its steps as a top-level
   ordered list. Step 1 registers the session; the last steps are
   cross-provider verification, the complete suite once against the
   verified tree, and close-out; the middle steps are the work. Never
   write a step that says "run the tests" without saying which run it
   means. Order sessions so earlier ones unblock later ones — a module
   before the modules that depend on it — and keep at most ~3 work steps
   per session.

   The first numbered session is the skeleton. For .NET it writes the
   solution file and every project the plan names, with their
   references; for Maven or Gradle, the parent build and every module.
   Either way it declares the test suite in `dabbler.yaml` under
   `testing.suites`, even before any tests exist, with `expensive: true`
   where its run is the run of record and a hang limit in its command --
   `dotnet test --blame-hang-timeout 5m`, or Surefire's
   `forkedProcessTimeoutInSeconds` -- so every later run inherits it,
   and adds the architecture test the plan requires -- where only the
   API tier may reach the database, that rule. Later sessions fill the
   modules in and never re-create what the skeleton made.

   Packaging and releases are sessions of their own. Where the plan
   names *Handoff artifacts*, plan one packaging session -- early, with
   project files only, or later -- that writes the `packaging:` block in
   `dabbler.yaml` and does nothing else. A release is a session with no
   steps, headed `### Session <N>: Release <version> (release:
   <version>)`; propose its version -- patch, minor or major by what the
   sessions it releases change -- for the person to approve with the plan.
4. Cross-provider verification.
5. Full test suite, recorded as the run of record.
6. Close-out.

This session writes the plan and nothing else: it creates, edits or
deletes no code, project, build file, test, `dabbler.yaml` suite or
packaging.

**Creates:** the numbered session list the rest of this repository runs,
starting with the skeleton session.

> Do NOT hand-author `sessions.json`. The first `session start`
> bootstraps it from this plan — state files are the writers' job.
