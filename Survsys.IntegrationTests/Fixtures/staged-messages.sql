-- Four inbound electronic lab reports waiting in the intake queue, for the
-- phase 5 ELR walkthroughs to review, promote and find a duplicate of.
--
-- These rows stand where the intake service (Survsys.Api/ElrIntakeService.cs)
-- would leave them had a file been dropped and polled: a parseable message in
-- the Entry state queued for review, and an unparseable one in the Exception
-- state carrying the parser's account of why. This fixture writes them
-- straight into the table instead, the way Survsys.IntegrationTests stages a
-- message through the context, so the end-to-end suite has messages to act on
-- without a live file drop and its poll interval.
--
-- The table is the legacy EMSA system_messages, mapped column by column in
-- Survsys.Data/StagedMessageConfiguration.cs: message (the raw ER7), source,
-- received_at (UTC), final_status (a code read against MessageReviewStatusRules
-- -- 'entry', 'pending', 'exception', 'assigned'), qa_flag and exception_detail.
--
-- Each row carries a distinct source, which is the handle a walkthrough's test
-- finds its own message by -- identity assigns the ids, so a source is the one
-- stable thing a test can name. The received_at values put them oldest first,
-- the order the queue screen lists them in. The raw messages are modelled on
-- Fixtures/Hl7/oru-r01-chlamydia.hl7; promotion does not parse them -- the
-- reviewer confirms the person, disease and agency by hand -- so their content
-- is what the review screen shows rather than anything a promotion reads.

INSERT INTO system_messages
    (message, source, received_at, final_status, qa_flag, exception_detail)
VALUES
    -- The exception: held because it never parsed, so there is nothing to
    -- promote. The raw has no MSH segment, which is why it failed; the detail
    -- is the parser's own sentence (OruR01Parser.cs), set here because this
    -- fixture does not run the parser.
    (N'GARBLED|this is not|an HL7 v2 message|no MSH segment here',
     N'elr-exception-example.hl7', '2026-10-15 07:45:00', N'exception', 0,
     N'The message could not be parsed as HL7 v2.'),
    -- Flagged for quality assurance: a queued message a reviewer has marked
    -- for a closer look. It carries the QA flag the review walkthrough reads.
    (N'MSH|^~\&|LAB_SYSTEM|QA_LAB^2.16.840.1.113883.3.72.5.30.1^ISO|EMSA|UTAH_DPH^2.16.840.1.113883.3.72.5.20^ISO|20260315081000||ORU^R01^ORU_R01|MSG00042|P|2.5.1
PID|1||QAPAT001^^^QA_LAB^MR||DOE^JANE^M||19920229|F
OBR|1|ORD042^QA_LAB|OBR042^QA_LAB|24111-7^Neisseria gonorrhoeae rRNA^LN|||20260315074500
OBX|1|CWE|24111-7^Neisseria gonorrhoeae rRNA^LN||260373001^Detected^SCT|||A|||F',
     N'elr-qa-example.hl7', '2026-10-15 08:10:00', N'entry', 1, NULL),
    -- Queued for review, for the promote walkthrough: a reviewer finds the
    -- person, confirms the disease and agency, and opens a case from it.
    (N'MSH|^~\&|LAB_SYSTEM|ARUP_LAB^2.16.840.1.113883.3.72.5.30.1^ISO|EMSA|UTAH_DPH^2.16.840.1.113883.3.72.5.20^ISO|20260316090000||ORU^R01^ORU_R01|MSG00101|P|2.5.1
PID|1||PROMPAT001^^^ARUP_LAB^MR||OKAFOR^CHIDI||19781103|M
OBR|1|ORD101^ARUP_LAB|OBR101^ARUP_LAB|11585-7^Chlamydia trachomatis Ag^LN|||20260316080000
OBX|1|CWE|11585-7^Chlamydia trachomatis Ag^LN||260373001^Detected^SCT|||A|||F',
     N'elr-promote-example.hl7', '2026-10-16 09:00:00', N'entry', 0, NULL),
    -- Queued for review, for the duplicate walkthrough: it is promoted once
    -- and then revisited, where Survsys shows it is already a case and offers
    -- no second promotion.
    (N'MSH|^~\&|LAB_SYSTEM|QUEST_LAB^2.16.840.1.113883.3.72.5.30.1^ISO|EMSA|UTAH_DPH^2.16.840.1.113883.3.72.5.20^ISO|20260316090500||ORU^R01^ORU_R01|MSG00102|P|2.5.1
PID|1||DUPPAT001^^^QUEST_LAB^MR||ZIMMERMAN^PAUL||19500704|M
OBR|1|ORD102^QUEST_LAB|OBR102^QUEST_LAB|6349-5^Hepatitis A virus Ab^LN|||20260316080000
OBX|1|CWE|6349-5^Hepatitis A virus Ab^LN||260373001^Detected^SCT|||A|||F',
     N'elr-duplicate-example.hl7', '2026-10-16 09:05:00', N'entry', 0, NULL);
