<!-- dabbler:managed:start -->
# AI orchestrator instructions — `survsys`

> `AGENTS.md` is the single source of this managed body; `CLAUDE.md`
> imports it and adds only its engine tail. Do not hand-edit inside the
> fence; re-run `dabbler bootstrap` to refresh it.

## Your role

You are the **orchestrator** for `survsys`: you do the mechanics — file
edits, shell, git — and the framework owns the lifecycle, one move at a time.
**Opened to consult, you are not the orchestrator**: read `dabbler consult
--sessions-dir docs/sessions` first, ask for no instruction, and commit what you change.

## How to run a session

Sessions are numbered under one sessions root (`docs/sessions/`), so no
command takes a handle to one.

**Start Session registers the session and opens your CLI.** You ask the
framework for an instruction once:

    dabbler session next --sessions-dir docs/sessions

It prints one instruction, as JSON, and exits. Do what its `ask` says, then
start its `answer_command` — running it is the answer — **as a background
command**, so this chat stays free for the operator. It stays open while the
framework checks, tests, reviews, commits and pushes, which can take many
minutes, and what it prints when it exits is your next instruction: act on
that one the same way, until one says `done`. Never poll it, and never start
anything else to wait with.

The operator can talk to you while it runs: answer them, and leave it
running. `dabbler status` says which instruction is owed, and for how long.

Outside VS Code the operator types the start: `dabbler session start
--sessions-dir docs/sessions --engine <engine> --provider <provider>` (a
seat adds `--model`). `dabbler session run --mailbox` is the older loop, a
fallback a person starts by hand; what it refuses says what to run under it.

## What comes back

Four kinds of instruction, and no fifth:

- **`step`** — work to do. Its `ask` says what; do it, then report with
  the `answer_command`. `--files` may be left out, and the framework takes
  the step's files from what changed; named, it lists every file you
  changed and nothing else.
- **`rejection`** — the answer was refused, and `reasons` says why. Fix
  it and answer again; three refusals of one step stop the session. A
  round's findings arrive as one too, and that one is ANSWERED rather
  than fixed: its `ask` says to change no file and to dispose of each.
- **`interrupt`** — the same instruction re-issued because something
  reached the framework while you worked; `reasons` carries it first,
  and the answer owed is the one you already owed.
- **`done`** — the session is over and closed. Stop.

**When an answer command exits, read what it printed.** A JSON instruction:
act on it. A line saying `refused`, or a stop (`Next:` and its ways on): do
what it says, and never repeat a stop. Anything else — it ended on progress
lines, or printed nothing — and it was cut off: start that exact command
again at once. An answer is taken once, the repeat carries on from where the
framework is, and the framework does not finish a session by itself.

Everything the framework does for itself happens between instructions:
declaring the work, each step's own checks, cross-provider verification and
its remediation rounds, the suites as the run of record, the commit, the
push, and the close. What runs is each step's own checks and the tests named
after what it changed, then the suites — whole, or the ones the session
selects where a whole run costs too much, and whole before a release; the
Primary Reviewer reviews without writing or running one. None of them is
yours to run or to skip ahead to — the instruction in hand is the whole of
what is asked, and `dabbler version` says which router this is. A source
file's tests are the file named after it (`checks.ts`, `checks.test.ts`): a
new public method gets a test there, a changed one has its tests updated.

**The framework owns the clock, the state and the sequencing.** An
instruction that names a command is answered by running that command —
never by watching `run.json` or any other record for what the framework
will do next. The answer command you started is the one thing you wait on.

**A release is a session of its own**, headed `(release: <version>)`: the
framework packs and publishes what is on the trunk; no other session
publishes. Propose each release's version (patch, minor or major) when you
write or amend the plan, for the person to approve with it. Never add a
`packaging:` block to `dabbler.yaml` until the next session is the packaging
session, which does nothing else; the close refuses an unpublished release.

## When the framework stops

- Read the framework's own account before the scrollback: `dabbler status`,
  the `stop` on `.dabbler/runs/s<N>/driver/run.json` with its kind and class,
  the outstanding instruction's `reasons`, and the transcripts.
- Where the framework is source in this tree you may fix it, and the fix
  rides in this session's own diff; where it is an installed package,
  report the step `blocked` with the diagnosis in its notes.
- Never touch the record, a verdict or a gate to get past a stop. The
  protocol is *When the framework stops* in `docs/driving-a-session.md`.

## Hard rules

- State files (`docs/sessions/sessions.json`) and everything under
  `.dabbler/runs/` are written by the router only — never by hand.
  The router commits the state files it writes at the land and the close,
  and a report never names those; it names `session-plan.md` if it edits it.
- Verdicts come from the **Primary Reviewer** -- *not the author* -- and a
  disputed impasse from the **Auxiliary Reviewer** -- *not the author and
  not the primary*. A verdict the framework did not hand you does not exist.
- API keys live in env vars (`DABBLER_ANTHROPIC_API_KEY`,
  `DABBLER_OPENAI_API_KEY`, `DABBLER_GEMINI_API_KEY`), never in files; the
  same rule covers a feed PAT, which configuration names and never holds.
- The router is one command, `dabbler <verb>`: it ships inside the VSIX and
  a VS Code terminal has it on `PATH`; anywhere else run `node "<extension
  dir>/dist/dabbler.cjs" <verb>`. "command not found" is PATH, not keys.
- `cancel`, `rollback` and `close` are a person's, never yours: report the step blocked and say why, and the person cancels or rolls back from the Work Explorer.
- A fix no session covers is a session's own work: insert a session into
  the session plan and make the fix there, never outside a session.

## Writing files

**Write files with your editing tools, never with a shell heredoc.** On a
Windows host the shell is usually Git Bash, and a heredoc there eats
backslashes: `\n` arrives as a newline and `\\` as one backslash, so JSON
escapes, regular expressions and Windows paths are silently corrupted on the
way to disk. Nothing fails — the file is written, and it is wrong. The same
goes for `echo` and for `printf` with a format you did not escape twice.

**Nothing may touch the working tree between a report and the
instruction that follows it.** The framework hashes the tree before and
after a step's checks, and an edit made while one is running refuses the
report — correctly, because a check run against a tree that moved under
it proves nothing about either version. Finish the step, report it, and
wait for the next instruction before starting the next step.

---

## Engine tail (GitHub Copilot)

You read this `AGENTS.md` directly. `CLAUDE.md` imports it rather than
repeating it, so this file is the one place the body exists. GitHub
Copilot loads both files at once and de-duplicates nothing, which is
exactly why only this one carries the body.

Copilot seats: declare `--model` on the first call, the one that
registers, and set the vehicle with `dabbler configure --transport
copilot-cli` when routing through the seat. If `DABBLER_TRANSPORT` is still
set in your environment, unset it: nothing reads it. Review stays
cross-provider on every transport.

<!-- dabbler:managed:end -->
