using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the agency endpoints decide without the database: the wire shape, what
/// an agency to save must be, the zip codes it is saved with, and the sentence
/// an unknown agency is refused with.
/// </summary>
public class AgencyEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheIdentifierAndTheName()
    {
        var agency = new Agency { Id = 5, Name = "Salt Lake County", ShortName = "SLCo" };

        Assert.Equal(
            new AgencyResponse(5, "Salt Lake County"),
            AgencyEndpoints.ToResponse.Compile().Invoke(agency));
    }

    [Fact]
    public void WhyNotAnAgencyAcceptsANameAShortNameAndZipCodes()
    {
        Assert.Null(AgencyEndpoints.WhyNotAnAgency(
            new SaveAgencyRequest("Salt Lake County", "SLCo", ["84108", " 84101-1234 "])));
    }

    [Fact]
    public void WhyNotAnAgencyAcceptsAnAgencyWithNoShortNameAndNoZipCodes()
    {
        Assert.Null(AgencyEndpoints.WhyNotAnAgency(new SaveAgencyRequest("Utah State")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotAnAgencyRefusesAnAgencyWithNoName(string name)
    {
        Assert.Contains("name is required",
            AgencyEndpoints.WhyNotAnAgency(new SaveAgencyRequest(name)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnAgencyRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            AgencyEndpoints.WhyNotAnAgency(new SaveAgencyRequest(new string('a', 201))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnAgencyRefusesAShortNameLongerThanTheColumn()
    {
        Assert.Contains("shortName is longer than 50",
            AgencyEndpoints.WhyNotAnAgency(new SaveAgencyRequest("Utah State", new string('a', 51))),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("8410")]
    [InlineData("ABCDE")]
    [InlineData("")]
    public void WhyNotAnAgencyRefusesAZipCodeThatIsNotOne(string zip)
    {
        Assert.Contains($"zipCodes holds '{zip}'",
            AgencyEndpoints.WhyNotAnAgency(new SaveAgencyRequest("Utah State", null, ["84108", zip])),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ZipCodesOfNormalizesDeduplicatesAndOrders()
    {
        Assert.Equal(
            ["84101", "84108"],
            AgencyEndpoints.ZipCodesOf(
                new SaveAgencyRequest("Salt Lake County", null, ["84108", "84101-1234", " 84108 "])));
    }

    [Fact]
    public void ZipCodesOfIsEmptyForNoZipCodes()
    {
        Assert.Empty(AgencyEndpoints.ZipCodesOf(new SaveAgencyRequest("Utah State")));
    }

    [Fact]
    public void NotAnAgencyNamesTheIdentifier()
    {
        Assert.StartsWith("agencyId 99 is not an agency", AgencyEndpoints.NotAnAgency(99), StringComparison.Ordinal);
    }
}
