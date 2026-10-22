namespace Survsys.Contracts;

/// <summary>
/// One condition a reportability response determined for one responsible
/// agency, as the eCR document screen shows it.
/// </summary>
/// <param name="RrDocumentId">The id of the RR the condition is from.</param>
/// <param name="ConditionCode">The condition's code (SNOMED CT), or null.</param>
/// <param name="ConditionName">The condition's display name, or null.</param>
/// <param name="DeterminationCode">The determination, RRVS1 reportable to RRVS4 no rule met, or null.</param>
/// <param name="DeterminationName">The determination's display name, or null.</param>
/// <param name="LocationCode">Whose address the determination was made for (RRVS5 to RRVS7), or null.</param>
/// <param name="LocationName">That location's display name, or null.</param>
/// <param name="ResponsibleAgencyId">The responsible agency's id, or null.</param>
/// <param name="ResponsibleAgencyName">The responsible agency's name, or null.</param>
/// <param name="ReportingTimeframe">The time to report within, or null.</param>
public record EcrConditionResponse(
    string RrDocumentId,
    string? ConditionCode,
    string? ConditionName,
    string? DeterminationCode,
    string? DeterminationName,
    string? LocationCode,
    string? LocationName,
    string? ResponsibleAgencyId,
    string? ResponsibleAgencyName,
    string? ReportingTimeframe);
