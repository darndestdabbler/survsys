namespace Survsys.Contracts;

/// <summary>A conditional expression of a rule set configuration, and the conditions it matches on.</summary>
/// <param name="Value">The value it contributes when it matches, or null.</param>
/// <param name="OtherSpecified">The "other, specify" text it carries, or null.</param>
/// <param name="ExecutionOrder">The order it is evaluated in, ascending.</param>
/// <param name="Conditions">Its conditions, in order; an <c>OR</c> starts a new group.</param>
public record RuleSetExpressionRow(
    string? Value,
    string? OtherSpecified,
    int ExecutionOrder,
    IReadOnlyList<RuleSetExpressionConditionRow> Conditions);
