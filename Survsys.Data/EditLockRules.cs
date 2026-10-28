namespace Survsys.Data;

/// <summary>
/// Who holds a case's edit lock at a moment: the user who took it, until the
/// lock times out.
/// </summary>
/// <remarks>
/// Pure, as <see cref="CaseStatusRules"/> is: it holds no clock and no
/// <c>DbContext</c>, and the caller hands it the two columns
/// <see cref="PersonCondition.LockedByUserId"/> and
/// <see cref="PersonCondition.LockedAt"/> and the moment to weigh them at. A
/// lock is released by clearing the columns, or lapses by age: one taken
/// <see cref="Timeout"/> or longer ago is held by nobody, so a user who walks
/// away from an open case does not keep it from everyone else.
/// </remarks>
public static class EditLockRules
{
    /// <summary>How long a lock lasts from when it was taken.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The id of the user holding the lock at <paramref name="now"/>, or null
    /// where none was taken or it has timed out.
    /// </summary>
    public static int? HolderAt(int? lockedByUserId, DateTimeOffset? lockedAt, DateTimeOffset now) =>
        lockedByUserId is { } holder && lockedAt is { } taken && now - taken < Timeout
            ? holder
            : null;
}
