-- What the eCR walkthroughs start from: the condition mapping that makes their
-- RRs' conditions reportable, and the person and open case one of them links to.
--
-- Seeded by the intake harness only, after elr-intake.sql, whose zip-to-agency
-- row puts every patient below -- all at 84101 -- under Salt Lake County. The
-- documents are the files beside this one, under Cda/, which each walkthrough
-- copies into the eCR drop directory, and each RR names the responsible agency
-- the harness configures as this jurisdiction:
--
--  * ecr-review-eicr.xml and ecr-review-rr.xml -- Malaria for Ingrid Solberg,
--    who is not on file.
--  * ecr-map-rr.xml -- Bartonellosis, an RR alone, its code mapped to nothing,
--    so its condition waits for review until the walkthrough maps it.
--  * ecr-open-eicr.xml and ecr-open-rr.xml -- Hepatitis A for Tomas Lindqvist,
--    who is not on file, so a person and a case are opened for him.
--  * ecr-link-eicr.xml and ecr-link-rr.xml -- Giardiasis for Amara Osei, who
--    has an open Giardiasis case seeded below. The eICR is linked to it.
--
-- The codes are SNOMED CT condition codes, as an RR carries them. Amara Osei's
-- name, birth date, sex and address are the ones her eICR carries, so the
-- person match scores her exact.

INSERT INTO ecr_condition_mapping (code_system, code, display_name, condition_id)
SELECT N'2.16.840.1.113883.6.96', mapping.code, mapping.display_name, condition.id
FROM (VALUES
        (N'61462000', N'Malaria', N'Malaria'),
        (N'40468003', N'Hepatitis A', N'Hepatitis A'),
        (N'58265007', N'Giardiasis', N'Giardiasis'))
     AS mapping (code, display_name, condition)
JOIN conditions AS condition ON condition.name = mapping.condition;

INSERT INTO persons
    (last_name, first_name, middle_name, birth_date, days_old_no_birthday,
     birth_gender, current_gender, ethnicity, language, country_of_birth)
VALUES
    (N'Osei', N'Amara', NULL, '1992-08-03', NULL, N'F', N'F', N'NH', N'en', N'USA');

INSERT INTO person_addresses
    (person_id, street, unit_number, city, state, postal_code, county,
     beginning, ending, note, residence_type_id)
SELECT person.id, N'61 Liberty Avenue', NULL, N'Salt Lake City', N'UT', N'84101', N'SL',
       NULL, NULL, NULL, NULL
FROM persons AS person
WHERE person.last_name = N'Osei' AND person.first_name = N'Amara';

-- Amara Osei's open Giardiasis case, which ecr-link-eicr.xml is linked to.
INSERT INTO person_conditions
    (person_id, condition_id, record_number, agency_id,
     first_reported_ph_date, event_onset_date, created_at)
SELECT person.id, condition.id, N'202600902', 5,
       '2026-10-01', '2026-10-01', '2026-10-01T09:00:00-06:00'
FROM persons AS person
CROSS JOIN conditions AS condition
WHERE person.last_name = N'Osei' AND person.first_name = N'Amara'
  AND condition.name = N'Giardiasis';
