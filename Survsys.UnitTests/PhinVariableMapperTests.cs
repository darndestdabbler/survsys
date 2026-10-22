using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The mapper read over a small in-memory catalogue: a condition whose guide
/// carries variables, a guide that links another and so carries both guides'
/// variables, a variable two guides share, a membership naming a variable the
/// catalogue does not define, a guide that carries nothing, and a condition
/// with no assigned guide — the last three yielding no variables rather than
/// throwing.
/// </summary>
public class PhinVariableMapperTests
{
    // Guide 1 carries INV100 and INV200 and links nothing (condition 10).
    // Guide 3 carries INV100 and SHARED and links guide 2, which carries
    // STD300 and SHARED (condition 30) -- so SHARED is carried by both the
    // guide and its linked guide. Guide 4 carries a defined variable and
    // GHOST, which the catalogue does not define (condition 40). Guide 9
    // carries nothing (condition 90). Condition 50 has no guide at all.
    private static PhinVariableMapper Mapper() => new(new PhinVariableCatalog(
        Variables:
        [
            new PhinVariable("INV200", "Second investigation variable", "CWE", "P", "PHINQUESTION"),
            new PhinVariable("INV100", "First investigation variable", "ST", "P", "PHINQUESTION"),
            new PhinVariable("STD300", "STD variable", "CWE", "P", "SCT"),
            new PhinVariable("SHARED", "Shared variable", "CWE", "P", "SCT"),
        ],
        ConditionGuides:
        [
            new ConditionGuide(ConditionId: 10, GuideId: 1),
            new ConditionGuide(ConditionId: 30, GuideId: 3),
            new ConditionGuide(ConditionId: 40, GuideId: 4),
            new ConditionGuide(ConditionId: 90, GuideId: 9),
        ],
        GuideVariables:
        [
            new GuideVariable(GuideId: 1, PhinVar: "INV200"),
            new GuideVariable(GuideId: 1, PhinVar: "INV100"),
            new GuideVariable(GuideId: 3, PhinVar: "INV100"),
            new GuideVariable(GuideId: 3, PhinVar: "SHARED"),
            new GuideVariable(GuideId: 2, PhinVar: "STD300"),
            new GuideVariable(GuideId: 2, PhinVar: "SHARED"),
            new GuideVariable(GuideId: 4, PhinVar: "INV100"),
            new GuideVariable(GuideId: 4, PhinVar: "GHOST"),
        ],
        GuideLinks:
        [
            new GuideLink(GuideId: 3, LinkedGuideId: 2),
        ]));

    [Fact]
    public void ConditionResolvesToExactlyItsGuidesVariablesOrderedByIdentifier()
    {
        var variables = Mapper().MapForCondition(conditionId: 10);

        Assert.Equal(["INV100", "INV200"], variables.Select(variable => variable.PhinVar));
    }

    [Fact]
    public void LinkedGuideVariablesAreUnionedWithTheGuidesOwn()
    {
        var variables = Mapper().MapForCondition(conditionId: 30);

        // Guide 3's INV100 and SHARED, together with linked guide 2's STD300,
        // ordered by identifier.
        Assert.Equal(["INV100", "SHARED", "STD300"], variables.Select(variable => variable.PhinVar));
    }

    [Fact]
    public void AVariableCarriedByAGuideAndItsLinkedGuideAppearsOnce()
    {
        var variables = Mapper().MapForCondition(conditionId: 30);

        // SHARED is carried by both guide 3 and the guide it links, guide 2.
        Assert.Single(variables, variable => variable.PhinVar == "SHARED");
    }

    [Fact]
    public void AMembershipNamingAnUndefinedVariableIsSkipped()
    {
        var variables = Mapper().MapForCondition(conditionId: 40);

        // Guide 4 carries INV100 and GHOST; GHOST has no definition, so only
        // INV100 comes through and nothing throws.
        Assert.Equal(["INV100"], variables.Select(variable => variable.PhinVar));
    }

    [Fact]
    public void AConditionWithNoAssignedGuideResolvesToNoVariables()
    {
        var variables = Mapper().MapForCondition(conditionId: 50);

        Assert.Empty(variables);
    }

    [Fact]
    public void AGuideThatCarriesNoVariablesResolvesToNoVariables()
    {
        var variables = Mapper().MapForCondition(conditionId: 90);

        Assert.Empty(variables);
    }
}
