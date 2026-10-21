using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// A condition's organism and test-type overrides: the default set, an
/// inclusion added to it, an exclusion taken out of it, and a rule nobody has
/// configured limiting nothing.
/// </summary>
public class LabOverridesTests
{
    private static DatasetRow Lab(int? organism, int? testType) =>
        new(new Dictionary<string, string?>
        {
            [LabOverrides.OrganismField] = organism?.ToString(),
            [LabOverrides.TestTypeField] = testType?.ToString(),
        });

    private static LabItemRule Rule(int[] defaults, params (int Id, bool Include)[] overrides) =>
        new(defaults.ToHashSet(), overrides.ToDictionary(item => item.Id, item => item.Include));

    [Fact]
    public void AllowsKeepsTheDefaultsAndTheInclusionsAndDropsTheExclusions()
    {
        var rule = Rule([1, 2], (2, false), (3, true));

        Assert.True(rule.Allows(1));
        Assert.False(rule.Allows(2));
        Assert.True(rule.Allows(3));
        Assert.False(rule.Allows(4));
        Assert.True(rule.Allows(null));
    }

    [Fact]
    public void AllowsEverythingButAnExclusionWhereThereAreNoDefaultsOrInclusions()
    {
        Assert.True(LabItemRule.None.Allows(7));
        Assert.True(Rule([], (8, false)).Allows(7));
        Assert.False(Rule([], (8, false)).Allows(8));
        Assert.False(Rule([], (8, true)).Allows(7));
    }

    [Fact]
    public void ApplyKeepsARowOnlyWhereBothItsOrganismAndItsTestTypeAreAllowed()
    {
        var labs = new[] { Lab(1, 10), Lab(2, 10), Lab(1, 11), Lab(null, null) };

        var kept = LabOverrides.Apply(labs, Rule([1]), Rule([], (11, false)));

        Assert.Equal([labs[0], labs[3]], kept);
    }
}
