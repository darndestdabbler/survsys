namespace Survsys.Contracts;

/// <summary>
/// Who holds a case morbidity report's edit lock, and until when — every
/// member null where nobody holds it.
/// </summary>
/// <param name="UserId">The user holding the lock, or null where none does.</param>
/// <param name="UserName">That user's display name, or null where none holds it.</param>
/// <param name="LockedAt">When the lock was taken, or null where none holds it.</param>
/// <param name="ExpiresAt">When the lock lapses unless taken again, or null where none holds it.</param>
public record EditLockResponse(
    int? UserId,
    string? UserName,
    DateTimeOffset? LockedAt,
    DateTimeOffset? ExpiresAt);
