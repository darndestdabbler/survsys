namespace Survsys.Data;

/// <summary>
/// How often a task repeats: the one of these a task's repeating interval
/// holds. <see cref="Never"/> is the absence of a repeat, which is how a task
/// that happens once is left.
/// </summary>
/// <remarks>
/// The intervals are the values the legacy <c>task.repeating_interval</c>
/// column holds, reduced to the three a schedule can be expanded from —
/// <see cref="Daily"/>, <see cref="Weekly"/> and <see cref="Monthly"/> — and
/// the absence of one.
/// </remarks>
public enum RepeatingInterval
{
    /// <summary>
    /// The task does not repeat. The legacy leaves a one-off task's interval
    /// empty, so this is the interval an empty or absent code reads as rather
    /// than a code of its own, as <see cref="CaseStatus.Unknown"/> is.
    /// </summary>
    Never,

    /// <summary>Every day, the code <c>daily</c>.</summary>
    Daily,

    /// <summary>Every seven days, the code <c>weekly</c>.</summary>
    Weekly,

    /// <summary>Every month on the same day of the month, the code <c>monthly</c>.</summary>
    Monthly,
}

/// <summary>
/// The repeating-task schedule: the interval vocabulary, and the two questions
/// a schedule needs answered of itself — whether an offered one can be
/// expanded at all, and which dates it expands to.
/// </summary>
/// <remarks>
/// The columns are the legacy <c>task.repeating_interval</c>,
/// <c>task.until_date</c> and <c>task.repeating_task_id</c>: an interval, a
/// last day the series may reach, and the first task of the series the rest of
/// it points at. The screen is the Notes tab's "Task Schedule", as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps it.
///
/// A series is expanded once, where the task is recorded, into one dated task
/// per date this rule returns. Nothing re-expands it afterwards, and nothing
/// here runs on a clock: a task becomes due by its due date being read. That
/// is why the whole of the schedule is this one pure rule and not a service —
/// there is no state to keep between the one expansion and the reading of the
/// dates it wrote.
///
/// Pure, and public and static in the shape <see cref="CaseStatusRules"/> and
/// <see cref="WorkflowRules"/> have, because the unit suite proves it without
/// starting a host. It takes no <c>DbContext</c>: a schedule is three values a
/// caller has in hand.
/// </remarks>
public static class RecurringTaskRules
{
    /// <summary>The code <see cref="RepeatingInterval.Daily"/> is stored under.</summary>
    public const string DailyCode = "daily";

    /// <summary>The code <see cref="RepeatingInterval.Weekly"/> is stored under.</summary>
    public const string WeeklyCode = "weekly";

    /// <summary>The code <see cref="RepeatingInterval.Monthly"/> is stored under.</summary>
    public const string MonthlyCode = "monthly";

    /// <summary>
    /// The most tasks one series may be expanded into.
    /// </summary>
    /// <remarks>
    /// A schedule is expanded into a row per date, so an until date decades
    /// out would be tens of thousands of rows written by one request. The cap
    /// makes that a refusal a caller reads — <see cref="WhyNotASchedule"/>
    /// names it and says to bring the until date in — rather than a write
    /// nobody asked for. A hundred is well past any real task schedule: a
    /// daily task for three months, a weekly one for two years, a monthly one
    /// for eight.
    /// </remarks>
    public const int MaxOccurrences = 100;

    /// <summary>
    /// The interval a stored code names.
    /// </summary>
    /// <remarks>
    /// An empty, whitespace or absent code is
    /// <see cref="RepeatingInterval.Never"/>, which is how the legacy leaves a
    /// task that happens once; surrounding whitespace is trimmed before a
    /// non-empty code is matched, and the match is exact — the codes are
    /// legacy codes and not display text, so <c>Daily</c> is not <c>daily</c>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The code is neither empty nor one of the three this vocabulary knows. A
    /// stored value like that was written by something that did not go through
    /// this rule, as <see cref="CaseStatusRules.Read"/> refuses a stored
    /// status it cannot read and for the same reason.
    /// </exception>
    public static RepeatingInterval Read(string? storedCode)
    {
        if (string.IsNullOrWhiteSpace(storedCode))
        {
            return RepeatingInterval.Never;
        }

        if (Match(storedCode) is { } interval)
        {
            return interval;
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a repeating interval code this tier knows. A stored "
            + $"repeating interval is one of {DailyCode}, {WeeklyCode} or {MonthlyCode}, or "
            + "empty for a task that happens once. A row holding anything else was written "
            + "by something that did not go through this vocabulary.");
    }

