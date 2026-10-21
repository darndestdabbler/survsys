namespace Survsys.Contracts;

/// <summary>
/// That a user opened a case morbidity report, and when: one row of what
/// <c>GET /api/access-audit</c> pages.
/// </summary>
/// <param name="Id">The record's identifier.</param>
/// <param name="UserId">The user who opened the report.</param>
/// <param name="UserName">That user's user name.</param>
/// <param name="PersonConditionId">The report that was opened; null for an action not on one report.</param>
/// <param name="Action">What the user did: View, SignIn, Export or Search.</param>
/// <param name="Detail">What the action was on, where it names anything.</param>
/// <param name="AccessedAt">When it happened, in UTC.</param>
public record AccessRecordResponse(
    int Id,
    int UserId,
    string UserName,
    int? PersonConditionId,
    string Action,
    string? Detail,
    DateTime AccessedAt);
