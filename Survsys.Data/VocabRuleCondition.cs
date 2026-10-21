namespace Survsys.Data;

/// <summary>
/// A condition within a <see cref="VocabRule"/>: one comparison of a field of
/// the message with a value that, with its siblings, decides whether the rule
/// matches.
/// </summary>
/// <remarks>
/// The shape <see cref="RuleSetExpressionCondition"/> has, so the intake rules
/// are evaluated by the same <c>ExpressionEvaluator</c>: an <c>OR</c> flag
/// starts a new group and an <c>AND</c> (or none) continues the current one.
/// The field is one intake builds for the result — <c>Loinc</c>,
/// <c>TestResult</c>, <c>ResultValue</c>, <c>Organism</c>, <c>Specimen</c> or
/// <c>DaysSinceCollection</c>. Conditions are evaluated in id order.
/// <see cref="VocabRuleId"/> is a bare id into <c>vocab_rules</c>. There is no
/// behaviour here and no navigation out.
/// </remarks>
public class VocabRuleCondition
{
    public int Id { get; set; }

    /// <summary>The rule this condition belongs to, as a bare id into <c>vocab_rules</c>.</summary>
    public int VocabRuleId { get; set; }

    /// <summary>The field of the message this condition reads.</summary>
    public required string Field { get; set; }

    /// <summary>The comparison operator (<c>=</c>, <c>&lt;&gt;</c>, <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c>, <c>&lt;=</c>).</summary>
    public required string Operator { get; set; }

    /// <summary>The value the field is compared with, where set.</summary>
    public string? Value { get; set; }

    /// <summary>
    /// The and/or flag that groups this condition: <c>OR</c> starts a new
    /// group, <c>AND</c> continues the current one.
    /// </summary>
    public string? AndOr { get; set; }
}
