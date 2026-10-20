using Microsoft.Extensions.Configuration;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The county equivalent's label: "Planning region" unless configuration
/// names another.
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
}
