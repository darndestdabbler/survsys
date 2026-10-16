namespace Survsys.Contracts;

/// <summary>
/// That a user opened a case morbidity report, and when: one row of what
/// <c>GET /api/access-audit</c> pages.
/// </summary>
/// <param name="Id">The record's identifier.</param>
/// <param name="UserId">The user who opened the report.</param>
/// <param name="UserName">That user's user name.</param>
/// <param name="PersonConditionId">The report that was opened.</param>
/// <param name="AccessedAt">When it was opened, in UTC.</param>
public record AccessRecordResponse(
    int Id,
    int UserId,
    string UserName,
    int PersonConditionId,
    DateTime AccessedAt);
