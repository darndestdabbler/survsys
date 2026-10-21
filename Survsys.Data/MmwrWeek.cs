namespace Survsys.Data;

/// <summary>
/// The MMWR week and year a date falls in: the CDC epidemiological-week
/// numbering a case morbidity report's event onset date is reported under.
/// </summary>
/// <remarks>
/// The algorithm is EpiTrax's own, as
/// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 4
/// records it from
/// <c>../nedss/legacy/nedss-ejb/src/main/java/gov/utah/health/service/nedss/MmwrEJBImpl.java</c>
/// and the PostgreSQL <c>mmwryearstartdate</c> function beside it. An MMWR week
/// runs Sunday to Saturday; week 1 of an MMWR year is the week containing
/// January 4, so the first days of a calendar year can belong to the previous
/// MMWR year and its last days to the next. That is why the year is read from
/// the start of the date's own MMWR week rather than from the date's calendar
/// year: a date in the final days of December can carry the next year's MMWR
/// year, and a date in the first days of January the previous year's.
///
/// The struct is both the rule and its answer: <see cref="Of"/> computes the
/// week and year together, which is what a caller wants of it — the shape
/// <c>Survsys.Api</c>'s record number rule has, pure and holding no clock. The
/// date is the caller's to supply; here is only the arithmetic over it.
/// </remarks>
/// <param name="Week">
/// The MMWR week, counting from 1 at the week containing January 4. The last
/// week of an MMWR year is 52 or 53, depending on how the Sundays fall.
/// </param>
/// <param name="Year">
/// The MMWR year, which can differ from the date's calendar year at either end
/// of it.
/// </param>
public readonly record struct MmwrWeek(int Week, int Year)
{
    /// <summary>
    /// The day of the month January 4 sits on, the day the first MMWR week of
    /// a year is defined to contain.
    /// </summary>
    private const int FirstWeekAnchor = 4;

    /// <summary>The length of a week, and the span one MMWR week number covers.</summary>
    private const int DaysInWeek = 7;

    /// <summary>
    /// The MMWR week and year <paramref name="date"/> falls in.
    /// </summary>
    public static MmwrWeek Of(DateOnly date)
    {
        var start = StartOfMmwrYearContaining(date);

        // days_between = date - start; mmwr_week = round((days_between - 3) / 7) + 1.
        // The numerator is a whole number of days and the denominator is seven,
        // so the quotient is never exactly a half; floor(x + 0.5) is Java's
        // Math.round, which the legacy used, and agrees with every rounding
        // mode over the values this can take.
        var daysBetween = date.DayNumber - start.DayNumber;
        var week = (int)Math.Floor(((daysBetween - 3) / (double)DaysInWeek) + 0.5) + 1;

        // The MMWR year is read seven days past its own start, which lands
        // clear of the December-or-January Sunday the year begins on and so
        // names the year the week belongs to rather than the one its Sunday
        // sits in.
        var year = start.AddDays(DaysInWeek).Year;

        return new MmwrWeek(week, year);
    }

    /// <summary>
    /// The Sunday that begins the MMWR year <paramref name="date"/> belongs to.
    /// </summary>
    /// <remarks>
    /// A date can precede its own calendar year's MMWR start (and so belong to
    /// the previous MMWR year) or reach the next year's (and so belong to it),
    /// which is why all three starts are weighed, exactly as the legacy
    /// <c>getMmwrStart</c> does.
    /// </remarks>
    private static DateOnly StartOfMmwrYearContaining(DateOnly date)
    {
        var previous = StartOfMmwrYear(date.Year - 1);
        var current = StartOfMmwrYear(date.Year);
        var next = StartOfMmwrYear(date.Year + 1);

        if (date < current)
        {
            return previous;
        }

        return date >= next ? next : current;
    }

    /// <summary>
    /// The Sunday that begins the MMWR year of a calendar year: the Sunday of
    /// the week containing January 4.
    /// </summary>
    /// <remarks>
    /// Found as the legacy <c>mmwryearstartdate</c> does: take the day of the
    /// week of January 1 with Sunday as 1 and Saturday as 7; step back to the
    /// Sunday of January 1's week; and where January 1 is Thursday, Friday or
    /// Saturday (so fewer than four of that week's days are in the new year),
    /// step forward a week, which is the week that holds January 4.
    /// </remarks>
    private static DateOnly StartOfMmwrYear(int year)
    {
        var januaryFirst = new DateOnly(year, 1, 1);

        // Sunday as 1 and Saturday as 7, which is DayOfWeek (Sunday 0) plus one.
        var dayOfWeek = (int)januaryFirst.DayOfWeek + 1;

        var sundayOfWeek = januaryFirst.AddDays(-(dayOfWeek - 1));

        return dayOfWeek > FirstWeekAnchor ? sundayOfWeek.AddDays(DaysInWeek) : sundayOfWeek;
    }
}
