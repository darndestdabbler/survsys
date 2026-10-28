namespace Survsys.Contracts;

/// <summary>
/// The body of a request that raises or lowers a notification-queue entry's
/// hold.
/// </summary>
/// <remarks>
/// One field, because a hold is one bit: true holds the case back from export
/// pending review, false releases it. The case is named in the URL, so it is
/// not here. Carrying the bit rather than a bare verb lets the one endpoint
/// both raise and lower it, as the legacy queue review raised and lowered the
/// <c>hold</c> flag.
/// </remarks>
/// <param name="Hold">True to hold the case back from export, false to release it.</param>
public record SetNotificationHoldRequest(bool Hold);
