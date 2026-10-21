namespace Survsys.Data;

/// <summary>
/// A condition within a ruleset expression: one comparison that, with its
/// siblings, decides whether the expression matches. The conditions of an
/// expression are parsed into groups by their <see cref="AndOr"/> flag.
/// </summary>
/// <remarks>
/// The columns are the legacy NMI <c>rule_set_expression_condition</c> table,
/// as described in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the
/// expression it belongs to, the field it reads, the operator and value it
/// compares with, the and/or flag that groups it, and whether the value names
/// another field rather than a literal. <see cref="RuleSetExpressionId"/> is a
/// bare id into <c>rule_set_expression</c>, not a navigation. There is no
/// behaviour here and no navigation out.
/// </remarks>
public class RuleSetExpressionCondition
{
    /// <summary>The condition's id. The key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// The expression this condition belongs to, as a bare id into
    /// <c>rule_set_expression</c>.
    /// </summary>
    public int RuleSetExpressionId { get; set; }

    /// <summary>The EpiTrax field this condition reads, where set.</summary>
    public string? Field { get; set; }

    /// <summary>
    /// The comparison operator (for example <c>=</c>, <c>&lt;&gt;</c>,
    /// <c>&gt;=</c>), where set.
    /// </summary>
    public string? Operator { get; set; }

    /// <summary>The value this condition compares the field against, where set.</summary>
    public string? Value { get; set; }

    /// <summary>
    /// The and/or flag that groups this condition: <c>OR</c> starts a new
    /// group, <c>AND</c> continues the current one.
    /// </summary>
    public string? AndOr { get; set; }

    /// <summary>
    /// Whether <see cref="Value"/> names another field to compare against
    /// rather than a literal value.
    /// </summary>
    public bool ValueIsField { get; set; }
}
