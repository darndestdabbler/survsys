using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the facility endpoints decide without the database: the wire shape and
/// what a facility to save must be.
/// </summary>
public class FacilityEndpointsTests
{
    private static Facility Stored() => new()
    {
        Id = 11,
        Name = "ARUP Laboratories",
        ShortName = "ARUP",
        AgencyId = 5,
        Street = "500 Chipeta Way",
        City = "Salt Lake City",
        State = "UT",
        Zip = "84108",
        Phone = "801-583-2787",
    };

    private static readonly FacilityResponse Expected = new(
        11, "ARUP Laboratories", "ARUP", 5, "500 Chipeta Way", "Salt Lake City", "UT", "84108", "801-583-2787");

    private static SaveFacilityRequest Valid() => new(
        "ARUP Laboratories", 5, "ARUP", "500 Chipeta Way", "Salt Lake City", "UT", "84108-1234", "801-583-2787");

    [Fact]
    public void ToResponseCarriesEveryColumn()
    {
        Assert.Equal(Expected, FacilityEndpoints.ToResponse.Compile().Invoke(Stored()));
    }

    [Fact]
    public void RespondIsTheReadMappingItself()
    {
        Assert.Equal(Expected, FacilityEndpoints.Respond(Stored()));
    }

    [Fact]
    public void WhyNotAFacilityAcceptsAFacilityWithEveryField()
    {
        Assert.Null(FacilityEndpoints.WhyNotAFacility(Valid()));
    }

    [Fact]
    public void WhyNotAFacilityAcceptsAFacilityWithOnlyANameAndAnAgency()
    {
        Assert.Null(FacilityEndpoints.WhyNotAFacility(new SaveFacilityRequest("UPHL", 14)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WhyNotAFacilityRefusesAFacilityWithNoName(string name)
    {
        Assert.Contains("name is required",
            FacilityEndpoints.WhyNotAFacility(Valid() with { Name = name }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAFacilityRefusesAFacilityWithNoAgency(int agencyId)
    {
        Assert.Contains("agencyId is required",
            FacilityEndpoints.WhyNotAFacility(Valid() with { AgencyId = agencyId }),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAFacilityRefusesAValueLongerThanItsColumn()
    {
        Assert.Contains("city is longer than 100",
            FacilityEndpoints.WhyNotAFacility(Valid() with { City = new string('a', 101) }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Utah")]
    [InlineData("U1")]
    public void WhyNotAFacilityRefusesAStateThatIsNotATwoLetterCode(string state)
    {
        Assert.Contains($"state '{state}'",
            FacilityEndpoints.WhyNotAFacility(Valid() with { State = state }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("8410")]
    [InlineData("ABCDE")]
    public void WhyNotAFacilityRefusesAZipThatIsNotOne(string zip)
    {
        Assert.Contains($"zip '{zip}'",
            FacilityEndpoints.WhyNotAFacility(Valid() with { Zip = zip }),
            StringComparison.Ordinal);
    }
}
