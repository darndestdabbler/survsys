using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The core element visibility rule: the element's default, and which
/// override replaces it for a report's condition and agency.
/// </summary>
public class CoreElementVisibilityRulesTests
{
    private const int Condition = 10;
    private const int OtherCondition = 11;
    private const int Agency = 5;
    private const int OtherAgency = 6;

    private static readonly CoreElement Shown = new() { Id = 1, Uid = "food_handler", Label = "Food handler", Tab = "Investigation" };

    private static readonly CoreElement HiddenByDefault =
        new() { Id = 2, Uid = "group_living", Label = "Group living", Tab = "Investigation", Hidden = true };

    private static CoreElementOverride Override(int elementId, int? conditionId, int? agencyId, bool hidden) =>
        new() { CoreElementId = elementId, ConditionId = conditionId, AgencyId = agencyId, Hidden = hidden };

    [Fact]
    public void IsHiddenIsTheDefaultWhereNoOverrideApplies()
    {
        Assert.False(CoreElementVisibilityRules.IsHidden(Shown, Condition, Agency, []));
        Assert.True(CoreElementVisibilityRules.IsHidden(HiddenByDefault, Condition, Agency, []));
    }

    [Fact]
    public void IsHiddenTakesAConditionOverride()
    {
        var overrides = new[] { Override(Shown.Id, Condition, null, hidden: true) };

        Assert.True(CoreElementVisibilityRules.IsHidden(Shown, Condition, Agency, overrides));
    }

    [Fact]
    public void IsHiddenLetsAnAgencyOverrideBeatAConditionOverride()
    {
        var overrides = new[]
        {
            Override(Shown.Id, Condition, null, hidden: true),
            Override(Shown.Id, null, Agency, hidden: false),
        };

        Assert.False(CoreElementVisibilityRules.IsHidden(Shown, Condition, Agency, overrides));
    }

    [Fact]
    public void IsHiddenLetsAnAgencyAndConditionOverrideBeatAnAgencyOverride()
    {
        var overrides = new[]
        {
            Override(Shown.Id, null, Agency, hidden: false),
            Override(Shown.Id, Condition, Agency, hidden: true),
            Override(Shown.Id, Condition, null, hidden: false),
        };

        Assert.True(CoreElementVisibilityRules.IsHidden(Shown, Condition, Agency, overrides));
    }

    [Fact]
    public void IsHiddenLetsAnOverrideShowAnElementHiddenByDefault()
    {
        var overrides = new[] { Override(HiddenByDefault.Id, Condition, null, hidden: false) };

        Assert.False(CoreElementVisibilityRules.IsHidden(HiddenByDefault, Condition, Agency, overrides));
    }

    [Fact]
    public void IsHiddenIgnoresOverridesForAnotherConditionAgencyOrElement()
    {
        var overrides = new[]
        {
            Override(Shown.Id, OtherCondition, null, hidden: true),
            Override(Shown.Id, null, OtherAgency, hidden: true),
            Override(Shown.Id, Condition, OtherAgency, hidden: true),
            Override(Shown.Id, OtherCondition, Agency, hidden: true),
            Override(HiddenByDefault.Id, Condition, null, hidden: true),
        };

        Assert.False(CoreElementVisibilityRules.IsHidden(Shown, Condition, Agency, overrides));
    }

    [Fact]
    public void IsHiddenIgnoresAgencyOverridesForAReportWithNoAgency()
    {
        var overrides = new[]
        {
            Override(Shown.Id, null, Agency, hidden: true),
            Override(Shown.Id, Condition, Agency, hidden: true),
        };

        Assert.False(CoreElementVisibilityRules.IsHidden(Shown, Condition, null, overrides));
    }
}
