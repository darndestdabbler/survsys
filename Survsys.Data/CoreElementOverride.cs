namespace Survsys.Data;

/// <summary>
/// A core element shown or hidden for one condition, one agency, or one
/// condition within one agency, in place of the element's default.
/// </summary>
/// <remarks>
/// The legacy keeps these in two tables, <c>core_element_condition</c> and
/// <c>core_element_agency</c> (whose condition is optional); this is the two
/// folded into one row with an optional condition and an optional agency, at
/// least one of them set. <see cref="CoreElementVisibilityRules.IsHidden"/>
/// says which override wins where several apply.
/// </remarks>
public class CoreElementOverride
{
    public int Id { get; set; }

    /// <summary>The element the override is for.</summary>
    public int CoreElementId { get; set; }

    /// <summary>The condition the override applies to, or null for every condition.</summary>
    public int? ConditionId { get; set; }

    /// <summary>The agency the override applies to, or null for every agency.</summary>
    public int? AgencyId { get; set; }

    /// <summary>Whether the element is hidden where this override applies.</summary>
    public bool Hidden { get; set; }
}
