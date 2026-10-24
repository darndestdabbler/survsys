namespace Survsys.Contracts;

/// <summary>
/// The case statuses that make a condition's cases notifiable to CDC: what
/// <c>PUT /api/conditions/{id}/print-criteria</c> takes, replacing those on file.
/// </summary>
/// <param name="CaseStatuses">
/// The case status codes (<c>C</c>, <c>P</c>, <c>S</c>, <c>NC</c>); empty for
/// none, so no case of the condition is notified.
/// </param>
public record ConditionPrintCriteriaRequest(IReadOnlyList<string> CaseStatuses);
