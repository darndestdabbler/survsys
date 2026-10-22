namespace Survsys.Contracts;

/// <summary>
/// A condition's print criteria: the case statuses that make its cases
/// notifiable to CDC, as <c>GET /api/conditions/{id}/print-criteria</c> answers.
/// </summary>
/// <param name="ConditionId">The condition.</param>
/// <param name="CaseStatuses">Its notifiable case status codes, in code order.</param>
public record ConditionPrintCriteriaResponse(int ConditionId, IReadOnlyList<string> CaseStatuses);
