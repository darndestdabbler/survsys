using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The HepLab dataset: the lab rows a hepatitis group's test types name, in
/// the group's sort order, each carrying its test group.
/// </summary>
public class HepatitisLabGroupingTests
{
    private static DatasetRow Lab(int? testType, string result) =>
        new(new Dictionary<string, string?>
        {
            [LabOverrides.TestTypeField] = testType?.ToString(),
            ["test_result"] = result,
        });

    [Fact]
    public void GroupKeepsTheGroupsTestTypesInSortOrderCarryingTheirTestGroup()
    {
        var labs = new[] { Lab(5, "Positive"), Lab(9, "Negative"), Lab(6, "Reactive"), Lab(null, "Positive"), Lab(5, "Negative") };
        HepLabTestType[] testTypes = [new(5, "HBsAg", 2), new(6, "anti-HBc IgM", 1)];

        var grouped = HepatitisLabGrouping.Group(labs, testTypes);

        Assert.Equal(["anti-HBc IgM", "HBsAg", "HBsAg"], grouped.Select(row => row.Value(HepatitisLabGrouping.TestGroupField)));
        Assert.Equal(["Reactive", "Positive", "Negative"], grouped.Select(row => row.Value("test_result")));
    }

    [Fact]
    public void GroupIsEmptyWhereTheGroupCarriesNoTestType()
    {
        Assert.Empty(HepatitisLabGrouping.Group([Lab(5, "Positive")], []));
    }
}
