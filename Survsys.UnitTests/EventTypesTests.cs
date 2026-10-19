using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The event type vocabulary: the three codes it knows, the order it lists
/// them in, and what it refuses.
/// </summary>
public class EventTypesTests
{
    [Theory]
    [InlineData(EventTypes.Morbidity)]
    [InlineData(EventTypes.Contact)]
    [InlineData(EventTypes.Assessment)]
    public void IsKnownAcceptsEachCode(string code)
    {
        Assert.True(EventTypes.IsKnown(code));
    }

    /// <summary>
    /// Absent, empty, a code in the wrong case and a code of no kind here are
    /// all refused, because the column holds the codes exactly as written.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Morbidity")]
    [InlineData("case")]
    public void IsKnownRefusesAnythingElse(string? code)
    {
        Assert.False(EventTypes.IsKnown(code));
    }

    [Fact]
    public void AllListsMorbidityContactAndAssessmentInThatOrder()
    {
        Assert.Equal(["morbidity", "contact", "assessment"], EventTypes.All);
    }
}
