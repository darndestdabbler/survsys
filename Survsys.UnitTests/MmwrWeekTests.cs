using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The MMWR week and year numbering: the clean weeks inside a year, and the
/// boundary cases at each year end where the MMWR year parts from the calendar
/// year.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the arithmetic itself and not a
/// database or a host. The algorithm is EpiTrax's own, as
/// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 4
/// records it: an MMWR week runs Sunday to Saturday, week 1 is the week
/// containing January 4, and the days around a year end can belong to the
/// neighbouring MMWR year. The values below are worked from that definition —
/// 2026 opens its MMWR year on Sunday January 4, so January 1 to 3 close out
/// MMWR year 2025 as its 53rd week, and the first days of some calendar years
/// carry the previous MMWR year while some Decembers carry the next.
/// </remarks>
public class MmwrWeekTests
{
    /// <summary>
    /// The clean weeks: the Sunday that opens MMWR year 2026 is week 1, its
    /// Saturday is still week 1, and the next Sunday is week 2.
    /// </summary>
    [Theory]
    [InlineData(2026, 1, 4, 1, 2026)]
    [InlineData(2026, 1, 10, 1, 2026)]
    [InlineData(2026, 1, 11, 2, 2026)]
    public void OfNumbersTheWeeksFromTheSundayContainingJanuaryFour(
        int year, int month, int day, int expectedWeek, int expectedYear)
    {
        var mmwr = MmwrWeek.Of(new DateOnly(year, month, day));

        Assert.Equal(new MmwrWeek(expectedWeek, expectedYear), mmwr);
    }

    /// <summary>
    /// The first day of a calendar year, which can open week 1 of its own MMWR
    /// year or close out the previous one. January 1, 2023 is a Sunday and
    /// opens MMWR year 2023; January 1, 2026 is a Thursday, so it falls in the
    /// final week of MMWR year 2025.
    /// </summary>
    [Theory]
    [InlineData(2023, 1, 1, 1, 2023)]
    [InlineData(2026, 1, 1, 53, 2025)]
    public void OfReadsTheFirstDayOfAYearIntoTheMmwrYearItBelongsTo(
        int year, int month, int day, int expectedWeek, int expectedYear)
    {
        var mmwr = MmwrWeek.Of(new DateOnly(year, month, day));

        Assert.Equal(new MmwrWeek(expectedWeek, expectedYear), mmwr);
    }

    /// <summary>
    /// The last day of a calendar year, which can close its own MMWR year or
    /// open the next. December 31, 2022 is the 52nd week of MMWR year 2022;
    /// December 31, 2025 is the 53rd week of MMWR year 2025; December 31, 2023
    /// is a Sunday and already week 1 of MMWR year 2024.
    /// </summary>
    [Theory]
    [InlineData(2022, 12, 31, 52, 2022)]
    [InlineData(2025, 12, 31, 53, 2025)]
    [InlineData(2023, 12, 31, 1, 2024)]
    public void OfReadsTheLastDayOfAYearIntoTheMmwrYearItBelongsTo(
        int year, int month, int day, int expectedWeek, int expectedYear)
    {
        var mmwr = MmwrWeek.Of(new DateOnly(year, month, day));

        Assert.Equal(new MmwrWeek(expectedWeek, expectedYear), mmwr);
    }

    /// <summary>
    /// The week that straddles a year end is one week, read the same from
    /// either side of the turn: Saturday January 3, 2026 and the Sunday that
    /// began its week, December 28, 2025, are both the 53rd week of MMWR year
    /// 2025, and the following day opens MMWR year 2026 at week 1.
    /// </summary>
    [Fact]
    public void OfGivesTheWeekStraddlingAYearEndOneNumberFromBothSides()
    {
        var sunday = MmwrWeek.Of(new DateOnly(2025, 12, 28));
        var saturday = MmwrWeek.Of(new DateOnly(2026, 1, 3));
        var nextSunday = MmwrWeek.Of(new DateOnly(2026, 1, 4));

        Assert.Equal(new MmwrWeek(53, 2025), sunday);
        Assert.Equal(new MmwrWeek(53, 2025), saturday);
        Assert.Equal(new MmwrWeek(1, 2026), nextSunday);
    }

    /// <summary>
    /// A date well inside a year, as a plain reading: July 4, 2026 is the
    /// Saturday of the 26th MMWR week, 181 days past the January 4 start.
    /// </summary>
    [Fact]
    public void OfNumbersAMidYearDate()
    {
        var mmwr = MmwrWeek.Of(new DateOnly(2026, 7, 4));

        Assert.Equal(new MmwrWeek(26, 2026), mmwr);
    }
}