    /// <summary>
    /// Reads a caller's offered interval code into an interval, answering
    /// whether it is one — a known code yields its interval, an empty or
    /// unknown code yields none.
    /// </summary>
    /// <remarks>
    /// Where <see cref="Read"/> treats an empty code as
    /// <see cref="RepeatingInterval.Never"/> and throws on an unknown one,
    /// this refuses both, as <see cref="WorkflowRules.TryParse"/> refuses
    /// both: the caller asking "is this an interval?" has a string in hand and
    /// wants an answer rather than an exception, and an empty string is not an
    /// interval. <see cref="WhyNotASchedule"/> is what sorts an empty code from
    /// an unknown one, because only there does the difference matter.
    /// </remarks>
    public static bool TryParse(string? offeredCode, out RepeatingInterval interval)
    {
        if (!string.IsNullOrWhiteSpace(offeredCode) && Match(offeredCode) is { } matched)
        {
            interval = matched;
            return true;
        }

        interval = default;
        return false;
    }

    /// <summary>
    /// The code an interval is stored under, for the caller that has an
    /// interval in hand and writes it.
    /// </summary>
    /// <remarks>
    /// <see cref="RepeatingInterval.Never"/> is the absence of a code, so it
    /// answers null: a task that happens once stores no interval, which is the
    /// empty column <see cref="Read"/> reads back as Never.
    /// </remarks>
    public static string? CodeOf(RepeatingInterval interval) => interval switch
    {
        RepeatingInterval.Never => null,
        RepeatingInterval.Daily => DailyCode,
        RepeatingInterval.Weekly => WeeklyCode,
        RepeatingInterval.Monthly => MonthlyCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(interval), interval, "is not a repeating interval this rule knows."),
    };

    /// <summary>
    /// Why the offered schedule is not one that can be expanded into a series,
    /// or null when it is.
    /// </summary>
    /// <remarks>
    /// The shape <c>Survsys.Api</c>'s <c>WhyNotAnExposure</c> has: a pure
    /// function the unit suite proves, with the endpoint turning the sentence
    /// into a 400 that names the field. Four things are wrong with a schedule
    /// on its face — an interval code this vocabulary does not know; a
    /// repeating interval with no due date to count from; an until date before
    /// the due date, which is a series with nothing in it; and a series longer
    /// than <see cref="MaxOccurrences"/>. A task that does not repeat has no
    /// schedule to be wrong: an empty interval is accepted whatever the other
    /// two values are.
    /// </remarks>
    public static string? WhyNotASchedule(DateOnly? dueDate, string? intervalCode, DateOnly? untilDate)
    {
        if (string.IsNullOrWhiteSpace(intervalCode))
        {
            return null;
        }

        if (!TryParse(intervalCode, out var interval))
        {
            return $"repeatingInterval '{intervalCode}' is not an interval this tier repeats "
                + $"on. Give one of {DailyCode}, {WeeklyCode} or {MonthlyCode}, or leave it "
                + "out for a task that happens once.";
        }

        if (dueDate is not { } firstDue)
        {
            return "dueDate is missing, and a repeating task is counted from the day it is "
                + "first due. Give the first due date, or leave repeatingInterval out for a "
                + "task that happens once.";
        }

        if (untilDate is not { } until)
        {
            return null;
        }

        if (until < firstDue)
        {
            return $"untilDate {until:yyyy-MM-dd} is before dueDate {firstDue:yyyy-MM-dd}, and "
                + "a series ends no earlier than it starts. Give the dates in order, or leave "
                + "untilDate out to record the first task alone.";
        }

        var occurrences = Occurrences(firstDue, interval, until);

        if (occurrences > MaxOccurrences)
        {
            return $"a {CodeOf(interval)} task due {firstDue:yyyy-MM-dd} and repeating until "
                + $"{until:yyyy-MM-dd} is {occurrences} tasks, and no more than "
                + $"{MaxOccurrences} are recorded from one schedule. Bring untilDate in, or "
                + "repeat less often.";
        }

        return null;
    }

    /// <summary>
    /// The dates of the series a schedule expands to, in order, with the first
    /// due date always among them.
    /// </summary>
    /// <remarks>
    /// <see cref="RepeatingInterval.Never"/>, or a schedule with no until
    /// date, is the first date alone: there is nothing to repeat towards.
    /// Otherwise the step is a day, seven days or a month, and the series runs
    /// up to and including an until date it lands on exactly. Every date is
    /// counted from the first due date rather than from the one before it, so
    /// a monthly task first due on the 31st is due on the 31st of every month
    /// long enough to have one and on the last day of the months that are not
    /// — which is what <see cref="DateOnly.AddMonths"/> does, and what a
    /// cumulative step would lose the moment it clamped.
    ///
    /// It expands what <see cref="WhyNotASchedule"/> accepted. Handed a
    /// schedule that rule refuses, it answers the first date alone rather than
    /// a series: the caller that did not ask first gets one task, not a
    /// runaway.
    /// </remarks>
    public static IReadOnlyList<DateOnly> DueDates(
        DateOnly firstDue,
        RepeatingInterval interval,
        DateOnly? untilDate)
    {
        if (interval == RepeatingInterval.Never || untilDate is not { } until || until < firstDue)
        {
            return [firstDue];
        }

        var occurrences = Math.Min(Occurrences(firstDue, interval, until), MaxOccurrences);

        return [.. Enumerable.Range(0, occurrences).Select(step => Step(firstDue, interval, step))];
    }

    /// <summary>
    /// How many dates a series holds, counted rather than walked.
    /// </summary>
    /// <remarks>
    /// Arithmetic and not a loop, because the first thing asked of a schedule
    /// is whether it is too long to expand: a count that walked the series
    /// would have to write the million dates before it could say there were
    /// too many. The monthly count is the months between the two dates,
    /// stepped back by one where the clamped date of that month overshoots the
    /// until date. The caller has established that the until date is not
    /// before the first due date, so the answer is at least one.
    /// </remarks>
    private static int Occurrences(DateOnly firstDue, RepeatingInterval interval, DateOnly until) =>
        interval switch
        {
            RepeatingInterval.Daily => (until.DayNumber - firstDue.DayNumber) + 1,
            RepeatingInterval.Weekly => ((until.DayNumber - firstDue.DayNumber) / 7) + 1,
            RepeatingInterval.Monthly => MonthlyOccurrences(firstDue, until),
            _ => 1,
        };

    /// <summary>
    /// How many monthly dates fall on or before the until date, counting from
    /// the first due date.
    /// </summary>
    private static int MonthlyOccurrences(DateOnly firstDue, DateOnly until)
    {
        var months = ((until.Year - firstDue.Year) * 12) + until.Month - firstDue.Month;

        if (months > 0 && firstDue.AddMonths(months) > until)
        {
            months--;
        }

        return months + 1;
    }

    /// <summary>The date a given number of steps past the first due date.</summary>
    private static DateOnly Step(DateOnly firstDue, RepeatingInterval interval, int step) =>
        interval switch
        {
            RepeatingInterval.Daily => firstDue.AddDays(step),
            RepeatingInterval.Weekly => firstDue.AddDays(step * 7),
            RepeatingInterval.Monthly => firstDue.AddMonths(step),
            _ => firstDue,
        };

    /// <summary>
    /// The interval a code names, or null where a non-empty code is not one of
    /// the three. The common core of <see cref="Read"/> and
    /// <see cref="TryParse"/>, which differ only in what they do with an empty
    /// code and an unknown one.
    /// </summary>
    private static RepeatingInterval? Match(string? code) => code?.Trim() switch
    {
        DailyCode => RepeatingInterval.Daily,
        WeeklyCode => RepeatingInterval.Weekly,
        MonthlyCode => RepeatingInterval.Monthly,
        _ => null,
    };
}
