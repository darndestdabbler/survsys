using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The repeating-task schedule: what each interval code reads back to, that a
/// task with no code happens once, what the rule refuses to read or to parse,
/// that an interval round-trips through its code, which schedules can be
/// expanded at all, and which dates each one expands to.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database
/// or a host. The intervals are the three the legacy
/// <c>task.repeating_interval</c> column holds — <c>daily</c>, <c>weekly</c>
/// and <c>monthly</c> — and the absence of one.
/// </remarks>
public class RecurringTaskRulesTests
{
    /// <summary>The day the dated series in these cases are counted from.</summary>
    private static readonly DateOnly FirstDue = new(2026, 3, 2);

    [Theory]
    [InlineData(RecurringTaskRules.DailyCode, RepeatingInterval.Daily)]
    [InlineData(RecurringTaskRules.WeeklyCode, RepeatingInterval.Weekly)]
    [InlineData(RecurringTaskRules.MonthlyCode, RepeatingInterval.Monthly)]
    public void ReadNamesTheIntervalEachCodeIsStoredUnder(string code, RepeatingInterval expected)
    {
        Assert.Equal(expected, RecurringTaskRules.Read(code));
    }

    /// <summary>
    /// A task with no interval happens once. Empty, whitespace and absent all
    /// read as <see cref="RepeatingInterval.Never"/>, because the legacy
    /// leaves a one-off task's interval empty.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadIsNeverForAnEmptyOrAbsentCode(string? storedCode)
    {
        Assert.Equal(RepeatingInterval.Never, RecurringTaskRules.Read(storedCode));
    }

    /// <summary>
    /// Surrounding whitespace is trimmed before a non-empty code is matched,
    /// as the legacy data can carry a padded code.
    /// </summary>
    [Theory]
    [InlineData(" daily ", RepeatingInterval.Daily)]
    [InlineData("\tmonthly\n", RepeatingInterval.Monthly)]
    public void ReadTrimsSurroundingWhitespace(string code, RepeatingInterval expected)
    {
        Assert.Equal(expected, RecurringTaskRules.Read(code));
    }

