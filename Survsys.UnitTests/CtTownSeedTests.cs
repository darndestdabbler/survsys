using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The Connecticut towns' seed: every town once, each in a county and a
/// planning region the county list holds.
/// </summary>
public class CtTownSeedTests
{
    private static readonly Dictionary<string, CodeValue> Counties = CodeValueSeed.Rows
        .Where(row => row.List == "county")
        .ToDictionary(row => row.Code);

    [Fact]
    public void HoldsEachOfThe169TownsOnce()
    {
        Assert.Equal(169, CtTownSeed.Rows.Select(row => row.Town).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(169, CtTownSeed.Rows.Length);
    }

    [Fact]
    public void EveryTownIsInAConnecticutCountyAndPlanningRegion()
    {
        Assert.All(CtTownSeed.Rows, row =>
        {
            Assert.Equal(CodeValue.CountyKind, Counties[row.County].Kind);
            Assert.Equal("CT", Counties[row.County].State);
            Assert.Equal(CodeValue.CountyEquivalentKind, Counties[row.PlanningRegion].Kind);
            Assert.Equal("CT", Counties[row.PlanningRegion].State);
        });
    }

    [Theory]
    [InlineData("Hartford", "09003", "09110")]
    [InlineData("Bridgeport", "09001", "09120")]
    [InlineData("Lyme", "09011", "09130")]
    [InlineData("Bristol", "09003", "09140")]
    [InlineData("Union", "09013", "09150")]
    [InlineData("Torrington", "09005", "09160")]
    [InlineData("New Haven", "09009", "09170")]
    [InlineData("Windham", "09015", "09180")]
    [InlineData("New Milford", "09005", "09190")]
    public void ATownHasItsCountyAndPlanningRegion(string town, string county, string region)
    {
        var row = Assert.Single(CtTownSeed.Rows, row => row.Town == town);

        Assert.Equal(county, row.County);
        Assert.Equal(region, row.PlanningRegion);
    }
}
