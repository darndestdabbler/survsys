namespace Survsys.Data;

/// <summary>
/// One inbound electronic lab report as it waits in the intake queue for a
/// reviewer: the raw message exactly as it arrived, where it came from and
/// when, the review state it sits in, whether it is flagged for QA, and the
/// detail of the exception that stopped it where one did. The legacy EMSA
/// <c>system_messages</c> row, reduced to what this phase needs — the message
/// pipeline's intake half, absorbed from Mirth and queued for review here
/// rather than assigned to a case, which is session 40's work.
/// </summary>
/// <remarks>
/// A thin holder of codes, as a report's case status is: the review state is a
/// string code read against <see cref="MessageReviewStatusRules"/>, not an
/// enum column, so the vocabulary lives in one place and the row carries the
/// code it was written under. This stages a message and resolves nothing — it
/// holds no person, no lab and no case, because queuing for review creates
/// none of those. It does keep the fields the parser read, as the text the
/// message gave: one staged message per order (OBR), each carrying the whole
/// raw message, the patient, its one order and specimen, and that order's
/// <see cref="Results"/>. A message that failed to parse keeps none of them.
/// </remarks>
public class StagedMessage
{
    public int Id { get; set; }

    /// <summary>
    /// The message exactly as it arrived, the ER7 text a lab sent. Kept whole
    /// so a reviewer reads what was received rather than a reconstruction of
    /// it, and so a message that failed to parse still has its raw form on
    /// file. The legacy <c>message</c>.
    /// </summary>
    public string RawMessage { get; set; } = string.Empty;

    /// <summary>
    /// Where the message came from — the dropped file's name, or the connector
    /// a lab sent under — or null where none was recorded. Generalises the
    /// legacy <c>connector</c>, which named the source lab.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>When the message was received into the queue, in UTC.</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// The review state this message sits in, as a code read against
    /// <see cref="MessageReviewStatusRules"/>: a parseable message arrives in
    /// <see cref="MessageReviewStatus.Entry"/> and one that failed to parse in
    /// <see cref="MessageReviewStatus.Exception"/>. The legacy
    /// <c>final_status</c>, which held the numeric queue id; here it holds the
    /// code the message was queued under.
    /// </summary>
    public string ReviewStatus { get; set; } = MessageReviewStatusRules.EntryCode;

    /// <summary>
    /// Whether the message is flagged for quality-assurance review. A message
    /// arrives unflagged; a reviewer raises the flag in a later session. The
    /// legacy QA queue, kept here as a flag on the message rather than a queue
    /// of its own.
    /// </summary>
    public bool QaFlagged { get; set; }

    /// <summary>
    /// The detail of the exception that stopped this message, or null where
    /// none did. Set when the review state is
    /// <see cref="MessageReviewStatus.Exception"/> — for a message that failed
    /// to parse, the parser's account of why — and null otherwise.
    /// </summary>
    public string? ExceptionDetail { get; set; }

    /// <summary>
    /// The format the message was read as, such as <c>HL7v2</c>; a message
    /// that failed to parse keeps the format its parser reads.
    /// </summary>
    public string MessageFormat { get; set; } = "HL7v2";

    /// <summary>The message type, such as <c>ORU^R01</c> (MSH-9).</summary>
    public string? MessageType { get; set; }

    /// <summary>The sending facility's code (MSH-4.1).</summary>
    public string? SendingFacility { get; set; }

    public string? PatientLastName { get; set; }

    public string? PatientFirstName { get; set; }

    public string? PatientMiddleName { get; set; }

    public DateOnly? PatientBirthDate { get; set; }

    /// <summary>The patient's administrative sex (PID-8).</summary>
    public string? PatientSex { get; set; }

    public string? PatientStreet { get; set; }

    public string? PatientCity { get; set; }

    public string? PatientState { get; set; }

    public string? PatientZip { get; set; }

    /// <summary>
    /// Which order of its message this row holds, counting from zero, or null
    /// for a message that failed to parse or reported no order.
    /// </summary>
    public int? OrderIndex { get; set; }

    /// <summary>The filler order number (OBR-3.1).</summary>
    public string? AccessionNo { get; set; }

    /// <summary>The ordered test's code (OBR-4.1).</summary>
    public string? TestCode { get; set; }

    /// <summary>The ordered test's name (OBR-4.2).</summary>
    public string? TestName { get; set; }

    /// <summary>The observation date (OBR-7).</summary>
    public DateOnly? ObservedOn { get; set; }

    /// <summary>The specimen type's code (SPM-4.1).</summary>
    public string? SpecimenSourceCode { get; set; }

    /// <summary>The specimen type's text (SPM-4.2).</summary>
    public string? SpecimenSourceText { get; set; }

    /// <summary>The collection date (SPM-17, else OBR-7).</summary>
    public DateOnly? CollectedOn { get; set; }

    /// <summary>The order's results (its OBX segments), in message order.</summary>
    public List<StagedMessageResult> Results { get; set; } = [];
}
