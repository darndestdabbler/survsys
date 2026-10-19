namespace Survsys.Contracts;

/// <summary>
/// One case waiting to be notified to CDC, as the API tier returns it in the
/// notification-queue list.
/// </summary>
/// <remarks>
/// The queue joins its entry to the case so a row reads whole without the
/// screen resolving a person or a condition of its own: the case's identifier,
/// its record number, the patient's name and the condition's name, when the
/// case was last queued, and whether the entry is held back from export. Unlike
/// the work queue, which carries identifiers and lets the screen name them from
/// catalogues it already holds, this row names the patient and the condition,
/// because the notification queue screen has neither catalogue in hand.
///
/// The case is named by its person-condition id, which is also how the hold is
/// raised and lowered: the queue is one-entry-to-a-case, so the case id
/// addresses the entry.
/// </remarks>
/// <param name="PersonConditionId">The case this row queues, and the id its hold is set by.</param>
/// <param name="RecordNumber">The number the case is known by.</param>
/// <param name="PatientName">
/// The patient the case is about, last name first for display — the last name
/// alone where no first name is on file.
/// </param>
/// <param name="Condition">The disease or condition the case is for, by name.</param>
/// <param name="QueuedAt">When the case was last queued for notification, in UTC.</param>
/// <param name="Hold">Whether the entry is held back from export pending review.</param>
public record NotificationQueueItemResponse(
    int PersonConditionId,
    string RecordNumber,
    string PatientName,
    string Condition,
    DateTime QueuedAt,
    bool Hold);
