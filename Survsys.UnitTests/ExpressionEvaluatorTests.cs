using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The expression evaluator: AND within a group requires every condition, an
/// OR flag starts a new group and any group matching makes the expression
/// match, each comparison operator behaves, and the value-is-field flag
/// compares two fields.
/// </summary>
public class ExpressionEvaluatorTests
{
    private static ConditionExpression Expression(params ExpressionCondition[] conditions) =>
        new(Value: "value", OtherSpecified: null, ExecutionOrder: 1, Conditions: conditions);

    private static EpiTraxRecord Record(params (string Field, string Value)[] fields) =>
        new(fields.ToDictionary(f => f.Field, f => (string?)f.Value));

    [Fact]
    public void AllConditionsInAnAndGroupMustMatch()
    {
        var expression = Expression(
            new ExpressionCondition("a", "=", "1", null, false),
            new ExpressionCondition("b", "=", "1", "AND", false));

        Assert.True(ExpressionEvaluator.Matches(expression, Record(("a", "1"), ("b", "1"))));
        Assert.False(ExpressionEvaluator.Matches(expression, Record(("a", "1"), ("b", "2"))));
    }

    [Fact]
    public void AnOrFlagStartsANewGroupAndAnyGroupMatching()
    {
        var expression = Expression(
            new ExpressionCondition("a", "=", "1", null, false),
            new ExpressionCondition("b", "=", "2", "OR", false));

        // First group (a = 1) fails, second group (b = 2) holds.
        Assert.True(ExpressionEvaluator.Matches(expression, Record(("a", "0"), ("b", "2"))));
        // Neither group holds.
        Assert.False(ExpressionEvaluator.Matches(expression, Record(("a", "0"), ("b", "0"))));
    }

    [Theory]
    [InlineData("5", "=", "5", true)]
    [InlineData("5", "=", "6", false)]
    [InlineData("5", "<>", "6", true)]
    [InlineData("5", "<>", "5", false)]
    [InlineData("10", ">", "9", true)]
    [InlineData("9", ">", "10", false)]
    [InlineData("9", "<", "10", true)]
    [InlineData("10", "<", "9", false)]
    [InlineData("10", ">=", "10", true)]
    [InlineData("9", ">=", "10", false)]
    [InlineData("10", "<=", "10", true)]
    [InlineData("11", "<=", "10", false)]
    public void EachOperatorCompares(string left, string @operator, string right, bool expected)
    {
        var expression = Expression(new ExpressionCondition("a", @operator, right, null, false));

        Assert.Equal(expected, ExpressionEvaluator.Matches(expression, Record(("a", left))));
    }

    [Fact]
    public void ValueIsFieldComparesTwoFields()
    {
        var expression = Expression(new ExpressionCondition("a", "=", "b", null, true));

        Assert.True(ExpressionEvaluator.Matches(expression, Record(("a", "5"), ("b", "5"))));
        Assert.False(ExpressionEvaluator.Matches(expression, Record(("a", "5"), ("b", "6"))));
    }
}
