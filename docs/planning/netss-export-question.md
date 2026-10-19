# NETSS

NETSS — the National Electronic Telecommunications System for Surveillance — is
CDC's older, fixed-width text format for reporting notifiable-condition case
counts. In the legacy NMI pipeline it sat beside the HL7 notification path: a
case was both generated as an HL7 message and, for some conditions, written out
as a NETSS fixed-width record whose columns were defined by the legacy
`nmi.netss_export_column` tables and whose values were mapped per column from
EpiTrax fields. It is a second, parallel wire to CDC, not a part of the HL7 one.

This document puts the open question about NETSS to the operator, records the
standing default while it is unanswered, and says how it gets settled.

## The question

**Does CDC still require the legacy NETSS fixed-width export from this
jurisdiction, now that direct `ORU^R01` transmission replaces the Mirth export
queue?**

The surveillance system this project replaces sent CDC two things: HL7 `ORU^R01`
notifications and, for some conditions, NETSS fixed-width records. This project
has now built the HL7 half directly — generation with nHapi and direct
transmission in place of the Mirth export queue — so the HL7 wire no longer
depends on any legacy infrastructure. What remains undecided is whether the
NETSS fixed-width wire is still needed at all.

The brief (`docs/planning/brief.md`) lists the NETSS legacy fixed-width export
under "Out of scope" as something "to be confirmed with CDC as still required
before it is dropped — it is not assumed either way," and the Phase 6 section of
`docs/planning/solution-plan.md` repeats that the question is left open
deliberately. It was left open so that it would be settled in this phase —
Phase 6, the outbound CDC notification phase — rather than guessed at earlier.
This is that phase, so this is where the answer belongs.

Answering it needs CDC's input, not the project's: only CDC can say whether it
still ingests NETSS fixed-width records from this jurisdiction or whether the
HL7 `ORU^R01` notifications now satisfy the reporting obligation on their own.

## Recorded default

Until the operator records CDC's answer here, the NETSS fixed-width export is
**not built**. Nothing in the system produces a NETSS fixed-width record: there
is no fixed-width writer, no NETSS export-column mapping, and no NETSS
verification-record generation. The reconciliation cutoff rule and the CDC
export log this session adds serve the HL7 notification path only.

This default is deliberate and safe to hold: building an export CDC no longer
accepts would be wasted work, whereas the HL7 notification path already carries
the case data to CDC. The cost of the default is only that, if CDC does still
require NETSS, that export has not yet been written — and that is a known gap
recorded here rather than a silent omission.

## How this gets settled

The operator confirms with CDC whether the NETSS fixed-width export is still
required from this jurisdiction, and records the answer in this document.

- **If CDC no longer requires it**, the default stands: NETSS is dropped, and no
  further work is done. This document becomes the record of why the system does
  not produce a NETSS export.
- **If CDC still requires it**, a later session builds the NETSS fixed-width
  export — its column definitions, its per-column field mapping and value
  translation, and the verification records reconciliation generates from it —
  as its own planned work, inserted into the session plan at that point.

Either way, the export is built only on a recorded "still required" answer from
CDC. Until then, the default above holds.
