using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The intake statistics' range: the days a request covers when it leaves an
/// end out, and the ranges it refuses.
/// </summary>
public class MessageStatisticsEndpointsTests
{
    private static readonly DateOnly Today = new(2026, 10, 20);

    [Fact]
    public void RangeIsTheLastThirtyDaysWhenNoneIsAskedFor()
    {
        Assert.Equal((new DateOnly(2026, 9, 21), Today), MessageStatisticsEndpoints.Range(null, null, Today));
    }

    [Fact]
    public void RangeKeepsTheDaysAskedFor()
    {
        var from = new DateOnly(2026, 1, 1);
        var to = new DateOnly(2026, 1, 31);

        Assert.Equal((from, to), MessageStatisticsEndpoints.Range(from, to, Today));
    }

    [Fact]
    public void RangeEndsAtTodayWhenOnlyAStartIsAskedFor()
    {
        var from = new DateOnly(2026, 10, 1);

        Assert.Equal((from, Today), MessageStatisticsEndpoints.Range(from, null, Today));
    }

    [Fact]
    public void RangeCountsBackFromAnEndAskedFor()
    {
        var to = new DateOnly(2026, 3, 31);

        Assert.Equal((new DateOnly(2026, 3, 2), to), MessageStatisticsEndpoints.Range(null, to, Today));
    }

    [Fact]
    public void WhyNotARangePassesOneDayAndTheLongestRange()
    {
        Assert.Null(MessageStatisticsEndpoints.WhyNotARange(Today, Today));
        Assert.Null(MessageStatisticsEndpoints.WhyNotARange(Today.AddDays(1 - MessageStatisticsEndpoints.MaxDays), Today));
    }

    [Fact]
    public void WhyNotARangeRefusesARangeThatEndsBeforeItStarts()
    {
        Assert.Contains("before", MessageStatisticsEndpoints.WhyNotARange(Today, Today.AddDays(-1)), StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotARangeRefusesARangeLongerThanTheMost()
    {
        Assert.NotNull(MessageStatisticsEndpoints.WhyNotARange(Today.AddDays(-MessageStatisticsEndpoints.MaxDays), Today));
    }
}
