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
    /// Why this message is where it is, or null where nothing stopped it. Set
    /// with <see cref="ExceptionCategory"/> when the message is held as an
    /// exception — for a message that failed to parse, the parser's account of
    /// why — or sent to pending for a reviewer, as a graylist release does; set
    /// alone when processing graylists it, finds it out of state or prunes it;
    /// and cleared when it is assigned, retried or moved by hand to a state
    /// that holds it for nothing.
    /// </summary>
    public string? ExceptionDetail { get; set; }

    /// <summary>
    /// The kind of reason this message is held, as a code read against
    /// <see cref="MessageExceptionRules"/>, or null where it is neither held
    /// (exception, unprocessed, locked, graylist exception) nor pending, or was
    /// held before categories were recorded. Set wherever a message is held or
    /// sent to pending with why, and cleared with <see cref="ExceptionDetail"/>
    /// when it leaves for a state that holds it for nothing; whether the
    /// message may be retried is its category's.
    /// </summary>
    public string? ExceptionCategory { get; set; }

    /// <summary>
    /// The warning-level checks of its sender's conformance profile the
    /// message failed at intake, one per line, or null where it failed none or
    /// its sender has no profile. A message that fails a required check is
    /// held instead, under <see cref="MessageExceptionRules.ConformanceCode"/>.
    /// </summary>
    public string? ConformanceWarnings { get; set; }

    /// <summary>How many times this message has been retried, by hand or by reprocessing.</summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// When the agency this message was assigned for was sent the batch
    /// notification naming it, in UTC, or null where it has not been.
    /// </summary>
    public DateTime? NotifiedAt { get; set; }

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

    /// <summary>The patient's county code as the message carries it (PID-11.9).</summary>
    public string? PatientCounty { get; set; }

    /// <summary>
    /// Which row of its message this is, counting from zero across every
    /// patient's orders in message order — one row per order, and one for a
    /// patient reported with none — or null for a message that failed to parse
    /// or reported no patient.
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

    /// <summary>
    /// The condition intake's vocabulary resolved the order to — its first
    /// result's that resolved to one — as a bare id into <c>conditions</c>, or
    /// null where none did or the message was not translated.
    /// </summary>
    public int? ConditionId { get; set; }

    /// <summary>
    /// The investigating agency the patient's zip resolves to, as a bare id
    /// into <c>agencies</c>, or null where the zip names no single agency.
    /// </summary>
    public int? AgencyId { get; set; }

    /// <summary>
    /// Whether the condition's case-management rule lets this message open a
    /// new case, or null where no single rule decided.
    /// </summary>
    public bool? AllowNewCase { get; set; }

    /// <summary>
    /// Whether the condition's case-management rule lets this message update
    /// an existing case, or null where no single rule decided.
    /// </summary>
    public bool? AllowUpdateCase { get; set; }

    /// <summary>
    /// Whether the case this message opens is a surveillance case, by the
    /// condition's case-management rule, or null where no single rule decided.
    /// </summary>
    public bool? IsSurveillance { get; set; }

    /// <summary>
    /// The case this message was assigned to, or the case a graylist release
    /// found for it, as a bare id into <c>person_conditions</c> with no
    /// navigation, as <see cref="GraylistSpoolEntry.PersonConditionId"/> is;
    /// null where it has none.
    /// </summary>
    public int? PersonConditionId { get; set; }

    /// <summary>The order's results (its OBX segments), in message order.</summary>
    public List<StagedMessageResult> Results { get; set; } = [];

    /// <summary>Every state this message has entered, oldest first once ordered by when.</summary>
    public List<StagedMessageAudit> Audits { get; set; } = [];
}
