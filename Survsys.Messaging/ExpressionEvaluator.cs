using System.Globalization;

namespace Survsys.Messaging;

/// <summary>
/// The condition engine the <c>conditionMap</c> and <c>fieldValueMap</c>
/// rulesets evaluate their expressions through: it parses an expression's
/// conditions into groups and decides whether the expression matches a record.
/// </summary>
/// <remarks>
/// The grouping follows the legacy NMI logic described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: reading the conditions
/// in order, an <c>OR</c> flag starts a new group and an <c>AND</c> flag (or
/// none) continues the current one. All conditions in a group must match, and
/// any group matching makes the expression match — AND within a group, OR
/// between groups. A condition compares the record's field value with a literal,
/// or with another field's value when its value-is-field flag is set, using the
/// six standard operators; <c>&gt;</c>, <c>&lt;</c>, <c>&gt;=</c> and <c>&lt;=</c>
/// compare numerically when both sides are numbers and otherwise by ordinal text.
/// An expression with no conditions matches unconditionally.
/// </remarks>
public static class ExpressionEvaluator
{
    /// <summary>
    /// Whether <paramref name="expression"/> matches <paramref name="record"/>:
    /// any of its AND-groups has all its conditions matching.
    /// </summary>
    public static bool Matches(ConditionExpression expression, EpiTraxRecord record)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(record);

        var groups = GroupConditions(expression.Conditions);
        if (groups.Count == 0)
        {
            return true;
        }

        return groups.Any(group => group.All(condition => ConditionMatches(condition, record)));
    }

    private static List<List<ExpressionCondition>> GroupConditions(
        IReadOnlyList<ExpressionCondition> conditions)
    {
        var groups = new List<List<ExpressionCondition>>();
        List<ExpressionCondition>? current = null;

        foreach (var condition in conditions)
        {
            var startsGroup = current is null
                || string.Equals(condition.AndOr, "OR", StringComparison.OrdinalIgnoreCase);
            if (startsGroup)
            {
                current = [];
                groups.Add(current);
            }

            current!.Add(condition);
        }

        return groups;
    }

    private static bool ConditionMatches(ExpressionCondition condition, EpiTraxRecord record)
    {
        var left = condition.Field is null ? string.Empty : record.Value(condition.Field) ?? string.Empty;
        var right = condition.ValueIsField
            ? (condition.Value is null ? string.Empty : record.Value(condition.Value) ?? string.Empty)
            : condition.Value ?? string.Empty;

        return Compare(left, condition.Operator, right);
    }

    private static bool Compare(string left, string? @operator, string right)
    {
        switch (@operator)
        {
            case "=":
                return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
            case "<>":
                return !string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
            case ">":
                return CompareOrdered(left, right) > 0;
            case "<":
                return CompareOrdered(left, right) < 0;
            case ">=":
                return CompareOrdered(left, right) >= 0;
            case "<=":
                return CompareOrdered(left, right) <= 0;
            default:
                return false;
        }
    }

    private static int CompareOrdered(string left, string right)
    {
        if (double.TryParse(left, NumberStyles.Any, CultureInfo.InvariantCulture, out var leftNumber)
            && double.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
