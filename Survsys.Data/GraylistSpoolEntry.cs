namespace Survsys.Data;

/// <summary>
/// A pending graylist request: that an event was created and the time-windowed
/// duplicate detection has yet to check the waiting graylisted messages against
/// it. The legacy EpiTrax <c>graylist_spool</c>, the spool a trigger filled
/// when a case was created and the spooler drained.
/// </summary>
/// <remarks>
/// This is the spool, not the matcher: the rule that decides whether a waiting
/// message matches the event lives in Survsys.Messaging and reads data, never
/// this table. The event is <see cref="PersonConditionId"/>, a bare nullable id
/// into <c>person_conditions</c> with no navigation, as
/// <see cref="AccessRecord.PersonConditionId"/> is — the spool outlives the
/// event it names and never takes it with it.
/// </remarks>
public class GraylistSpoolEntry
{
    public int Id { get; set; }

    /// <summary>When the event was spooled, in UTC.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The source port the event arrived on, where one is recorded.</summary>
    public string? Port { get; set; }

    /// <summary>
    /// Whether the spooled request has been evaluated against the waiting
    /// messages. The legacy <c>sent</c> smallint flag, as a bool.
    /// </summary>
    public bool Sent { get; set; }

    /// <summary>
    /// The event that triggered the check, as a bare id into
    /// <c>person_conditions</c>; null where the spooled row names none. The
    /// legacy <c>event_id</c>.
    /// </summary>
    public int? PersonConditionId { get; set; }
}
