using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the encounter endpoints decide without the database: what an encounter
/// to save must be in itself. Whether its case and facility exist is the
/// database's to answer and is proven in the integration suite.
/// </summary>
public class EncounterEndpointsTests
{
    [Fact]
    public void WhyNotAnEncounterAcceptsADateAlone()
    {
        Assert.Null(EncounterEndpoints.WhyNotAnEncounter(
            new SaveEncounterRequest(new DateOnly(2026, 3, 2))));
    }

    [Fact]
    public void WhyNotAnEncounterAcceptsEveryFieldSet()
    {
        Assert.Null(EncounterEndpoints.WhyNotAnEncounter(new SaveEncounterRequest(
            new DateOnly(2026, 3, 2),
            "ED",
            12,
            "Seen in the emergency department")));
    }

    [Fact]
    public void WhyNotAnEncounterRefusesNoDate()
    {
        Assert.Contains("encounterDate is required",
            EncounterEndpoints.WhyNotAnEncounter(new SaveEncounterRequest(null)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void WhyNotAnEncounterRefusesAFacilityIdBelowOne(int facilityId)
    {
        Assert.Contains($"facilityId {facilityId} is not a facility",
            EncounterEndpoints.WhyNotAnEncounter(
                new SaveEncounterRequest(new DateOnly(2026, 3, 2), FacilityId: facilityId)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnEncounterRefusesATypeLongerThanTheColumn()
    {
        Assert.Contains("encounterType is longer than 50",
            EncounterEndpoints.WhyNotAnEncounter(new SaveEncounterRequest(
                new DateOnly(2026, 3, 2), EncounterType: new string('a', 51))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnEncounterRefusesADescriptionLongerThanTheColumn()
    {
        Assert.Contains("description is longer than 1000",
            EncounterEndpoints.WhyNotAnEncounter(new SaveEncounterRequest(
                new DateOnly(2026, 3, 2), Description: new string('a', 1_001))),
            StringComparison.Ordinal);
    }
}
