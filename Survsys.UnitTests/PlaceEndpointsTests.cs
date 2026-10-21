using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the town and zip code endpoints decide without the database: what a
/// row to save must be, and each table's CSV read back as it was written.
/// </summary>
public class PlaceEndpointsTests
{
    [Fact]
    public void WhyNotATownAcceptsATownWithItsCountyAndRegion()
    {
        Assert.Null(PlaceEndpoints.WhyNotATown(new CtTownRow("Andover", "09013", "09110")));
    }

    [Theory]
    [InlineData("", "09013", "09110", "town is required")]
    [InlineData("Andover", " ", "09110", "county is required")]
    [InlineData("Andover", "09013", "", "planningRegion is required")]
    public void WhyNotATownRefusesABlankField(string town, string county, string region, string expected)
    {
        Assert.Equal($"{expected}.", PlaceEndpoints.WhyNotATown(new CtTownRow(town, county, region)));
    }

    [Fact]
    public void WhyNotATownRefusesATownOverFiftyCharacters()
    {
        Assert.Contains(
            "town is longer than 50",
            PlaceEndpoints.WhyNotATown(new CtTownRow(new string('x', 51), "09013", "09110")));
    }

    [Theory]
    [InlineData("06101", null)]
    [InlineData("06101-1234", "09003")]
    public void WhyNotAZipCodeAcceptsAZipWithOrWithoutACounty(string code, string? county)
    {
        Assert.Null(PlaceEndpoints.WhyNotAZipCode(new ZipCodeRow(code, county)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0610")]
    [InlineData("ABCDE")]
    public void WhyNotAZipCodeRefusesAValueThatIsNotAZip(string code)
    {
        Assert.Contains("is not a zip code", PlaceEndpoints.WhyNotAZipCode(new ZipCodeRow(code, null)));
    }

    [Fact]
    public void ATownReadsBackFromItsCsvRow()
    {
        var town = new CtTownRow("North Stonington", "09011", "09180");

        var (rows, refused) = ReferenceCsv.Read(
            ReferenceCsv.Write(PlaceEndpoints.TownColumns, [PlaceEndpoints.Fields(town)]), PlaceEndpoints.TownColumns);

        Assert.Empty(refused);
        Assert.Equal(town, PlaceEndpoints.Town(Assert.Single(rows)));
    }

    [Fact]
    public void AZipCodeReadsBackFromItsCsvRowAsItsFiveDigits()
    {
        var (rows, _) = ReferenceCsv.Read("zip_code,county\r\n06101-1234,09003\r\n06102,\r\n", PlaceEndpoints.ZipCodeColumns);

        Assert.Equal(
            [new ZipCodeRow("06101", "09003"), new ZipCodeRow("06102", null)],
            rows.Select(PlaceEndpoints.ZipCode));
    }
}
