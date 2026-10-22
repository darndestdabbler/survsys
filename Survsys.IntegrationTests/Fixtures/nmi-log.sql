-- A small CDC export history, for the phase 6 walkthroughs that inspect the
-- export log and review a generated message.
--
-- Nothing writes nmi_log yet -- generation and transmission do not persist
-- what they did -- so this fixture writes the history straight into the table,
-- the way staged-messages.sql writes the intake queue, and the export log and
-- generated-message review screens have rows to show.
--
-- One case morbidity report, and two exports of it:
--
--   * the case is An Nguyen's, whom persons.sql seeds, for Influenza, animal
--     isolates, which conditions.sql seeds. Both are found by name rather than
--     by a hard-coded id, because identity assigns the ids. The condition is
--     one no case walkthrough creates a report for, and the case is left
--     unassigned (agency_id NULL), so it sits in no agency's work queue and in
--     no saved view another walkthrough filters by condition or agency.
--   * its record number, 202500048, is the handle a test finds it by. It is a
--     2025 number, so it is never the highest number of the year the API tier
--     issues the next one from, and a report a walkthrough creates is numbered
--     as it would be without this seed.
--   * the first export was sent and accepted, and carries the generated
--     message in the xml column -- an ORU^R01, which is what is sent.
--   * the second is queued and unsent: no message yet, no sent time and no
--     outcome, which is the no-message-yet line the review screen shows.
--
-- The times are UTC, as nmi_log keeps them; the queued export is the newer of
-- the two, so it is the first row of the log, newest first.

INSERT INTO person_conditions
    (person_id, condition_id, record_number, agency_id,
     first_reported_ph_date, event_onset_date, created_at)
SELECT person.id, condition.id, N'202500048', NULL,
       '2025-11-03', '2025-10-30', '2025-11-03T09:12:00-05:00'
FROM persons AS person
CROSS JOIN conditions AS condition
WHERE person.last_name = N'Nguyen' AND person.first_name = N'An'
  AND condition.name = N'Influenza, animal isolates';

INSERT INTO nmi_log
    (person_condition_id, record_number, guide_id, xml, queue_time, sent_at, outcome, detail)
SELECT report.id, report.record_number, NULL,
       N'MSH|^~\&|SURVSYS|CT_DPH|CDC|CDC_NNDSS|20251104080500||ORU^R01^ORU_R01|NMI202500048-1|P|2.5.1
PID|1||202500048^^^SURVSYS||NGUYEN^AN||19920229|M
OBR|1||202500048^SURVSYS|68991-9^Epidemiologic information^LN|||20251103
OBX|1|CWE|78746-5^Country of birth^LN||VNM^Viet Nam^ISO3166_1||||||F',
       '2025-11-04 08:00:00', '2025-11-04 08:05:00', N'accepted',
       N'The CDC endpoint answered 200.'
FROM person_conditions AS report
WHERE report.record_number = N'202500048';

INSERT INTO nmi_log
    (person_condition_id, record_number, guide_id, xml, queue_time, sent_at, outcome, detail)
SELECT report.id, report.record_number, NULL, NULL,
       '2025-11-10 08:00:00', NULL, NULL, NULL
FROM person_conditions AS report
WHERE report.record_number = N'202500048';
