# HL7 CDA artifacts

The files under this folder are kept exactly as their publishers released them.
Do not edit them. To update one, replace it with the newer published file. The
validator and the stylesheet read them from the build output, where their
relative `include`s and `document()` calls resolve against each other.

| Folder | Files | Source (GitHub, fetched 2026-10-22) |
| --- | --- | --- |
| `Schema/` | CDA R2 schema with the SDTC extensions, `infrastructure/cda` and `processable/coreschemas` laid out as published | `HL7/CDA-core-2.0` `schema/extensions/SDTC` @ e922fc35586f |
| `Schematron/` | `CDAR2_IG_PHCASERPT_R2_STU1.1_SCHEMATRON.sch`, `voc.xml` (eICR R1.1) | `HL7/CDA-phcaserpt-1.1.1` `validation` @ 95c72d275e12 |
| `Schematron/` | `CDAR2_IG_PHCASERPT_R2_D3_SCHEMATRON.sch` (eICR R3.1) | `HL7/CDA-phcaserpt-1.3.0` `validation` @ ca898f29d68f |
| `Schematron/` | `CDAR2_IG_PHCASERPT_R2_STU3.1.1_SCHEMATRON.sch`, `CDAR2_IG_PHCASERPT_R2_STU3.1.1_VOCABULARY.xml` (eICR R3.1.1) | `HL7/CDA-phcaserpt` `CDA-phcaserpt-3.1.1/validation` @ 28e32a7bb258 |
| `Schematron/` | `CDAR2_IG_PHCR_R2_RR_D1_2017DEC_SCHEMATRON.sch`, `CDAR2_IG_PHCR_R2_RR_D1_2017DEC_VOC.xml` (RR 1.1) | `HL7/CDA-phcr-rr-1.1.0` `validation` @ 0d4129871e53 |
| `Iso/` | ISO Schematron XSLT 1.0 implementation (`iso_dsdl_include.xsl`, `iso_abstract_expand.xsl`, `iso_svrl_for_xslt1.xsl`, `iso_schematron_skeleton_for_xslt1.xsl`), MIT licence | `schematron/schematron` `trunk/schematron/code` @ 77dcd36c53d1 |
| `Stylesheet/` | HL7 CDA stylesheet `CDA.xsl` (XSLT 1.0) with `cda_l10n.xml` and `cda_narrativeblock.xml` | `HL7/CDA-core-xsl` @ 951237ce508e |

## R3.1's vocabulary

The eICR R3.1 schematron loads `CDAR2_IG_PHCASERPT_R2_D3_VOCABULARY.xml`. That
file is 58 MB, so it is not kept here. The R3.1 schematron looks up nine value
sets in it, and all nine are also in the R3.1.1 vocabulary. The validator
therefore gives the R3.1 schematron the R3.1.1 file under the R3.1 file's name.

## XSLT 2.0

The schematrons declare `queryBinding="xslt2"`. Their tests are XPath 1.0
except for two `matches()` calls in R3.1.1. The validator therefore compiles
them with the XSLT 1.0 skeleton: it reads the query binding as `xslt1` and binds
`matches()` to a .NET regular expression. Both changes are made in memory, and
the files here are not touched.
