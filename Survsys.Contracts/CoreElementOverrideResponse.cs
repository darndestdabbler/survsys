namespace Survsys.Contracts;

/// <summary>
/// A core element shown or hidden for one condition, one agency, or one
/// condition within one agency, in place of the element's default.
/// </summary>
/// <param name="Id">The override's identifier.</param>
/// <param name="CoreElementId">The element the override is for.</param>
/// <param name="ConditionId">The condition it applies to, or null for every condition.</param>
/// <param name="AgencyId">The agency it applies to, or null for every agency.</param>
/// <param name="Hidden">Whether the element is hidden where the override applies.</param>
public record CoreElementOverrideResponse(int Id, int CoreElementId, int? ConditionId, int? AgencyId, bool Hidden);
