-- What the intake walkthroughs start from: a sending facility on file, the
-- vocabulary and rules that translate its three messages, an agency for the
-- patients' zip, and the two people those messages name.
--
-- Seeded by the intake harness only, after conditions.sql and persons.sql,
-- into a database of its own: that harness runs the assignment pass, which
-- the main harness leaves off. The three messages are the files beside this
-- one, under Hl7/, which each walkthrough copies into the drop directory:
--
--  * elr-assign-example.hl7 -- Hepatitis A for a patient not on file. Its
--    rule opens a case, so the pass opens one for a new person.
--  * elr-update-example.hl7 -- Giardiasis for Leila Haddad, who has an open
--    Giardiasis case seeded below. The message updates that case.
--  * elr-graylist-example.hl7 -- Lyme disease for Owen Brennan, who has no
--    case. Its rule opens none, so the message waits on the graylist.
--
-- Each person's name, birth date, sex and address are the ones their message
-- carries, so the person match scores them exact. The LOINC codes are the
-- messages' own; this fixture does not vouch for them against the LOINC
-- release, because reference data is an administrator's to correct.

INSERT INTO facilities (name, short_name, agency_id, street, city, state, zip, phone)
VALUES (N'ARUP Laboratories', N'ARUP_LAB', 5, N'500 Chipeta Way', N'Salt Lake City', N'UT', N'84108', NULL);

INSERT INTO zip_code_agencies (zip_code, agency_id)
VALUES (N'84101', 5);

INSERT INTO vocab_master_loinc (loinc, concept_name, condition_id, organism_id, organism_from_result)
SELECT loinc.code, loinc.concept, condition.id, NULL, 0
FROM (VALUES
        (N'22314-9', N'Hepatitis A virus IgM Ab [Presence] in Serum', N'Hepatitis A'),
        (N'6463-4', N'Giardia lamblia Ag [Presence] in Stool', N'Giardiasis'),
        (N'5061-8', N'Borrelia burgdorferi IgM Ab [Presence] in Serum', N'Lyme disease'))
     AS loinc (code, concept, condition)
JOIN conditions AS condition ON condition.name = loinc.condition;

INSERT INTO vocab_master_vocab (category, concept, app_value)
VALUES (N'test_result', N'Detected', N'Positive');

-- The lab's SNOMED code for Detected (OBX-5.1), translated to Positive.
INSERT INTO vocab_child_vocab (lab_id, category, child_code, master_id)
SELECT facility.id, N'test_result', N'260373001', master.id
FROM facilities AS facility
CROSS JOIN vocab_master_vocab AS master
WHERE facility.short_name = N'ARUP_LAB'
  AND master.category = N'test_result' AND master.concept = N'Detected';

-- Each condition accepts every message (a filter rule with no conditions),
-- and decides a positive result by one case-management rule: Hepatitis A and
-- Giardiasis open or update a case, Lyme disease updates one but opens none.
INSERT INTO vocab_rules (condition_id, kind, execution_order, allow_new_cmr, allow_update_cmr, is_surveillance)
SELECT condition.id, decision.kind, 1, decision.allow_new, decision.allow_update, 0
FROM (VALUES
        (N'Hepatitis A', N'message_filter', 0, 0),
        (N'Hepatitis A', N'case_management', 1, 1),
        (N'Giardiasis', N'message_filter', 0, 0),
        (N'Giardiasis', N'case_management', 1, 1),
        (N'Lyme disease', N'message_filter', 0, 0),
        (N'Lyme disease', N'case_management', 0, 1))
     AS decision (condition, kind, allow_new, allow_update)
JOIN conditions AS condition ON condition.name = decision.condition;

INSERT INTO vocab_rule_conditions (vocab_rule_id, field, operator, value, and_or)
SELECT vocab_rule.id, N'TestResult', N'=', N'Positive', NULL
FROM vocab_rules AS vocab_rule
WHERE vocab_rule.kind = N'case_management';

INSERT INTO persons
    (last_name, first_name, middle_name, birth_date, days_old_no_birthday,
     birth_gender, current_gender, ethnicity, language, country_of_birth)
VALUES
    (N'Haddad', N'Leila', NULL, '1988-04-12', NULL, N'F', N'F', N'NH', N'en', N'USA'),
    (N'Brennan', N'Owen', NULL, '1971-09-30', NULL, N'M', N'M', N'NH', N'en', N'USA');

INSERT INTO person_addresses
    (person_id, street, unit_number, city, state, postal_code, county,
     beginning, ending, note, residence_type_id)
SELECT person.id, address.street, NULL, N'Salt Lake City', N'UT', N'84101', N'SL',
       NULL, NULL, NULL, NULL
FROM (VALUES
        (N'Haddad', N'48 Orchard Lane'),
        (N'Brennan', N'7 Canyon Road'))
     AS address (last_name, street)
JOIN persons AS person ON person.last_name = address.last_name;

-- Leila Haddad's open Giardiasis case, which elr-update-example.hl7 updates:
-- its event date is a week before the message's collection date, inside the
-- default whitelist rule's 90 days, and it has no laboratory result yet.
INSERT INTO person_conditions
    (person_id, condition_id, record_number, agency_id,
     first_reported_ph_date, event_onset_date, created_at)
SELECT person.id, condition.id, N'202600901', 5,
       '2026-09-28', '2026-09-28', '2026-09-28T10:30:00-06:00'
FROM persons AS person
CROSS JOIN conditions AS condition
WHERE person.last_name = N'Haddad' AND person.first_name = N'Leila'
  AND condition.name = N'Giardiasis';
