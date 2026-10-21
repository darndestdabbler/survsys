namespace Survsys.Contracts;

/// <summary>
/// One of a condition's intake rules, as <c>/api/conditions/{id}/rules</c>
/// reads and replaces them: a message-filter rule, which accepts a message
/// when it matches, or a case-management rule, whose flags decide what a
/// message it matches may do.
/// </summary>
/// <param name="Id">The rule's identifier; ignored when the rules are replaced.</param>
/// <param name="Kind">The kind: <c>message_filter</c> or <c>case_management</c>.</param>
/// <param name="ExecutionOrder">The order the rule is listed and evaluated in.</param>
/// <param name="AllowNewCase">Whether a matched message may open a new case.</param>
/// <param name="AllowUpdateCase">Whether a matched message may update an existing case.</param>
/// <param name="IsSurveillance">Whether the case a matched message opens is surveillance.</param>
/// <param name="Conditions">The conditions; none matches every message.</param>
public record VocabRuleRow(
    int Id,
    string Kind,
    int ExecutionOrder,
    bool AllowNewCase,
    bool AllowUpdateCase,
    bool IsSurveillance,
    IReadOnlyList<VocabRuleConditionRow> Conditions);
