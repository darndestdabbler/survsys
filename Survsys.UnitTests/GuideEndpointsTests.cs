using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the guide endpoints refuse before the database is asked: a guide with
/// no name, a header variable given twice, a variable default with no name,
/// and a guide's JSON with a bad variable, a rule set with no type, or a
/// variable given twice.
/// </summary>
public class GuideEndpointsTests
{
    private static readonly PhinVariableRow Variable =
        new("INV163", "Case Status", null, null, "CWE", null, null, null, null, "SCT");

    private static readonly RuleSetConfigRow RuleSet =
        new(0, "string", null, false, ["case_status"], [], [], []);

    [Fact]
    public void WhyNotAGuideNeedsANameAndAShortVersion()
    {
        Assert.Null(GuideEndpoints.WhyNotAGuide(new GuideRow(0, "Arbo", "Arbo_Case_Map_v1.0")));
        Assert.Contains("name", GuideEndpoints.WhyNotAGuide(new GuideRow(0, " ", null)));
        Assert.Contains("version", GuideEndpoints.WhyNotAGuide(new GuideRow(0, "Arbo", new string('v', 101))));
    }

    [Fact]
    public void WhyNotGuideVariablesRefusesAVariableGivenTwice()
    {
        Assert.Null(GuideEndpoints.WhyNotGuideVariables([new(1, null, "X^1^ISO", null), new(2, null, null, null)]));
        Assert.Contains("twice", GuideEndpoints.WhyNotGuideVariables([new(1, null, "X", null), new(1, null, "Y", null)]));
    }

    [Fact]
    public void WhyNotAGuideVariableDefaultNeedsAName()
    {
        Assert.Null(GuideEndpoints.WhyNotAGuideVariableDefault(new GuideVariableDefaultRow(0, "sending_app", "A^1^ISO")));
        Assert.Contains("name", GuideEndpoints.WhyNotAGuideVariableDefault(new GuideVariableDefaultRow(0, "", "A")));
    }

    [Fact]
    public void WhyNotAGuideExportAcceptsOne()
    {
        Assert.Null(GuideEndpoints.WhyNotAGuideExport(
            new GuideExport("Generic", "v2.0", [new(Variable, "fieldValueMap", RuleSet), new(Variable with { PhinVar = "NOT116" }, null, null)])));
    }

    [Fact]
    public void WhyNotAGuideExportRefusesABadVariableARuleSetWithNoTypeAndAVariableGivenTwice()
    {
        Assert.Contains("description", GuideEndpoints.WhyNotAGuideExport(
            new GuideExport("Generic", null, [new(Variable with { Description = "" }, null, null)])));
        Assert.Contains("no rule set type", GuideEndpoints.WhyNotAGuideExport(
            new GuideExport("Generic", null, [new(Variable, null, RuleSet)])));
        Assert.Contains("twice", GuideEndpoints.WhyNotAGuideExport(
            new GuideExport("Generic", null, [new(Variable, null, null), new(Variable, null, null)])));
    }
}
