using Microsoft.Extensions.Configuration;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which of an address's county and county equivalent each program reports.
/// </summary>
public class CountyProgramsTests
{
    private static readonly PersonAddress Hartford = new() { County = "09003", CountyEquivalent = "09110" };

    [Fact]
    public void DefaultsAreTheCountyForElrAndEcrAndTheEquivalentForCdc()
    {
        var programs = new CountyPrograms();

        Assert.Equal("09003", programs.Pick(CountyPrograms.ElrProgram, Hartford));
        Assert.Equal("09003", programs.Pick(CountyPrograms.EcrProgram, Hartford));
        Assert.Equal("09110", programs.Pick(CountyPrograms.CdcNotificationProgram, Hartford));
    }

    [Fact]
    public void BindsEachProgramByItsName()
    {
        var programs = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CountyPrograms:elr"] = "county-equivalent",
                ["CountyPrograms:cdc-notification"] = "county",
            })
            .Build()
            .GetSection(CountyPrograms.SectionName)
            .Get<CountyPrograms>()!;

        Assert.True(programs.UsesCountyEquivalent(CountyPrograms.ElrProgram));
        Assert.False(programs.UsesCountyEquivalent(CountyPrograms.EcrProgram));
        Assert.Equal("09003", programs.Pick(CountyPrograms.CdcNotificationProgram, Hartford));
    }

    [Theory]
    [InlineData("county", "09003")]
    [InlineData("county-equivalent", "09110")]
    public void ANotificationCarriesTheFipsCodeOfTheValueItsSettingNames(string setting, string expected)
    {
        var fips = CodeValueSeed.Rows
            .Where(row => row.List == "county" && row.Fips != null)
            .ToDictionary(row => row.Code, row => row.Fips!);
        var county = new CountyPrograms { CdcNotification = setting }.NotificationCounty(Hartford);

        Assert.Equal(expected, OruR01GeneratorTests.CountyInPid(county, fips));
    }

    [Fact]
    public void RefusesAnUnknownProgramOrSetting()
    {
        var programs = new CountyPrograms { Elr = "region" };

        Assert.Throws<ArgumentOutOfRangeException>(() => programs.UsesCountyEquivalent("vaers"));
        Assert.Throws<InvalidOperationException>(() => programs.UsesCountyEquivalent(CountyPrograms.ElrProgram));
    }
}
