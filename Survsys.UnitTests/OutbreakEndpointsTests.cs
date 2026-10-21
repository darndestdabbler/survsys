using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the outbreak endpoints decide without the database: the list-row wire
/// shape, and what an outbreak to save must be in itself — its name, its
/// number and its status, the parts a pure validator can judge. Whether the
/// condition and the owning agency exist is the database's to answer and is
/// proven in the integration suite.
/// </summary>
public class OutbreakEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheOutbreaksOwnColumns()
    {
        var created = new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);
        var outbreak = new Outbreak
        {
            Id = 7,
            Number = "2026-001",
            Name = "Spring picnic",
            OutbreakDate = new DateOnly(2026, 3, 1),
            Description = "A gathering",
            ConditionId = 4,
            AgencyId = 5,
            Status = OutbreakEndpoints.StatusCompleted,
            CreatedAt = created,
        };

        Assert.Equal(
            new OutbreakResponse(
                7,
                "2026-001",
                "Spring picnic",
                new DateOnly(2026, 3, 1),
                "A gathering",
                4,
                5,
                OutbreakEndpoints.StatusCompleted,
                created),
            OutbreakEndpoints.ToResponse.Compile().Invoke(outbreak));
    }

    [Fact]
    public void WhyNotAnOutbreakAcceptsANameAloneAtActive()
    {
        Assert.Null(OutbreakEndpoints.WhyNotAnOutbreak(new SaveOutbreakRequest("Spring picnic")));
    }

    [Fact]
    public void WhyNotAnOutbreakAcceptsEveryFieldSet()
    {
        Assert.Null(OutbreakEndpoints.WhyNotAnOutbreak(new SaveOutbreakRequest(
            "Spring picnic",
            "2026-001",
            new DateOnly(2026, 3, 1),
            "A gathering",
            ConditionId: 4,
            AgencyId: 5,
            Status: OutbreakEndpoints.StatusCompleted)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotAnOutbreakRefusesAnOutbreakWithNoName(string name)
    {
        Assert.Contains("name is required",
            OutbreakEndpoints.WhyNotAnOutbreak(new SaveOutbreakRequest(name)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnOutbreakRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 200",
            OutbreakEndpoints.WhyNotAnOutbreak(new SaveOutbreakRequest(new string('a', 201))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAnOutbreakRefusesANumberLongerThanTheColumn()
    {
        Assert.Contains("number is longer than 50",
            OutbreakEndpoints.WhyNotAnOutbreak(
                new SaveOutbreakRequest("Spring picnic", new string('a', 51))),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void WhyNotAnOutbreakRefusesAStatusThatIsNotKnown(int status)
    {
        Assert.Contains($"status {status} is not a known outbreak status",
            OutbreakEndpoints.WhyNotAnOutbreak(
                new SaveOutbreakRequest("Spring picnic", Status: status)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(OutbreakEndpoints.StatusActive)]
    [InlineData(OutbreakEndpoints.StatusCompleted)]
    public void WhyNotAnOutbreakAcceptsEachKnownStatus(int status)
    {
        Assert.Null(OutbreakEndpoints.WhyNotAnOutbreak(
            new SaveOutbreakRequest("Spring picnic", Status: status)));
    }

    [Fact]
    public void NotAConditionNamesTheConditionAndWhereTheConditionsAre()
    {
        var sentence = OutbreakEndpoints.NotACondition(42);

        Assert.Contains("conditionId 42", sentence, StringComparison.Ordinal);
        Assert.Contains("/api/conditions", sentence, StringComparison.Ordinal);
    }
}
