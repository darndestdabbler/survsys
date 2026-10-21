namespace Survsys.Messaging;

/// <summary>What a condition's message filter makes of a message.</summary>
public enum FilterOutcome
{
    /// <summary>A filter rule matched: the message is recent enough to act on.</summary>
    Accepted,

    /// <summary>Filter rules exist and none matched: the message is filtered out.</summary>
    Filtered,

    /// <summary>The condition has no filter rule, so a person has to decide.</summary>
    NoRules,

    /// <summary>A filter rule reads a field the message does not carry, such as its collection date.</summary>
    MissingField,
}

/// <summary>What a condition's case-management rules make of a message.</summary>
public enum CaseManagementOutcome
{
    /// <summary>Exactly one rule matched, and its flags decide.</summary>
    Decided,

    /// <summary>The condition has no case-management rule.</summary>
    NoRules,

    /// <summary>Rules exist and none matched.</summary>
    NoneMatched,

    /// <summary>More than one rule matched, so the decision is ambiguous.</summary>
    MultipleMatched,
}

/// <summary>
/// A case-management rule: the conditions that decide whether it matches, and
/// what a message it matches may do.
/// </summary>
/// <param name="When">The conditions, evaluated by <see cref="ExpressionEvaluator"/>.</param>
/// <param name="AllowNewCase">Whether a matched message may open a new case.</param>
/// <param name="AllowUpdateCase">Whether a matched message may update an existing case.</param>
/// <param name="IsSurveillance">Whether the case a matched message opens is surveillance.</param>
public sealed record CaseManagementRule(
    ConditionExpression When,
    bool AllowNewCase,
    bool AllowUpdateCase,
    bool IsSurveillance);

/// <summary>
/// The case-management decision: the outcome and, when it is
/// <see cref="CaseManagementOutcome.Decided"/>, the one rule that matched.
/// </summary>
public sealed record CaseManagementDecision(CaseManagementOutcome Outcome, CaseManagementRule? Rule = null);

/// <summary>
/// The first two of the legacy's four rules engines, as
/// <c>../nedss/docs/business-logic/EMSA_RULES_ENGINE.md</c> describes them: the
/// message filter, which decides whether a message is recent enough to act on,
/// and the case-management rules, which decide whether it may open or update a
/// case and whether that case is surveillance.
/// </summary>
/// <remarks>
/// Both read the record intake builds for a result and evaluate each rule's
/// conditions through <see cref="ExpressionEvaluator"/>, in place of the
/// legacy's free-text filter and its JSON conditions turned into JavaScript.
/// The legacy filter's "always accept" is a rule with no conditions, which the
/// evaluator matches unconditionally, and its "6 months" a condition such as
/// <c>DaysSinceCollection &lt; 183</c>. Pure and static, so the unit suite
/// proves every outcome without a database.
/// </remarks>
public static class IntakeRules
{
    /// <summary>
    /// The message filter over a condition's filter rules: no rules is
    /// <see cref="FilterOutcome.NoRules"/>; a rule reading a field the record
    /// lacks is <see cref="FilterOutcome.MissingField"/>, the legacy's missing
    /// reference date; any rule matching accepts; and none matching filters.
    /// </summary>
    public static FilterOutcome Filter(IReadOnlyList<ConditionExpression> rules, EpiTraxRecord record)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(record);

        if (rules.Count == 0)
        {
            return FilterOutcome.NoRules;
        }

        var missing = rules
            .SelectMany(rule => rule.Conditions)
            .Any(condition => condition.Field is { } field && string.IsNullOrWhiteSpace(record.Value(field)));
        if (missing)
        {
            return FilterOutcome.MissingField;
        }

        return rules.Any(rule => ExpressionEvaluator.Matches(rule, record))
            ? FilterOutcome.Accepted
            : FilterOutcome.Filtered;
    }

    /// <summary>
    /// The case-management decision over a condition's case-management rules.
    /// Exactly one rule must match — the legacy's core constraint, kept: none
    /// defined, none matching and more than one matching are each an outcome
    /// of their own rather than a guess.
    /// </summary>
    public static CaseManagementDecision CaseManagement(
        IReadOnlyList<CaseManagementRule> rules, EpiTraxRecord record)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(record);

        if (rules.Count == 0)
        {
            return new CaseManagementDecision(CaseManagementOutcome.NoRules);
        }

        var matched = rules.Where(rule => ExpressionEvaluator.Matches(rule.When, record)).Take(2).ToList();

        return matched.Count switch
        {
            0 => new CaseManagementDecision(CaseManagementOutcome.NoneMatched),
            1 => new CaseManagementDecision(CaseManagementOutcome.Decided, matched[0]),
            _ => new CaseManagementDecision(CaseManagementOutcome.MultipleMatched),
        };
    }
}
