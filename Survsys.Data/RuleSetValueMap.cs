namespace Survsys.Data;

/// <summary>
/// A value translation of a ruleset configuration: one <c>value_from</c> to
/// <c>value_to</c> mapping the transformation applies before it formats a
/// value (for example an EpiTrax answer to a CDC concept code).
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_value_map</c> table, as described
/// in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the configuration it
/// belongs to, the input and output values, and an other-specified text. The
/// special <c>null_value</c> and <c>default_value</c> fallbacks are ordinary
/// rows whose <see cref="ValueFrom"/> carries those sentinels.
/// <see cref="RuleSetConfigId"/> is a bare id into <c>rule_set_config</c>, not
/// a navigation. There is no behaviour here and no navigation out.
/// </remarks>
public class RuleSetValueMap
{
    /// <summary>The value map's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The configuration this value map belongs to, as a bare id into
    /// <c>rule_set_config</c>.
    /// </summary>
    public int RuleSetConfigId { get; set; }

    /// <summary>The input value this row matches, where set.</summary>
    public string? ValueFrom { get; set; }

    /// <summary>The output value this row maps to, where set.</summary>
    public string? ValueTo { get; set; }

    /// <summary>The "other, specify" text this row carries, where set.</summary>
    public string? OtherSpecified { get; set; }
}
