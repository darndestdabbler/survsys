using Microsoft.EntityFrameworkCore;

namespace Survsys.Data;

/// <summary>
/// That a case changed and so is due to be notified to CDC: the person-
/// condition id of the case, and when it changed. The domain event the case
/// write path raises, which <see cref="CaseNotificationQueue"/> turns into a
/// queue entry.
/// </summary>
/// <remarks>
/// The application-side stand-in for the legacy database trigger that watched
/// <c>person_condition</c> and queued a case on insert or update. Raised in
/// application code where the case is written rather than in the database, so
/// the queuing is visible to a reader of the write path and testable without a
/// trigger. A plain record of the fact, carrying nothing the queue does not
/// need.
/// </remarks>
/// <param name="PersonConditionId">The case that changed.</param>
/// <param name="ChangedAt">When it changed, in UTC — the time the entry is queued under.</param>
public sealed record CaseChanged(int PersonConditionId, DateTime ChangedAt);

/// <summary>
/// The auto-queue: it keeps the outbound notification queue in step with case
/// changes, upserting the one <see cref="NotificationQueueEntry"/> a changed
/// case has.
/// </summary>
/// <remarks>
/// Given the <see cref="SurvsysDbContext"/>, so it writes through the one path
/// into the database every other write here takes. <see cref="EnqueueAsync"/>
/// stages the change on the context and does not save it: the caller is in the
/// middle of writing the case, and the queue entry is meant to land in that
/// same unit of work, so the caller's <c>SaveChangesAsync</c> persists both the
/// case and its queue entry together or neither.
///
/// One entry to a case: the queue entry is unique on its person-condition id,
/// so a case that changes again has its existing entry's time refreshed rather
/// than a second entry made. Whether a queued case is actually exported, and
/// when the entry leaves the queue, is the later queue-review and process work;
/// here is only the keeping of the queue in step with the cases.
/// </remarks>
public sealed class CaseNotificationQueue(SurvsysDbContext database)
{
    /// <summary>
    /// Upserts the queue entry for the changed case: refreshes the queued time
    /// of the case's existing entry, or adds a new unheld entry where the case
    /// has none yet. Stages the change on the context without saving, so it
    /// persists in the caller's unit of work.
    /// </summary>
    public async Task EnqueueAsync(CaseChanged change, CancellationToken cancellationToken)
    {
        var existing = await database.NotificationQueue.SingleOrDefaultAsync(
            entry => entry.PersonConditionId == change.PersonConditionId, cancellationToken);

        if (existing is null)
        {
            database.NotificationQueue.Add(new NotificationQueueEntry
            {
                PersonConditionId = change.PersonConditionId,
                QueuedAt = change.ChangedAt,
                Hold = false,
            });
        }
        else
        {
            existing.QueuedAt = change.ChangedAt;
        }
    }
}
