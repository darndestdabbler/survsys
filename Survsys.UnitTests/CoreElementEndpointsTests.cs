using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the core element endpoints decide without the database: the wire
/// shapes and what an override to save must be.
/// </summary>
public class CoreElementEndpointsTests
{
    private static CoreElement StoredElement() =>
        new() { Id = 6, Uid = "food_handler", Label = "Food handler", Tab = "Investigation", Hidden = true };

    private static CoreElementOverride StoredOverride() =>
        new() { Id = 3, CoreElementId = 6, ConditionId = 40, AgencyId = 5, Hidden = false };

    [Fact]
    public void ToResponseCarriesEveryColumn()
    {
        Assert.Equal(
            new CoreElementResponse(6, "food_handler", "Food handler", "Investigation", true),
            CoreElementEndpoints.ToResponse.Compile().Invoke(StoredElement()));
    }

    [Fact]
    public void RespondIsTheReadMappingItself()
    {
        Assert.Equal(
            CoreElementEndpoints.ToResponse.Compile().Invoke(StoredElement()),
            CoreElementEndpoints.Respond(StoredElement()));
    }

    [Fact]
    public void ToOverrideResponseCarriesEveryColumn()
    {
        Assert.Equal(
            new CoreElementOverrideResponse(3, 6, 40, 5, false),
            CoreElementEndpoints.ToOverrideResponse.Compile().Invoke(StoredOverride()));
    }

    [Fact]
    public void RespondOverrideIsTheReadMappingItself()
    {
        Assert.Equal(
            new CoreElementOverrideResponse(3, 6, 40, 5, false),
            CoreElementEndpoints.RespondOverride(StoredOverride()));
    }

    [Theory]
    [InlineData(40, null)]
    [InlineData(null, 5)]
    [InlineData(40, 5)]
    public void WhyNotAnOverrideAcceptsAConditionAnAgencyOrBoth(int? conditionId, int? agencyId)
    {
        Assert.Null(CoreElementEndpoints.WhyNotAnOverride(new SaveCoreElementOverrideRequest(6, conditionId, agencyId, true)));
    }

    [Fact]
    public void WhyNotAnOverrideRefusesAnOverrideWithNeitherConditionNorAgency()
    {
        var why = CoreElementEndpoints.WhyNotAnOverride(new SaveCoreElementOverrideRequest(6, null, null, true));

        Assert.NotNull(why);
        Assert.Contains("conditionId or agencyId", why);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAnOverrideRefusesAnOverrideWithNoElement(int coreElementId)
    {
        var why = CoreElementEndpoints.WhyNotAnOverride(new SaveCoreElementOverrideRequest(coreElementId, 40, null, true));

        Assert.NotNull(why);
        Assert.Contains("coreElementId", why);
    }

    [Fact]
    public void WhyNotAnOverrideRefusesAConditionNothingCanBe()
    {
        var why = CoreElementEndpoints.WhyNotAnOverride(new SaveCoreElementOverrideRequest(6, 0, null, true));

        Assert.NotNull(why);
        Assert.Contains("conditionId 0", why);
    }

    [Fact]
    public void WhyNotAnOverrideRefusesAnAgencyNothingCanBe()
    {
        Assert.Equal(
            AgencyEndpoints.NotAnAgency(-2),
            CoreElementEndpoints.WhyNotAnOverride(new SaveCoreElementOverrideRequest(6, null, -2, true)));
    }
}
