namespace Survsys.Data;

/// <summary>
/// A configured instance of a ruleset: the parameters one PHIN variable's
/// transformation is driven by. The fields, data parameters, value maps and
/// expressions that complete it hang off its id in their own tables.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_config</c> table, as described
/// in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>, kept to what a
/// configuration holds in its own right: the format type its value is produced
/// in, the value set coded outputs resolve through, and whether the variable
/// repeats. Which ruleset type this is is carried on the <c>phin_var</c> row
/// (its <c>rule_set_id</c>), not here.
///
/// The child tables — <see cref="RuleSetField"/>, <see cref="RuleSetData"/>,
/// <see cref="RuleSetValueMap"/> and <see cref="RuleSetExpression"/> — reach
/// this configuration by a bare <c>rule_set_config_id</c>, not a navigation,
/// as the existing reference entities do. There is no behaviour here.
/// </remarks>
public class RuleSetConfig
{
    /// <summary>The configuration's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The format the value is produced in (for example <c>string</c>,
    /// <c>date</c>, <c>phinValueSet</c>), where set.
    /// </summary>
    public string? FormatType { get; set; }

    /// <summary>
    /// The name of the value set coded outputs are resolved through, where the
    /// format type needs one.
    /// </summary>
    public string? ValueSetName { get; set; }

    /// <summary>
    /// Whether the variable repeats: when set, every formatted response is
    /// returned; when clear, only the first non-empty one.
    /// </summary>
    public bool Repeating { get; set; }
}
