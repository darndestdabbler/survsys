namespace Survsys.Data;

/// <summary>
/// One of the seven NMI ruleset types: the kind of transformation a PHIN
/// variable's value is produced by (for example <c>staticValue</c> or
/// <c>conditionMap</c>). A lookup of exactly seven rows.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: an id and the type's
/// label. The seven ids are fixed and stable — a <see cref="RuleSetConfig"/>
/// and a <c>phin_var</c> refer to a type by its id — so the id is assigned,
/// not generated, and the seven rows are seeded with the configuration.
///
/// There is no behaviour here and no navigation out; the evaluation that turns
/// a configured ruleset into a formatted value lives in Survsys.Messaging,
/// over in-memory data rather than this table.
/// </remarks>
public class RuleSet
{
    /// <summary>The ruleset type's id, one of the seven fixed values. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The type's label, as the legacy system names it (for example
    /// <c>fieldValueMap</c>).
    /// </summary>
    public required string Label { get; set; }
}
