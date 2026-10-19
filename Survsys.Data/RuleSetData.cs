namespace Survsys.Data;

/// <summary>
/// A key-value parameter of a ruleset configuration: one named setting the
/// configuration's transformation reads (for example <c>staticValue</c> or
/// <c>dateFormat</c>).
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_data</c> table, as described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the configuration it
/// belongs to, the parameter's name and its value. <see cref="RuleSetConfigId"/>
/// is a bare id into <c>rule_set_config</c>, not a navigation. There is no
/// behaviour here and no navigation out.
/// </remarks>
public class RuleSetData
{
    /// <summary>The parameter's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The configuration this parameter belongs to, as a bare id into
    /// <c>rule_set_config</c>.
    /// </summary>
    public int RuleSetConfigId { get; set; }

    /// <summary>The parameter's name.</summary>
    public required string DataName { get; set; }

    /// <summary>The parameter's value, where set.</summary>
    public string? DataValue { get; set; }
}
