using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the hepatitis lab group endpoints refuse before the database is asked:
/// a group with no name, a test type with no test group, and a condition or
/// test type given twice.
/// </summary>
public class HepLabGroupEndpointsTests
{
    private static readonly HepLabGroupRow Group =
        new(0, "Hepatitis B", [12, 13], [new(5, "HBsAg", 1), new(6, "anti-HBc IgM", 2)]);

    [Fact]
    public void WhyNotAHepLabGroupAcceptsOne()
    {
        Assert.Null(HepLabGroupEndpoints.WhyNotAHepLabGroup(Group));
        Assert.Null(HepLabGroupEndpoints.WhyNotAHepLabGroup(new HepLabGroupRow(0, "Empty", [], [])));
    }

    [Fact]
    public void WhyNotAHepLabGroupNeedsANameAndATestGroupForEachTestType()
    {
        Assert.Contains("name", HepLabGroupEndpoints.WhyNotAHepLabGroup(Group with { Name = " " }));
        Assert.Contains("testGroupName", HepLabGroupEndpoints.WhyNotAHepLabGroup(Group with { TestTypes = [new(5, "", 1)] }));
    }

    [Fact]
    public void WhyNotAHepLabGroupRefusesAConditionOrTestTypeGivenTwice()
    {
        Assert.Contains("condition 12", HepLabGroupEndpoints.WhyNotAHepLabGroup(Group with { ConditionIds = [12, 12] }));
        Assert.Contains("test type 5", HepLabGroupEndpoints.WhyNotAHepLabGroup(
            Group with { TestTypes = [new(5, "HBsAg", 1), new(5, "HBeAg", 2)] }));
    }
}
