namespace Survsys.Data;

/// <summary>
/// The notification detail for a condition: the guide a case of that condition
/// is exported under. One row per condition, so a condition is exported under
/// one guide.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>condition_detail</c> table, as described
/// in <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c>, which maps a
/// condition to its guide. The legacy table also carries the CDC-disease-code
/// ruleset a later session wires; this session keeps it to the guide
/// assignment the mapping reads.
///
/// <see cref="ConditionId"/> is the key — a condition has one detail row — and
/// both it and <see cref="GuideId"/> are bare ids, into <c>conditions</c> and
/// <c>guide</c>, held as plain ints and not navigations, as the existing
/// reference entities are. There is no behaviour here and no navigation out.
/// </remarks>
public class ConditionDetail
{
    /// <summary>
    /// The condition this detail is for, as a bare id into <c>conditions</c>.
    /// The primary key, so a condition is exported under one guide.
    /// </summary>
    public int ConditionId { get; set; }

    /// <summary>
    /// The guide the condition is exported under, as a bare id into
    /// <c>guide</c>.
    /// </summary>
    public int GuideId { get; set; }
}
