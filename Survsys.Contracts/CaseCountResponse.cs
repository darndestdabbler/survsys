namespace Survsys.Contracts;

/// <summary>
/// One row of an ad-hoc case-count report: a value of the chosen grouping, and
/// how many case morbidity reports have it.
/// </summary>
/// <param name="Group">The grouping's value as text, or null for the reports that have none.</param>
/// <param name="Count">How many reports have that value.</param>
public sealed record CaseCountResponse(string? Group, int Count);
