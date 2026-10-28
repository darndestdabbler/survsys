using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The county list's seed: Connecticut's historical counties and planning
/// regions beside Utah's legacy codes; and the provider type's.
/// </summary>
public class CodeValueSeedTests
{
    private static IEnumerable<CodeValue> Counties =>
        CodeValueSeed.Rows.Where(row => row.List == "county");

    [Fact]
    public void ConnecticutHasEightCountiesAndNinePlanningRegions()
    {
        var connecticut = Counties.Where(row => row.State == "CT").ToList();

        Assert.Equal(
            ["09001", "09003", "09005", "09007", "09009", "09011", "09013", "09015"],
            connecticut.Where(row => row.Kind == CodeValue.CountyKind).Select(row => row.Code).Order());
        Assert.Equal(
            ["09110", "09120", "09130", "09140", "09150", "09160", "09170", "09180", "09190"],
            connecticut.Where(row => row.Kind == CodeValue.CountyEquivalentKind).Select(row => row.Code).Order());
        Assert.All(connecticut, row =>
        {
            Assert.Equal(row.Code, row.Fips);
            Assert.True(row.Active);
        });
    }

    [Fact]
    public void UtahCodesAreCountiesWithNoFips()
    {
        var utah = Counties.Where(row => row.State == "UT").ToList();

        Assert.Equal(29, utah.Count);
        Assert.All(utah, row =>
        {
            Assert.Equal(2, row.Code.Length);
            Assert.Equal(CodeValue.CountyKind, row.Kind);
            Assert.Null(row.Fips);
        });
        Assert.Contains(utah, row => row.Code == "SL" && row.Description == "Salt Lake");
    }

    [Fact]
    public void OutOfStateIsACountyOfNoState()
    {
        var outOfState = Assert.Single(Counties, row => row.Code == "OS");

        Assert.Null(outOfState.State);
        Assert.Null(outOfState.Fips);
        Assert.Equal(CodeValue.CountyKind, outOfState.Kind);
    }

    [Fact]
    public void TheProviderTypeIsTheLegacyClinician()
    {
        var clinician = Assert.Single(CodeValueSeed.Rows, row => row.List == "provider_type");

        Assert.Equal("clinician", clinician.Code);
        Assert.Equal("Clinician", clinician.Description);
        Assert.True(clinician.Active);
    }

    [Fact]
    public void OnlyCountiesCarryAStateFipsOrKind()
    {
        Assert.All(CodeValueSeed.Rows.Where(row => row.List != "county"), row =>
        {
            Assert.Null(row.State);
            Assert.Null(row.Fips);
            Assert.Null(row.Kind);
        });
    }
}
