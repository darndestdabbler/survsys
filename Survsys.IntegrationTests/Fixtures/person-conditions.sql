-- One case morbidity report, for the laboratory round-trip to record a
-- specimen against.
--
-- The report echoes the one
--
--   ../nedss/docs/db-snapshots/session-011/02-after-lab-save.txt
--
-- dumps: record number 202600004, first reported to public health on the 12th
-- of March, event onset on the 8th, created on the 13th. That snapshot is also
-- where the lab values the round-trip posts come from -- accession
-- LAB-2026-011, collected on the 8th of March, from a specimen source, with one
-- test and one result -- so the test's values and this report are the one
-- legacy case read across.
--
-- The report is written against a person and a condition the other two
-- fixtures already seed rather than against the snapshot's own ids, which this
-- schema does not use: Maria Rodriguez, whom persons.sql seeds, and Malaria,
-- which conditions.sql seeds as active. The report finds them by name rather
-- than by a hard-coded id, because identity assigns the ids.
--
-- Two translations from the legacy types, both the schema's: the two dates are
-- dates where the legacy columns are timestamps at midnight, and created_at
-- carries its offset, as the column is a datetimeoffset.
--
-- The laboratory round-trip clears any specimens it records before and does
-- not delete this report, so a report with no specimens against it is what
-- this fixture leaves and what the next run finds.

INSERT INTO person_conditions
    (person_id, condition_id, record_number, agency_id,
     first_reported_ph_date, event_onset_date, created_at)
SELECT person.id, condition.id, N'202600004', NULL,
       '2026-03-12', '2026-03-08', '2026-03-13T00:19:03-06:00'
FROM persons AS person
CROSS JOIN conditions AS condition
WHERE person.last_name = N'Rodriguez' AND person.first_name = N'Maria'
  AND condition.name = N'Malaria';
