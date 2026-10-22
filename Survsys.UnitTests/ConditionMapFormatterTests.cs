using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The condition-map formatter: an expression matching through an AND group,
/// the right expression selected when a later one matches through an OR group,
/// and the configured default value when no expression matches.
/// </summary>
public class ConditionMapFormatterTests
{
    private static LoadedRuleSet Rule(
        IReadOnlyList<ConditionExpression> expressions,
        IReadOnlyDictionary<string, string?>? data = null)
        => new(
            TypeLabel: "conditionMap",
            FormatType: "text",
            Repeating: false,
            ValueSetName: null,
            Fields: [],
            Data: data ?? new Dictionary<string, string?>(),
            ValueMaps: [],
            Expressions: expressions,
            ValueSet: []);

    [Fact]
    public void AnExpressionMatchesWhenAllConditionsInItsAndGroupHold()
    {
        var expression = new ConditionExpression(
            Value: "onset",
            OtherSpecified: null,
            ExecutionOrder: 1,
            Conditions:
            [
                new ExpressionCondition("investigation_started", "=", "Yes", null, false),
                new ExpressionCondition("disease_onset_known", "=", "Yes", "AND", false),
            ]);
        var record = new EpiTraxRecord(new Dictionary<string, string?>
        {
            ["investigation_started"] = "Yes",
            ["disease_onset_known"] = "Yes",
        });

        var result = new ConditionMapFormatter().Format(Rule([expression]), record);

        Assert.Equal("onset", Assert.Single(result).Value);
    }

    [Fact]
    public void TheMatchingExpressionIsSelectedThroughAnOrGroup()
    {
        // First expression needs status = active (fails). The second matches
        // through its OR group: its first group (priority = 1) fails but its
        // second group (urgent = Yes) holds, so its value is the one returned.
        var first = new ConditionExpression(
            Value: "active",
            OtherSpecified: null,
            ExecutionOrder: 1,
            Conditions: [new ExpressionCondition("status", "=", "active", null, false)]);
        var second = new ConditionExpression(
            Value: "escalate",
            OtherSpecified: null,
            ExecutionOrder: 2,
            Conditions:
            [
                new ExpressionCondition("priority", "=", "1", null, false),
                new ExpressionCondition("urgent", "=", "Yes", "OR", false),
            ]);
        var record = new EpiTraxRecord(new Dictionary<string, string?>
        {
            ["status"] = "closed",
            ["priority"] = "3",
            ["urgent"] = "Yes",
        });

        var result = new ConditionMapFormatter().Format(Rule([first, second]), record);

        Assert.Equal("escalate", Assert.Single(result).Value);
    }

    [Fact]
    public void TheDefaultValueIsUsedWhenNoExpressionMatches()
    {
        var expression = new ConditionExpression(
            Value: "matched",
            OtherSpecified: null,
            ExecutionOrder: 1,
            Conditions: [new ExpressionCondition("status", "=", "active", null, false)]);
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["status"] = "closed" });

        var result = new ConditionMapFormatter().Format(
            Rule([expression], new Dictionary<string, string?> { ["defaultValue"] = "fallback" }),
            record);

        Assert.Equal("fallback", Assert.Single(result).Value);
    }
}
