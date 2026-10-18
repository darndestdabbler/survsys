# Electronic case reporting (eCR) — what survsys needs

> Researched on 2026-10-18 from CDC, APHL AIMS, HL7, ONC and CMS sources,
> which are cited inline. It is the specification for phase 13 (sessions 93 to
> 98 in `docs/sessions/session-plan.md`). Claims marked **[confirm]** could not
> be confirmed from a current public source and are to be confirmed with the
> APHL eCR service desk before the session that depends on them.

## Where survsys stands

**No eCR support.** Nothing in the solution parses CDA, eICR or RR, and
`StagedMessage` has no message-type column. Legacy EMSA handled eICRs as
`CLINICAL_DOCUMENT` messages (`legacy-EMSA/.../Constants/MessageType.php`):
Mirth turned each into EMSA's master XML with a per-sender XSLT
(`elr.structure_xslt`), a diagnosis code drove the case rules where there was no
LOINC, and `EmsaMessageXmlUtils::clinicalDocumentToNotes` put the clinical
content into the case's notes; lab data was kept only for senders flagged
"eCRs update lab data". **Legacy EMSA never read the Reportability Response.**
Connecticut's requirements are in `../nedss/docs/business-logic/CT_SITE_REQUIREMENTS.md` §2,
and its specifications are in `../nedss/docs/specs/site-specs/CT DPH eCR *.pdf`.

## How eCR flows

1. **Trigger.** The EHR matches encounter data against the Reportable
   Conditions Trigger Codes (RCTC), distributed in the eRSD.
   <https://ecr.aimsplatform.org/public-health-agencies/understanding-ecr-standards/>
2. **eICR to AIMS.** The EHR, or an intermediary (eCR Now, an HIE, TEFCA),
   sends an electronic Initial Case Report to APHL AIMS over Direct or IHE XDR.
   eCR was TEFCA's first live public-health use case.
   <https://ecr.aimsplatform.org/hie-health-data/supporting-technical-transactions>
3. **RCKMS decides.** AIMS evaluates the eICR against each jurisdiction's
   RCKMS-authored rules for reportability and routing.
4. **Reportability Response.** AIMS and RCKMS make one RR per eICR, listing
   each condition, its determination, the responsible agency and any
   instructions. **The PHA receives the RR; it neither makes nor acknowledges
   it.** <https://ecr.aimsplatform.org/public-health-agencies/onboarding-and-implementation/>
5. **Delivery.** The PHA receives the eICR and its RR as two CDA R2 XML
   documents, on a test channel (no PII) and a production channel. AIMS can
   optionally pass them through the CDC DIBBs eCR Refiner first.
   <https://www.cdc.gov/data-modernization/php/technologies/about-dibbs.html>
   **The AIMS-to-PHA transport is not documented publicly [confirm]:** it
   has been SFTP, PHINMS or cloud-bucket pickup. survsys's intake is
   therefore a pluggable folder or bucket poller.

## Standards to handle

| Artifact | Versions | Source |
|---|---|---|
| eICR, CDA | R1.1 and R3.1 / R3.1.1, both live; Edition 4 in ballot [confirm] | <https://ecr.aimsplatform.org/public-health-agencies/understanding-ecr-standards/> |
| RR, CDA | R1 STU 1.1 | <https://www.hl7.org/implement/standards/product_brief.cfm?product_id=470> |
| FHIR eCR | 2.1.0 in regulation, 2.1.2 current; not known to be delivered to PHAs in production [confirm] | <https://hl7.org/fhir/us/ecr/> |
| RCTC | through APHL's Terminology Exchange Service | <https://tes.tools.aimsplatform.org/> |
| RR determination | RRVS1 reportable, RRVS2 may be reportable, RRVS3 not reportable, RRVS4 no rule met | <https://hl7.org/fhir/us/ecr/2018Sep/ValueSet-determination-of-reportability-ecr.xml.html> |

