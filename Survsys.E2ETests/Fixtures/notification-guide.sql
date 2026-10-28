-- The notification guide the CDC walkthroughs send under: one guide, with
-- Lyme disease exported under it.
--
-- The queue worker builds a case's message only when the case's condition is
-- exported under a guide (condition_detail), and nothing in the application
-- assigns a condition to a guide, so this fixture does, the way elr-intake.sql
-- puts the intake's reference data on file. Lyme disease is the condition the
-- CDC walkthroughs report on, and no other walkthrough does.
--
-- The guide carries no PHIN variables, so its message is two segments, the
-- MSH header and the Epidemiologic Information OBR -- no PID, since no DEM
-- variable fills one, and no OBX -- and nothing a value set or a rule set
-- would have to be on file for. Seeded by the main harness only,
-- after conditions.sql, whose Lyme disease it names by name because identity
-- assigns the ids.

INSERT INTO guide (name, version)
VALUES (N'Walkthrough guide', NULL);

INSERT INTO condition_detail (condition_id, guide_id)
SELECT conditions.id, guide.id
FROM conditions, guide
WHERE conditions.name = N'Lyme disease' AND guide.name = N'Walkthrough guide';
