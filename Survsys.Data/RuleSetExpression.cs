namespace Survsys.Data;

/// <summary>
/// A conditional expression of a ruleset configuration: one ordered rule that
/// contributes a value when its conditions match. Its conditions hang off its
/// id in <see cref="RuleSetExpressionCondition"/>.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_expression</c> table, as
/// described in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the
/// configuration it belongs to, the value it contributes, an other-specified
/// text, and the order it is evaluated in. <see cref="RuleSetConfigId"/> is a
/// bare id into <c>rule_set_config</c>, not a navigation. There is no
/// behaviour here and no navigation out.
/// </remarks>
public class RuleSetExpression
{
    /// <summary>The expression's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The configuration this expression belongs to, as a bare id into
    /// <c>rule_set_config</c>.
    /// </summary>
    public int RuleSetConfigId { get; set; }

    /// <summary>The value this expression contributes when it matches, where set.</summary>
    public string? Value { get; set; }

    /// <summary>The "other, specify" text this expression carries, where set.</summary>
    public string? OtherSpecified { get; set; }

    /// <summary>The order this expression is evaluated in, ascending.</summary>
    public int ExecutionOrder { get; set; }
}
