namespace Survsys.Contracts;

/// <summary>One comparison of a rule set expression.</summary>
/// <param name="Field">The field it reads, or null.</param>
/// <param name="Operator">The comparison: <c>=</c>, <c>&lt;&gt;</c>, <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c> or <c>&lt;=</c>; or null.</param>
/// <param name="Value">The value it compares against, or null.</param>
/// <param name="AndOr"><c>AND</c> to continue the current group, <c>OR</c> to start a new one; or null.</param>
/// <param name="ValueIsField">Whether <paramref name="Value"/> names another field rather than a literal.</param>
public record RuleSetExpressionConditionRow(
    string? Field,
    string? Operator,
    string? Value,
    string? AndOr,
    bool ValueIsField);
