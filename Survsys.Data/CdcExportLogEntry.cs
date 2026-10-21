namespace Survsys.Data;

/// <summary>
/// One export of a case to CDC, as the log keeps it: which case was exported
/// under which record number and notification guide, the message that was
/// generated for it, when it was queued and when it was sent, and the outcome
/// with its detail. An append-only history — a case exported more than once
/// has a row for each — rather than the one-per-case queue
/// <see cref="NotificationQueueEntry"/> is.
/// </summary>
/// <remarks>
/// The legacy NMI <c>nmi.log</c> row, reduced to what this phase needs. The
/// legacy table fanned out to <c>nmi.log_detail</c> (a per-PHIN-variable
/// message with a severity) and <c>nmi.process_log</c> (an optional debug
/// trace), and carried a <c>cdc_batch_file</c> naming the batch an export rode
/// in; none is here, because this session creates the log table and nothing
/// writes a row to it yet — the generation, transmission and per-variable
/// messaging that would fill those are later work. What remains is the spine
/// of a log row: the case, its record number and guide, the generated message,
/// the queued and sent times, and the outcome.
///
/// The per-variable messages are now its <see cref="Details"/>, the legacy
/// <c>nmi.log_detail</c>; the processing id and queue the export ran under
/// are recorded on the row, and the <c>cdc_batch_file</c> is its
/// <see cref="BatchFile"/>.
///
/// Otherwise a thin holder of cross-table ids, as the reference rows are: the
/// case is <see cref="PersonConditionId"/> and the guide is
/// <see cref="GuideId"/>, each a bare int read elsewhere, not a link walked
/// from here.
/// </remarks>
public class CdcExportLogEntry
{
    public int Id { get; set; }

    /// <summary>
    /// The case this export was of — the <see cref="PersonCondition"/> it is
    /// filed against. The row is deleted with the case. The legacy
    /// <c>person_condition_id</c>.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// The case's record number as it stood when the export was made, kept on
    /// the log so the history reads without a join back to a case whose number
    /// may since have changed. The legacy <c>record_number</c>.
    /// </summary>
    public string RecordNumber { get; set; } = string.Empty;

    /// <summary>
    /// The notification guide the export was generated under, or null where
    /// none was recorded. A bare id with no navigation. The legacy
    /// <c>guide_id</c>.
    /// </summary>
    public int? GuideId { get; set; }

    /// <summary>
    /// The message generated for the export — the ER7 or XML that was or would
    /// be sent — or null where none has been generated yet. Unbounded, as a
    /// generated message is as long as it is. The legacy <c>xml</c>.
    /// </summary>
    public string? GeneratedMessage { get; set; }

    /// <summary>When the case was queued for this export, in UTC. The legacy <c>queue_time</c>.</summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// When the export was sent to CDC, in UTC, or null while it is still
    /// queued and unsent. The legacy <c>sent_at</c>.
    /// </summary>
    public DateTime? SentAt { get; set; }

    /// <summary>
    /// The outcome of the export as a short code, or null while it is unsent.
    /// Reduced from the legacy severity-bearing log detail to the single
    /// outcome of the export here.
    /// </summary>
    public string? Outcome { get; set; }

    /// <summary>
    /// The detail behind the outcome — the responding account or the reason an
    /// export faulted — or null where there is none. Unbounded, as the detail
    /// carries whatever was said.
    /// </summary>
    public string? Detail { get; set; }

    /// <summary>
    /// The processing id the message was generated under — <c>P</c>
    /// (production), <c>T</c> (training) or <c>D</c> (debug), as MSH-11 carries
    /// it — or null where none was recorded.
    /// </summary>
    public string? ProcessingId { get; set; }

    /// <summary>
    /// The queue the export ran from — <c>production</c>, <c>onboarding</c> or
    /// <c>batch</c> — or null where none was recorded.
    /// </summary>
    public string? Queue { get; set; }

    /// <summary>
    /// The file the export was written to for a PHINMS or AIMS sender to pick
    /// up, or null where it was posted directly or not sent. Each file carries
    /// one message, so this names what the file carried. The legacy
    /// <c>cdc_batch_file</c>.
    /// </summary>
    public string? BatchFile { get; set; }

    /// <summary>
    /// The messages building the export raised, each with its severity — the
    /// legacy <c>nmi.log_detail</c> rows. The one navigation here, so a log row
    /// and its details are written together.
    /// </summary>
    public List<CdcExportLogDetail> Details { get; set; } = [];
}
