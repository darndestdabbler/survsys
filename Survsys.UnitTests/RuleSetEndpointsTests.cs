using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the rule set endpoints refuse before the database is asked: a blank
/// field or parameter name, a parameter given twice, an unknown operator or
/// and/or, and a value longer than its column.
/// </summary>
public class RuleSetEndpointsTests
{
    private static readonly RuleSetConfigRow RuleSet = new(
        0,
        "phinValueSet",
        "PHVS_YesNoUnknown_CDC",
        false,
        ["hospitalized"],
        [new RuleSetDataRow("dataSet", "Lab")],
        [new RuleSetValueMapRow("true", "Y", null), new RuleSetValueMapRow("default_value", "UNK", null)],
        [new RuleSetExpressionRow("Y", null, 1, [new RuleSetExpressionConditionRow("hospitalized", "=", "true", null, false)])]);

    [Fact]
    public void WhyNotARuleSetAcceptsOne()
    {
        Assert.Null(RuleSetEndpoints.WhyNotARuleSet(RuleSet));
        Assert.Null(RuleSetEndpoints.WhyNotARuleSet(new RuleSetConfigRow(0, null, null, false, [], [], [], [])));
    }

    [Fact]
    public void WhyNotARuleSetRefusesABlankFieldOrParameterName()
    {
        Assert.Contains("field 1", RuleSetEndpoints.WhyNotARuleSet(RuleSet with { Fields = [" "] }));
        Assert.Contains("name", RuleSetEndpoints.WhyNotARuleSet(RuleSet with { Data = [new RuleSetDataRow("", "x")] }));
    }

    [Fact]
    public void WhyNotARuleSetRefusesAParameterGivenTwice()
    {
        var why = RuleSetEndpoints.WhyNotARuleSet(
            RuleSet with { Data = [new RuleSetDataRow("dataSet", "Lab"), new RuleSetDataRow("dataSet", "Vaccine")] });

        Assert.Contains("twice", why);
    }

    [Theory]
    [InlineData("LIKE", null, "operator")]
    [InlineData("=", "XOR", "and/or")]
    [InlineData(">=", "or", null)]
    public void WhyNotARuleSetKnowsTheOperatorsAndTheAndOr(string op, string? andOr, string? expected)
    {
        var why = RuleSetEndpoints.WhyNotARuleSet(RuleSet with
        {
            Expressions = [new RuleSetExpressionRow("Y", null, 1, [new RuleSetExpressionConditionRow("age", op, "18", andOr, false)])],
        });

        if (expected is null)
        {
            Assert.Null(why);
        }
        else
        {
            Assert.Contains(expected, why);
        }
    }

    [Fact]
    public void WhyNotARuleSetRefusesAValueLongerThanItsColumn()
    {
        Assert.Contains("longer", RuleSetEndpoints.WhyNotARuleSet(
            RuleSet with { ValueMaps = [new RuleSetValueMapRow(new string('x', 501), "Y", null)] }));
        Assert.Contains("longer", RuleSetEndpoints.WhyNotARuleSet(RuleSet with { FormatType = new string('x', 51) }));
    }
}
