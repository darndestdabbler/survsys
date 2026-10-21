using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The message filter and the case-management rules: every outcome each
/// gives, over records built the way intake builds them for a result.
/// </summary>
public class IntakeRulesTests
{
    private static ConditionExpression When(params ExpressionCondition[] conditions) =>
        new(Value: null, OtherSpecified: null, ExecutionOrder: 1, Conditions: conditions);

    private static ExpressionCondition Is(string field, string @operator, string value, string? andOr = null) =>
        new(field, @operator, value, andOr, ValueIsField: false);

    private static EpiTraxRecord Record(params (string Field, string? Value)[] fields) =>
        new(fields.ToDictionary(f => f.Field, f => f.Value));

    private static readonly ConditionExpression WithinSixMonths = When(Is("DaysSinceCollection", "<", "183"));

    [Fact]
    public void FilterWithNoRulesIsNoRules()
    {
        Assert.Equal(FilterOutcome.NoRules, IntakeRules.Filter([], Record(("DaysSinceCollection", "1"))));
    }

    [Fact]
    public void FilterAcceptsUnderARuleWithNoConditions()
    {
        // The legacy "always accept".
        Assert.Equal(FilterOutcome.Accepted, IntakeRules.Filter([When()], Record()));
    }

    [Theory]
    [InlineData("0", FilterOutcome.Accepted)]
    [InlineData("182", FilterOutcome.Accepted)]
    [InlineData("183", FilterOutcome.Filtered)]
    [InlineData("400", FilterOutcome.Filtered)]
    public void FilterAcceptsARecentMessageAndFiltersAnOldOne(string days, FilterOutcome expected)
    {
        Assert.Equal(expected, IntakeRules.Filter([WithinSixMonths], Record(("DaysSinceCollection", days))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FilterReadingAFieldTheRecordLacksIsMissingField(string? days)
    {
        Assert.Equal(
            FilterOutcome.MissingField,
            IntakeRules.Filter([WithinSixMonths], Record(("DaysSinceCollection", days))));
        Assert.Equal(FilterOutcome.MissingField, IntakeRules.Filter([WithinSixMonths], Record()));
    }

    [Fact]
    public void FilterAcceptsWhenAnyOfSeveralRulesMatches()
    {
        var rules = new[] { When(Is("Specimen", "=", "Blood")), WithinSixMonths };

        Assert.Equal(
            FilterOutcome.Accepted,
            IntakeRules.Filter(rules, Record(("Specimen", "Urine"), ("DaysSinceCollection", "10"))));
    }

    private static readonly CaseManagementRule Positive = new(
        When(Is("TestResult", "=", "Positive")), AllowNewCase: true, AllowUpdateCase: true, IsSurveillance: false);

    private static readonly CaseManagementRule Negative = new(
        When(Is("TestResult", "=", "Negative")), AllowNewCase: false, AllowUpdateCase: true, IsSurveillance: false);

    private static readonly CaseManagementRule HighTiter = new(
        When(Is("TestResult", "=", "Positive"), Is("ResultValue", ">", "64", "AND")),
        AllowNewCase: true,
        AllowUpdateCase: false,
        IsSurveillance: true);

    [Fact]
    public void CaseManagementWithNoRulesIsNoRules()
    {
        var decision = IntakeRules.CaseManagement([], Record(("TestResult", "Positive")));

        Assert.Equal(CaseManagementOutcome.NoRules, decision.Outcome);
        Assert.Null(decision.Rule);
    }

    [Fact]
    public void CaseManagementDecidesByTheOneRuleThatMatches()
    {
        var decision = IntakeRules.CaseManagement([Positive, Negative], Record(("TestResult", "Negative")));

        Assert.Equal(CaseManagementOutcome.Decided, decision.Outcome);
        Assert.Same(Negative, decision.Rule);
    }

    [Fact]
    public void CaseManagementWithNoRuleMatchingIsNoneMatched()
    {
        var decision = IntakeRules.CaseManagement([Positive, Negative], Record(("TestResult", "Equivocal")));

        Assert.Equal(CaseManagementOutcome.NoneMatched, decision.Outcome);
        Assert.Null(decision.Rule);
    }

    [Fact]
    public void CaseManagementWithMoreThanOneRuleMatchingIsMultipleMatched()
    {
        var decision = IntakeRules.CaseManagement(
            [Positive, HighTiter], Record(("TestResult", "Positive"), ("ResultValue", "128")));

        Assert.Equal(CaseManagementOutcome.MultipleMatched, decision.Outcome);
        Assert.Null(decision.Rule);
    }
}
