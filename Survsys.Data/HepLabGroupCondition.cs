namespace Survsys.Data;

/// <summary>
/// A condition in a hepatitis lab group: its notification carries the group's
/// lab results.
/// </summary>
/// <remarks>
/// The legacy <c>hep_lab_condition_group.condition_id</c> array, held as one
/// row per condition. Keyed by the condition, so a condition is in one group;
/// both ids are bare. There is no behaviour here and no navigation out.
/// </remarks>
public class HepLabGroupCondition
{
    /// <summary>The group, as a bare id into <c>nmi_hep_lab_group</c>.</summary>
    public int HepLabGroupId { get; set; }

    /// <summary>The condition, as a bare id into <c>conditions</c>. The key.</summary>
    public int ConditionId { get; set; }
}
