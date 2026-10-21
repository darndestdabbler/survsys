using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The graylist time-windowed duplicate detection over small in-memory data: a
/// candidate matches a request when it reports the same condition and its
/// reference date falls within the window drawn around the event date,
/// inclusive at both bounds, so a date on a bound matches and one a day past it
/// does not.
/// </summary>
public class GraylistRulesTests
{
    private static readonly DateOnly EventDate = new(2026, 6, 15);
    private static readonly GraylistWindow Window = new(DaysBefore: 10, DaysAfter: 20);
    private static readonly GraylistRequest Request = new(ConditionId: 42, EventDate: EventDate);

    [Fact]
    public void DifferentConditionNeverMatchesHoweverCloseTheDate()
    {
        var sameDayOtherCondition = new GraylistCandidate(ConditionId: 43, ReferenceDate: EventDate);

        Assert.False(GraylistRules.Matches(Request, sameDayOtherCondition, Window));
    }

    [Fact]
    public void ReferenceDateOnTheLowerBoundMatches()
    {
        var onLowerBound = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(-Window.DaysBefore));

        Assert.True(GraylistRules.Matches(Request, onLowerBound, Window));
    }

    [Fact]
    public void ReferenceDateOnTheUpperBoundMatches()
    {
        var onUpperBound = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(Window.DaysAfter));

        Assert.True(GraylistRules.Matches(Request, onUpperBound, Window));
    }

    [Fact]
    public void ReferenceDateADayBeforeTheLowerBoundDoesNotMatch()
    {
        var justBelow = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(-Window.DaysBefore - 1));

        Assert.False(GraylistRules.Matches(Request, justBelow, Window));
    }

    [Fact]
    public void ReferenceDateADayAfterTheUpperBoundDoesNotMatch()
    {
        var justAbove = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(Window.DaysAfter + 1));

        Assert.False(GraylistRules.Matches(Request, justAbove, Window));
    }

    [Fact]
    public void SelectMatchesReturnsExactlyTheMatchingCandidatesInOrder()
    {
        var onLowerBound = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(-Window.DaysBefore));
        var otherCondition = new GraylistCandidate(ConditionId: 43, ReferenceDate: EventDate);
        var inside = new GraylistCandidate(Request.ConditionId, EventDate);
        var justAbove = new GraylistCandidate(Request.ConditionId, EventDate.AddDays(Window.DaysAfter + 1));

        var matches = GraylistRules.SelectMatches(
            Request, [onLowerBound, otherCondition, inside, justAbove], Window).ToList();

        Assert.Equal([onLowerBound, inside], matches);
    }
}
