namespace Survsys.Data;

/// <summary>
/// One case waiting to be notified to CDC: the outbound queue entry an
/// application-side case change writes, holding which case is queued and when
/// it was queued, and whether it is held back from export pending review.
/// </summary>
/// <remarks>
/// The legacy NMI <c>nmi.export_queue</c> row, reduced to what this phase
/// needs. The legacy table carried a <c>log_id</c> linking the queue entry to
/// a generated <c>nmi.log</c> row and a <c>queue</c> type naming which stream
/// (<c>production</c>, <c>onboarding</c>, <c>batch</c>, <c>noQueue</c>) the
/// export belonged to; neither is here, because this session queues a case on
/// change and does not yet generate or transmit anything, so there is no log
/// row to point at and one stream to be in. What remains is the case, the time
/// it was queued, and the hold flag the legacy queue review raised and
/// lowered.
///
/// One entry to a case: the queue is upserted on each case change rather than
/// appended to, so a case that changes repeatedly has its single entry's
/// <see cref="QueuedAt"/> refreshed, not a second entry made. A thin holder of
/// a cross-table id with no navigation, as the reference rows are: the case is
/// <see cref="PersonConditionId"/> and nothing more is read through it here.
/// </remarks>
public class NotificationQueueEntry
{
    public int Id { get; set; }

    /// <summary>
    /// The case this entry queues — the <see cref="PersonCondition"/> it is
    /// filed against. Unique across the queue, so a case has at most one entry;
    /// the entry is deleted with the case. The legacy <c>person_condition_id</c>,
    /// read there through the queue's <c>log_id</c> and carried here directly.
    /// </summary>
    public int PersonConditionId { get; set; }

    /// <summary>
    /// When the case was last queued, in UTC. Refreshed each time the case
    /// changes and is re-queued, so it names the most recent change rather than
    /// the first. The legacy <c>queue_time</c>, which lived on the log row.
    /// </summary>
    public DateTime QueuedAt { get; set; }

    /// <summary>
    /// Whether the entry is held back from export pending review. An
    /// auto-queued entry is not held — it arrives false — and the hold-and-
    /// approve review that raises and lowers it is a later session's queue
    /// screen. The legacy <c>hold</c>.
    /// </summary>
    public bool Hold { get; set; }
}