    /// <summary>
    /// Each shape the reader refuses: a legacy interval this rule does not
    /// model, a code in the wrong case, and a value that is not a code at all.
    /// The sentence names the value and says what a stored interval may be, so
    /// whoever reads the log knows which row to look at.
    /// </summary>
    [Theory]
    [InlineData("yearly")]
    [InlineData("Daily")]
    [InlineData("every other tuesday")]
    public void ReadRefusesACodeTheVocabularyDoesNotKnow(string storedCode)
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => RecurringTaskRules.Read(storedCode));

        Assert.Contains(storedCode, refused.Message, StringComparison.Ordinal);
        Assert.Contains(RecurringTaskRules.DailyCode, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RecurringTaskRules.DailyCode, RepeatingInterval.Daily)]
    [InlineData(RecurringTaskRules.WeeklyCode, RepeatingInterval.Weekly)]
    [InlineData(RecurringTaskRules.MonthlyCode, RepeatingInterval.Monthly)]
    public void TryParseReadsEachOfferedCode(string offeredCode, RepeatingInterval expected)
    {
        Assert.True(RecurringTaskRules.TryParse(offeredCode, out var interval));
        Assert.Equal(expected, interval);
    }

    /// <summary>
    /// Where <see cref="RecurringTaskRules.Read"/> treats an empty code as
    /// Never and throws on an unknown one, the parse refuses both: the caller
    /// is asking whether a string is an interval, and neither is.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("yearly")]
    public void TryParseRefusesAnEmptyOrUnknownCode(string? offeredCode)
    {
        Assert.False(RecurringTaskRules.TryParse(offeredCode, out _));
    }

    [Theory]
    [InlineData(RepeatingInterval.Daily, RecurringTaskRules.DailyCode)]
    [InlineData(RepeatingInterval.Weekly, RecurringTaskRules.WeeklyCode)]
    [InlineData(RepeatingInterval.Monthly, RecurringTaskRules.MonthlyCode)]
    public void AnIntervalRoundTripsThroughItsCode(RepeatingInterval interval, string expected)
    {
        var code = RecurringTaskRules.CodeOf(interval);

        Assert.Equal(expected, code);
        Assert.Equal(interval, RecurringTaskRules.Read(code));
    }

    /// <summary>
    /// A task that happens once stores no interval, so the code of Never is
    /// the absence of one — which is the empty column the reader reads back as
    /// Never.
    /// </summary>
    [Fact]
    public void TheCodeOfNeverIsNoCodeAtAll()
    {
        Assert.Null(RecurringTaskRules.CodeOf(RepeatingInterval.Never));
    }

    /// <summary>
    /// A daily series runs a date per day, the first due date included, up to
    /// an until date it lands on exactly.
    /// </summary>
    [Fact]
    public void ADailySeriesIsADatePerDay()
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, RepeatingInterval.Daily, FirstDue.AddDays(3));

        Assert.Equal(
            [FirstDue, FirstDue.AddDays(1), FirstDue.AddDays(2), FirstDue.AddDays(3)],
            dates);
    }

    /// <summary>A weekly series steps seven days at a time.</summary>
    [Fact]
    public void AWeeklySeriesStepsSevenDays()
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, RepeatingInterval.Weekly, FirstDue.AddDays(14));

        Assert.Equal([FirstDue, FirstDue.AddDays(7), FirstDue.AddDays(14)], dates);
    }

    /// <summary>
    /// An until date between two steps stops at the last date on or before it:
    /// a weekly series until the thirteenth day holds two dates, not three,
    /// because the third would fall on the fourteenth.
    /// </summary>
    [Fact]
    public void ASeriesStopsAtTheLastDateOnOrBeforeTheUntilDate()
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, RepeatingInterval.Weekly, FirstDue.AddDays(13));

        Assert.Equal([FirstDue, FirstDue.AddDays(7)], dates);
    }

    /// <summary>
    /// A monthly series is the same day of each month, and a day no month has
    /// is that month's last — a task first due on 31 January is due on 28
    /// February and then on 31 March again, because every date is counted from
    /// the first due date rather than from the one before it.
    /// </summary>
    [Fact]
    public void AMonthlySeriesClampsIntoAShorterMonthAndRecoversAfterIt()
    {
        var dates = RecurringTaskRules.DueDates(
            new DateOnly(2026, 1, 31), RepeatingInterval.Monthly, new DateOnly(2026, 4, 30));

        Assert.Equal(
            [
                new DateOnly(2026, 1, 31),
                new DateOnly(2026, 2, 28),
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 4, 30),
            ],
            dates);
    }

    /// <summary>
    /// A monthly series whose clamped date overshoots the until date stops
    /// before it: first due on 31 January and until 27 February is the first
    /// date alone, because the February date falls on the 28th.
    /// </summary>
    [Fact]
    public void AMonthlySeriesStopsWhereTheClampedDateOvershootsTheUntilDate()
    {
        var dates = RecurringTaskRules.DueDates(
            new DateOnly(2026, 1, 31), RepeatingInterval.Monthly, new DateOnly(2026, 2, 27));

        Assert.Equal([new DateOnly(2026, 1, 31)], dates);
    }

    /// <summary>
    /// A task that does not repeat, and one with no until date to repeat
    /// towards, are each the first due date alone: there is nothing to repeat
    /// or nothing to repeat towards.
    /// </summary>
    [Theory]
    [InlineData(RepeatingInterval.Never, true)]
    [InlineData(RepeatingInterval.Daily, false)]
    public void ASeriesWithNothingToRepeatIsTheFirstDateAlone(
        RepeatingInterval interval,
        bool withUntilDate)
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, interval, withUntilDate ? FirstDue.AddDays(10) : null);

        Assert.Equal([FirstDue], dates);
    }

    /// <summary>
    /// Handed a schedule <see cref="RecurringTaskRules.WhyNotASchedule"/>
    /// refuses, the expansion answers the first date alone rather than a
    /// series: the caller that did not ask first gets one task, not a
    /// runaway.
    /// </summary>
    [Fact]
    public void ASeriesEndingBeforeItStartsIsTheFirstDateAlone()
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, RepeatingInterval.Daily, FirstDue.AddDays(-1));

        Assert.Equal([FirstDue], dates);
    }

    /// <summary>
    /// A schedule long enough to be refused is also capped where it is
    /// expanded anyway, so nothing writes more than the cap allows.
    /// </summary>
    [Fact]
    public void AnExpandedSeriesIsNeverLongerThanTheCap()
    {
        var dates = RecurringTaskRules.DueDates(
            FirstDue, RepeatingInterval.Daily, FirstDue.AddYears(5));

        Assert.Equal(RecurringTaskRules.MaxOccurrences, dates.Count);
    }

    /// <summary>
    /// A task that happens once has no schedule to be wrong, whatever the
    /// other two values say.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoIntervalIsNoScheduleToRefuse(string? intervalCode)
    {
        Assert.Null(RecurringTaskRules.WhyNotASchedule(null, intervalCode, null));
    }

    /// <summary>A schedule the rule accepts, with and without an until date.</summary>
    [Theory]
    [InlineData(RecurringTaskRules.DailyCode, 30)]
    [InlineData(RecurringTaskRules.WeeklyCode, 70)]
    [InlineData(RecurringTaskRules.MonthlyCode, 365)]
    public void AScheduleThatCanBeExpandedIsNotRefused(string intervalCode, int untilDays)
    {
        Assert.Null(RecurringTaskRules.WhyNotASchedule(
            FirstDue, intervalCode, FirstDue.AddDays(untilDays)));

        Assert.Null(RecurringTaskRules.WhyNotASchedule(FirstDue, intervalCode, null));
    }

    /// <summary>
    /// An interval code the vocabulary does not know is refused with the field
    /// named and the codes it may be.
    /// </summary>
    [Fact]
    public void AScheduleOnAnUnknownIntervalIsRefused()
    {
        var why = RecurringTaskRules.WhyNotASchedule(FirstDue, "fortnightly", null);

        Assert.NotNull(why);
        Assert.Contains("repeatingInterval", why, StringComparison.Ordinal);
        Assert.Contains(RecurringTaskRules.WeeklyCode, why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A repeating interval with no due date is refused: a series is counted
    /// from the day it is first due, and there is no such day.
    /// </summary>
    [Fact]
    public void ARepeatingScheduleWithNoDueDateIsRefused()
    {
        var why = RecurringTaskRules.WhyNotASchedule(
            null, RecurringTaskRules.WeeklyCode, FirstDue.AddDays(30));

        Assert.NotNull(why);
        Assert.Contains("dueDate", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// An until date before the due date is refused: it is a series with
    /// nothing in it.
    /// </summary>
    [Fact]
    public void AScheduleEndingBeforeItStartsIsRefused()
    {
        var why = RecurringTaskRules.WhyNotASchedule(
            FirstDue, RecurringTaskRules.DailyCode, FirstDue.AddDays(-1));

        Assert.NotNull(why);
        Assert.Contains("untilDate", why, StringComparison.Ordinal);
        Assert.Contains("dueDate", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// A series past the cap is refused with the count and the cap in it,
    /// rather than quietly writing a row per day for years.
    /// </summary>
    [Fact]
    public void AScheduleLongerThanTheCapIsRefused()
    {
        var why = RecurringTaskRules.WhyNotASchedule(
            FirstDue, RecurringTaskRules.DailyCode, FirstDue.AddYears(1));

        Assert.NotNull(why);
        Assert.Contains($"{RecurringTaskRules.MaxOccurrences}", why, StringComparison.Ordinal);
        Assert.Contains("untilDate", why, StringComparison.Ordinal);
    }

    /// <summary>
    /// The cap is the count of the dates, not an approximation of it: a daily
    /// series of exactly the cap is accepted and one date longer is refused.
    /// </summary>
    [Fact]
    public void TheCapIsTheCountOfTheDates()
    {
        var upToTheCap = FirstDue.AddDays(RecurringTaskRules.MaxOccurrences - 1);

        Assert.Null(RecurringTaskRules.WhyNotASchedule(
            FirstDue, RecurringTaskRules.DailyCode, upToTheCap));

        Assert.Equal(
            RecurringTaskRules.MaxOccurrences,
            RecurringTaskRules.DueDates(FirstDue, RepeatingInterval.Daily, upToTheCap).Count);

        Assert.NotNull(RecurringTaskRules.WhyNotASchedule(
            FirstDue, RecurringTaskRules.DailyCode, upToTheCap.AddDays(1)));
    }
}
