-- Seven real rows of the legacy conditions reference table, copied from
--
--   ../nedss/legacy/nedss-jpa/src/main/resources/db/migration/V0003__init.sql
--
-- which is the Flyway seed EpiTrax ships and the only place the 186 condition
-- rows exist. Session 5's section of the session plan named
-- ../nedss/docs/db-snapshots/ as the fixture source; none of those five
-- snapshot sets dumps the condition table -- they dump person,
-- person_condition, facility and their neighbours -- so the rows are taken
-- from the seed instead, and this comment is where that is said.
--
-- Two translations from the legacy values, both deliberate:
--
--  * The legacy columns are an unbounded VARCHAR with no null convention, and
--    a condition with no CDC code or no lead-in prose carries an empty string.
--    This schema models absence as NULL, so an empty legacy string arrives
--    here as NULL. Bartonellosis is a real condition with no CDC code, and is
--    in this fixture to carry that case.
--  * active is a bit rather than PostgreSQL's bool, so true and false arrive
--    as 1 and 0.
--
-- The insert order is deliberately not alphabetical. The ids identity assigns
-- therefore run in a different order from the names, so a test that asserts
-- the rows come back by name is asserting something: an endpoint that forgot
-- to order would return them in this order instead.

INSERT INTO conditions (name, contact_lead_in, place_lead_in, treatment_lead_in, active, cdc_code, condition_group_id)
VALUES
    (N'Malaria', NULL, NULL, NULL, 1, N'10130', NULL),
    (N'Giardiasis', N'Identify all ill contacts.', NULL, NULL, 1, N'11570', NULL),
    (N'Lyme disease', NULL, NULL, NULL, 1, N'11080', NULL),
    (N'Bartonellosis', NULL, NULL, NULL, 1, NULL, NULL),
    (N'Listeriosis', N'Listeria is rarely transmitted from person to person.  Identifying contacts for this disease is at the discretion of the local health department.', NULL, NULL, 1, N'10640', NULL),
    (N'Hepatitis A', N'There will be two separate questions. The first is "Was patient a contact of a person with confirmed or suspected Hepatitis A infection?" If yes, contact info will be entered in the Contacts tab. The next question isn''t really a question, it''s an instruction: "Obtain information for all contacts exposed to case during the communicable period".', NULL, NULL, 1, N'10110', NULL),
    (N'Influenza, animal isolates', NULL, NULL, NULL, 0, N'11070', NULL);
