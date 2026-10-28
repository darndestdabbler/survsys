namespace Survsys.Contracts;

/// <summary>
/// One change to one row, as <c>GET /api/audit</c> pages them.
/// </summary>
/// <param name="Id">The audit entry's identifier.</param>
/// <param name="TableName">The audited table.</param>
/// <param name="KeyValues">The changed row's primary key, as text.</param>
/// <param name="Operation">Insert, Update or Delete.</param>
/// <param name="OccurredAt">When the change was saved, in UTC.</param>
/// <param name="UserId">The user who made it, or null where none was signed in.</param>
/// <param name="UserName">That user's user name, or null.</param>
/// <param name="OldValues">The row before the change, as JSON; null for an insert.</param>
/// <param name="NewValues">The row after the change, as JSON; null for a delete.</param>
public record AuditEntryResponse(
    int Id,
    string TableName,
    string KeyValues,
    string Operation,
    DateTime OccurredAt,
    int? UserId,
    string? UserName,
    string? OldValues,
    string? NewValues);
