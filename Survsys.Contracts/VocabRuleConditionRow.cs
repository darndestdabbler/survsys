namespace Survsys.Contracts;

/// <summary>
/// A condition of a <see cref="VocabRuleRow"/>: one comparison of a field of
/// the message with a value.
/// </summary>
/// <param name="Field">
/// The field read: <c>Loinc</c>, <c>TestResult</c>, <c>ResultValue</c>,
/// <c>Organism</c>, <c>Specimen</c> or <c>DaysSinceCollection</c>.
/// </param>
/// <param name="Operator">The operator: <c>=</c>, <c>&lt;&gt;</c>, <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c> or <c>&lt;=</c>.</param>
/// <param name="Value">The value compared with, or null.</param>
/// <param name="AndOr"><c>OR</c> to start a new group, <c>AND</c> or null to continue the current one.</param>
public record VocabRuleConditionRow(
    string Field,
    string Operator,
    string? Value,
    string? AndOr);
