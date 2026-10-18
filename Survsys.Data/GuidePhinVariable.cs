namespace Survsys.Data;

/// <summary>
/// A PHIN variable a guide carries: the association that says a given
/// notification guide's message includes a given <see cref="PhinVariable"/>.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>guide_phin_var</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: a variable belongs to
/// a guide, and a variable can belong to many guides. Keyed by the pair, so a
/// guide carries a variable once.
///
/// <see cref="GuideId"/> and <see cref="PhinVar"/> are bare ids, into
/// <c>guide</c> and <c>phin_var</c>, held as a plain int and string and not
/// navigations, as the existing reference entities are. There is no behaviour
/// here and no navigation out.
/// </remarks>
public class GuidePhinVariable
{
    /// <summary>The guide, as a bare id into <c>guide</c>.</summary>
    public int GuideId { get; set; }

    /// <summary>
    /// The PHIN variable the guide carries, as a bare identifier into
    /// <c>phin_var</c>.
    /// </summary>
    public required string PhinVar { get; set; }
}