ONC §170.315(f)(5) asks certified EHRs for eICR R3.1 or FHIR 2.1.0 from
2026-01-01, but under enforcement discretion, so R1.1 keeps arriving.
<https://healthit.gov/test-method/transmission-public-health-agencies-electronic-case-reporting>

## What survsys must do, by session

| # | Capability | Session |
|---|---|---|
| 1 | Pick up eICR and RR documents from a configured folder or bucket, test and production feeds kept apart; store the raw XML unchanged; take each document once, keyed on its id | 93 |
| 2 | Parse eICR R1.1 and R3.1/3.1.1 and RR 1.1 by templateId, in `Survsys.Messaging` with no EF Core; pair each RR with its eICR; group an eICR's versions by `setId` and `versionNumber` | 93 |
| 3 | Validate against the CDA schema and the HL7 schematron; record the result without blocking intake. If the schematrons compile only to XSLT 2.0, which .NET's `XslCompiledTransform` cannot run, the session records that and validates by schema alone [confirm] | 94 |
| 4 | Show the eICR to a reviewer through the HL7 CDA stylesheet (XSLT 1.0, <https://github.com/HL7/cda-core-2.0>), alongside the RR's determinations | 94 |
| 5 | Read each RR condition's determination, responsible agency and timeframe; keep the conditions this jurisdiction is responsible for; send RRVS2 and unmapped conditions to review | 95 |
| 6 | Map RCTC and SNOMED condition codes to the conditions catalogue, maintained on a screen and importable from an RCTC release | 95 |
| 7 | Match the patient (phase 9's person matching), link to an existing case on the same person and condition (including one opened from ELR), or open a new one | 96 |
| 8 | Write demographics, encounter, problems, labs (per the sender's "update lab data" setting, as legacy EMSA had it), medications as treatments, pregnancy, travel and occupation; record which eICR id and version each value came from | 96 |
| 9 | Keep sender facilities with their onboarding stage (test, validation, production) and per-sender settings | 97 |
| 10 | Report data quality per sender and condition: volumes, validation failures, receipt latency, an RR with no eICR or the reverse | 97 |

## Reusable pieces

- HL7 CDA stylesheet and schema: <https://github.com/HL7/cda-core-2.0>
- eICR IG artifacts (examples, schematron): <https://github.com/HL7/CDA-phcaserpt-1.1.1>
- RR IG artifacts: <https://github.com/HL7/CDA-phcr-rr-1.1.0>
- CDC DIBBs eCR Viewer (containers; supports SQL Server metadata): <https://github.com/CDCgov/dibbs-ecr-viewer>, which a deployment may run beside survsys in place of capability 4.
- CDC DIBBs eCR Refiner and Record Linker: <https://github.com/CDCgov/dibbs-ecr-refiner>, <https://github.com/CDCgov/RecordLinker>
- **No open-source .NET eICR parser was found**, so session 93 writes a LINQ-to-XML model keyed on templateIds.

## Why it matters

- CDC's prerequisites for a PHA include processing eCR into its surveillance
  system. <https://www.cdc.gov/ecr/php/getting-started/index.html>
- CDC's Public Health Data Strategy milestone 1.03 for 2026: 70% of
  ELC-funded health departments on AIMS process eCR into **production**
  surveillance systems. <https://cdc.gov/public-health-data-strategy/php/about/phds-milestones.html>
- The CMS Promoting Interoperability eCR measure lets clinicians claim an
  exclusion where no PHA has declared readiness, so a PHA's readiness
  declaration matters to its providers.
  <https://qpp.cms.gov/docs/pi_specifications/Measure%20Specifications/2026-MIPS-Promoting-Interoperability-Electronic-Case-Reporting-Measure.pdf>

## Not in scope

- RCKMS authoring, which the PHA does in RCKMS.
- FHIR eICR and RR bundles, until AIMS delivers them in production.
- Connecticut's MIF XML export to Maven, its 12-hour batching and its HTML
  scrubbing, and Verato UMPI (see `gap-analysis.md`).
