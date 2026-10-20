using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The address rules: how each field is made regular on save, and how a
/// person's addresses are left with one current.
/// </summary>
public class AddressRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Theory]
    [InlineData("  12  Main\t St ", "12 Main St")]
    [InlineData("Main", "Main")]
    public void TextTrimsAndCollapsesWhitespace(string value, string expected)
    {
        Assert.Equal(expected, AddressRules.Text(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TextIsNullWhereNothingWasSaid(string? value)
    {
        Assert.Null(AddressRules.Text(value));
    }

    [Theory]
    [InlineData("123 MAIN st", "123 Main St")]
    [InlineData("salt  lake city", "Salt Lake City")]
    public void TitledPutsTheTextInTitleCase(string value, string expected)
    {
        Assert.Equal(expected, AddressRules.Titled(value));
    }

    [Theory]
    [InlineData("UT", "UT")]
    [InlineData("ut", "UT")]
    [InlineData(" utah ", "UT")]
    [InlineData("New York", "NY")]
    [InlineData("Ontario", "Ontario")]
    public void StateCodeIsTheCodeOfTheStateNamed(string value, string expected)
    {
        Assert.Equal(expected, AddressRules.StateCode(value));
    }

    [Theory]
    [InlineData("84101", "84101")]
    [InlineData(" 84101 ", "84101")]
    [InlineData("841011234", "84101-1234")]
    [InlineData("84101 1234", "84101-1234")]
    [InlineData("84101-1234", "84101-1234")]
    [InlineData("8410", "8410")]
    [InlineData("K1A 0B1", "K1A 0B1")]
    public void PostalCodeIsFiveDigitsOrZipPlusFour(string value, string expected)
    {
        Assert.Equal(expected, AddressRules.PostalCode(value));
    }

    [Fact]
    public void NormalizeMakesEveryFieldRegular()
    {
        var address = new PersonAddress
        {
            Street = " 12  elm STREET ",
            UnitNumber = "apt 4",
            City = "OGDEN",
            State = "utah",
            PostalCode = "844011234",
            County = " wb ",
            CountyEquivalent = " x9 ",
            Note = "  side   door ",
        };

        AddressRules.Normalize(address);

        Assert.Equal("12 Elm Street", address.Street);
        Assert.Equal("Apt 4", address.UnitNumber);
        Assert.Equal("Ogden", address.City);
        Assert.Equal("UT", address.State);
        Assert.Equal("84401-1234", address.PostalCode);
        Assert.Equal("WB", address.County);
        Assert.Equal("X9", address.CountyEquivalent);
        Assert.Equal("side door", address.Note);
    }

    private static readonly Dictionary<string, string> CountiesByZip = new()
    {
        ["06103"] = "09009",
        ["84401"] = "WB",
    };

    private static readonly CtTown[] Towns =
    [
        new() { Town = "Windsor Locks", County = "09003", PlanningRegion = "09110" },
        new() { Town = "Hartford", County = "09003", PlanningRegion = "09110" },
    ];

    [Fact]
    public void FillConnecticutCountiesTakesBothFromTheTown()
    {
        var address = new PersonAddress { City = "Windsor Locks", State = "CT", PostalCode = "06103" };

        AddressRules.FillConnecticutCounties(address, Towns, CountiesByZip);

        Assert.Equal("09003", address.County);
        Assert.Equal("09110", address.CountyEquivalent);
    }

    [Fact]
    public void FillConnecticutCountiesMatchesTheTownInEitherCase()
    {
        var address = new PersonAddress { City = "WINDSOR LOCKS", State = "CT" };

        AddressRules.FillConnecticutCounties(address, Towns, null);

        Assert.Equal(("09003", "09110"), (address.County, address.CountyEquivalent));
    }

    [Fact]
    public void FillConnecticutCountiesReadsOnlyTheTownsItIsGiven()
    {
        // A corrected town is the one passed in, not the seed's.
        var corrected = new CtTown { Town = "Windsor Locks", County = "09013", PlanningRegion = "09130" };
        var address = new PersonAddress { City = "Windsor Locks", State = "CT" };
        var unknown = new PersonAddress { City = "Windsor Locks", State = "CT" };

        AddressRules.FillConnecticutCounties(address, [corrected], null);
        AddressRules.FillConnecticutCounties(unknown, null, null);

        Assert.Equal(("09013", "09130"), (address.County, address.CountyEquivalent));
        Assert.Equal((null, null), (unknown.County, unknown.CountyEquivalent));
    }

    [Fact]
    public void FillConnecticutCountiesTakesTheCountyFromTheZipWhenTheTownIsUnknown()
    {
        var address = new PersonAddress { City = "Nowhere", State = "CT", PostalCode = "06103" };

        AddressRules.FillConnecticutCounties(address, Towns, CountiesByZip);

        Assert.Equal("09009", address.County);
        Assert.Null(address.CountyEquivalent);
    }

    [Fact]
    public void FillConnecticutCountiesKeepsAGivenValue()
    {
        var address = new PersonAddress
        {
            City = "Hartford", State = "CT", County = "09001", PostalCode = "06103",
        };

        AddressRules.FillConnecticutCounties(address, Towns, CountiesByZip);

        Assert.Equal("09001", address.County);
        Assert.Equal("09110", address.CountyEquivalent);
    }

    [Fact]
    public void FillConnecticutCountiesFillsOnlyTheCountyFromTheZipOutsideConnecticut()
    {
        var address = new PersonAddress { City = "Hartford", State = "UT", PostalCode = "84401" };

        AddressRules.FillConnecticutCounties(address, Towns, CountiesByZip);

        Assert.Equal("WB", address.County);
        Assert.Null(address.CountyEquivalent);
    }

    [Fact]
    public void CloseHistoricKeepsTheLatestBeginningCurrent()
    {
        var older = new PersonAddress { Beginning = new DateOnly(2020, 1, 1) };
        var newer = new PersonAddress { Beginning = new DateOnly(2024, 6, 1) };
        var undated = new PersonAddress();

        AddressRules.CloseHistoric([older, newer, undated], Today);

        Assert.Null(newer.Ending);
        Assert.Equal(new DateOnly(2024, 6, 1), older.Ending);
        Assert.Equal(new DateOnly(2024, 6, 1), undated.Ending);
    }

    [Fact]
    public void CloseHistoricKeepsTheLaterOfTwoUndatedAndClosesTheOtherToday()
    {
        var first = new PersonAddress();
        var second = new PersonAddress();

        AddressRules.CloseHistoric([first, second], Today);

        Assert.Null(second.Ending);
        Assert.Equal(Today, first.Ending);
    }

    [Fact]
    public void CloseHistoricLeavesEndedAddressesAndASingleOpenOneAlone()
    {
        var ended = new PersonAddress { Ending = new DateOnly(2019, 3, 1) };
        var open = new PersonAddress { Beginning = new DateOnly(2019, 3, 1) };

        AddressRules.CloseHistoric([ended, open], Today);

        Assert.Equal(new DateOnly(2019, 3, 1), ended.Ending);
        Assert.Null(open.Ending);
    }
}
