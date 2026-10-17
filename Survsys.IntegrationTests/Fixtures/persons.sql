-- Seven people for the person round-trip, and the rows that hang off them.
--
-- The first of them is real. Maria Elena Rodriguez, her address, her
-- telephone and her email address are copied value for value out of
--
--   ../nedss/docs/db-snapshots/session-010/03-after-demographics-save.txt
--
-- which is the snapshot the session plan names: the state of the legacy
-- database immediately after a demographics save, and the only dump of a
-- person these snapshots hold.
--
-- Three things in this file are NOT from that snapshot, and this is where
-- that is said rather than left for a reader to work out:
--
--  * person_race is empty in every session-010 snapshot, and none of them
--    dumps person_alias or parent_guardian at all. Maria's two races, her
--    alias and her guardian row are written for this suite, because the
--    multi-select race, the aliases and the parent-or-guardian table are
--    three of the things this session's schema is for and a fixture that
--    left them empty would assert nothing about them. Her guardian's name
--    is the one real value available: 'Carlos Rodriguez', which the
--    snapshot carries on person_condition.parent_guardian — the column that
--    belongs to the case morbidity report rather than to the person.
--  * The other six people are written for this suite. One person cannot
--    show an ordering or a page boundary, and the endpoint promises both.
--  * Carlos Rodriguez is here as a person of his own as well as a name on
--    Maria's guardian row, because the legacy parent_guardian table allows
--    both and because two people sharing a surname is what makes the
--    endpoint's second ordering key — the first name — assert something.
--
-- Two translations from the legacy types, both the schema's and both
-- deliberate:
--
--  * birth_date, beginning and ending are dates here where the legacy
--    columns are timestamps. Every value in the snapshots is at midnight.
--  * An absent value is NULL. The legacy data writes an empty string in
--    several of these columns, and a column that means "nobody has said"
--    says it with NULL in this schema.
--
-- The insert order is deliberately not the order the endpoint returns, so a
-- test that asserts the rows come back by name is asserting something: an
-- endpoint that forgot to order would return them in this order instead.
-- The child rows find their person by name rather than by a hard-coded id,
-- because identity assigns the ids.

INSERT INTO persons
    (last_name, first_name, middle_name, birth_date, days_old_no_birthday,
     birth_gender, current_gender, ethnicity, language, country_of_birth)
VALUES
    (N'Rodriguez', N'Maria', N'Elena', '1985-06-15', NULL, N'F', N'F', N'H', N'es', N'MEX'),
    (N'Zimmerman', N'Paul', NULL, '1950-07-04', NULL, N'M', N'M', N'NH', N'en', N'USA'),
    (N'Nguyen', N'An', NULL, '1992-02-29', NULL, N'M', N'M', N'NH', N'vi', N'VNM'),
    (N'Rodriguez', N'Carlos', NULL, '1960-01-20', NULL, N'M', N'M', N'H', N'es', N'MEX'),
    -- No first name at all: the one required name part is the last one, and
    -- a page has to carry a person who has only that.
    (N'Doe', NULL, NULL, NULL, NULL, N'UNK', N'UNK', N'UNK', N'UNK', NULL),
    -- An infant reported with an age but no birthday: the legacy fallback,
    -- and the only row where days_old_no_birthday is anything.
    (N'Abbott', N'Jane', N'Marie', NULL, 14, N'F', N'F', N'NH', N'en', N'USA'),
    (N'Okafor', N'Chidi', NULL, '1978-11-03', NULL, N'M', N'M', N'NH', N'en', N'NGA');

INSERT INTO person_addresses
    (person_id, street, unit_number, city, state, postal_code, county,
     beginning, ending, note, residence_type_id)
SELECT id, N'1234 Main Street', N'Apt 5B', N'Salt Lake City', N'UT', N'84101', N'SL',
       NULL, NULL, NULL, NULL
FROM persons
WHERE last_name = N'Rodriguez' AND first_name = N'Maria';

INSERT INTO person_telephones
    (person_id, telephone_type, country_code, area_code, phone_number, extension,
     beginning, note)
SELECT id, NULL, NULL, N'801', N'5551234', NULL, NULL, NULL
FROM persons
WHERE last_name = N'Rodriguez' AND first_name = N'Maria';

INSERT INTO person_emails (person_id, email_address, current_email)
SELECT id, N'maria.rodriguez@example.com', NULL
FROM persons
WHERE last_name = N'Rodriguez' AND first_name = N'Maria';

-- Two races for one person, which is the whole point of the multi-select:
-- the key is the person and the race together, so the schema itself refuses
-- the same race twice and allows any number of different ones.
INSERT INTO person_races (person_id, race)
SELECT id, N'W' FROM persons WHERE last_name = N'Rodriguez' AND first_name = N'Maria'
UNION ALL
SELECT id, N'AA' FROM persons WHERE last_name = N'Rodriguez' AND first_name = N'Maria';

INSERT INTO person_aliases (person_id, person_alias)
SELECT id, N'Maria Elena Garcia'
FROM persons
WHERE last_name = N'Rodriguez' AND first_name = N'Maria';

-- A guardian named, and linked: 'Carlos Rodriguez' is both the name on the
-- row and a person in this fixture, which is the case the legacy table's two
-- columns exist for.
INSERT INTO parent_guardians (child_id, parent_id, parent_guardian)
SELECT child.id, parent.id, N'Carlos Rodriguez'
FROM persons AS child
CROSS JOIN persons AS parent
WHERE child.last_name = N'Rodriguez' AND child.first_name = N'Maria'
  AND parent.last_name = N'Rodriguez' AND parent.first_name = N'Carlos';
