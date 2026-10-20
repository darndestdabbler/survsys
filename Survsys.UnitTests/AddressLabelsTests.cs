using Microsoft.Extensions.Configuration;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The county equivalent's label: "Planning region" unless configuration
/// names another; and a county code shown by its name.
/// </summary>
public class AddressLabelsTests
{
    private static IConfiguration Configured(string? label) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [AddressLabels.CountyEquivalentKey] = label })
        .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void CountyEquivalentIsPlanningRegionWhereNoneIsConfigured(string? label)
    {
        Assert.Equal("Planning region", AddressLabels.CountyEquivalent(Configured(label)));
    }

    [Fact]
    public void CountyEquivalentIsTheConfiguredLabel()
    {
        Assert.Equal("County equivalent", AddressLabels.CountyEquivalent(Configured(" County equivalent ")));
    }

    private static readonly CodeResponse[] Counties =
    [
        new(1, CodeLists.County, "09003", "Hartford County", 0, false, []),
        new(2, CodeLists.County, "09110", "Capitol Planning Region", 0, true, []),
        new(3, CodeLists.County, "SL", null, 0, true, []),
    ];

    [Theory]
    [InlineData("09003", "Hartford County")]
    [InlineData("09110", "Capitol Planning Region")]
    [InlineData("SL", "SL")]
    [InlineData("09999", "09999")]
    [InlineData(null, null)]
    public void CountyNameIsTheCodesDescriptionOrTheCodeItself(string? code, string? expected)
    {
        Assert.Equal(expected, AddressLabels.CountyName(Counties, code));
    }
}
