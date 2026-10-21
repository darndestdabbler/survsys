namespace Survsys.Messaging;

/// <summary>
/// A new event the duplicate detection checks the waiting messages against:
/// the condition it reports and the date it happened. The .NET equivalent of
/// the legacy <c>GraylistRequest</c> a trigger spooled when a case was created.
/// </summary>
/// <param name="ConditionId">The condition the event reports.</param>
/// <param name="EventDate">The date the event happened, the window is drawn around.</param>
public sealed record GraylistRequest(int ConditionId, DateOnly EventDate);

/// <summary>
/// A waiting graylisted message's matching key: the condition it reports and
/// the reference date the window is tested against. None of this is a
/// Survsys.Data entity; the caller projects whatever it holds into this record.
/// </summary>
/// <param name="ConditionId">The condition the message reports.</param>
/// <param name="ReferenceDate">The message's reference date, tested against the window.</param>
public sealed record GraylistCandidate(int ConditionId, DateOnly ReferenceDate);

/// <summary>
/// The time window a candidate's reference date must fall in, in whole days
/// before and after the event date. The caller translates a legacy interval
/// ("6 months") into days; this module never parses one.
/// </summary>
/// <param name="DaysBefore">How many days before the event date the window opens.</param>
/// <param name="DaysAfter">How many days after the event date the window closes.</param>
public sealed record GraylistWindow(int DaysBefore, int DaysAfter)
{
    /// <summary>
    /// A month either side of the event date, as a reasonable default where the
    /// caller has no condition-specific interval to apply.
    /// </summary>
    public static GraylistWindow Default { get; } = new(30, 30);
}

/// <summary>
/// The graylist time-windowed duplicate detection: whether a waiting graylisted
/// message belongs to a new event. Pure and over data — the matcher takes its
/// inputs as plain records and reaches for no DbContext, so this module keeps
/// its fence against Survsys.Data and Entity Framework Core, the fence the
/// architecture test asserts.
/// </summary>
/// <remarks>
/// A message matches when it reports the same condition as the event and its
/// reference date falls within the window drawn around the event date. The
/// window is inclusive at both bounds, so a date exactly on a bound matches and
/// one a day past it does not. This is the date-and-condition half of what the
/// legacy <c>GraylistRulesEngine</c> did; the operator-driven rules engine over
/// <c>conditions_structured</c> is not here.
/// </remarks>
public static class GraylistRules
{
    /// <summary>
    /// Whether one candidate matches a request within the window: its condition
    /// equals the request's and its reference date is on or after the event date
    /// less <see cref="GraylistWindow.DaysBefore"/> and on or before the event
    /// date plus <see cref="GraylistWindow.DaysAfter"/>.
    /// </summary>
    public static bool Matches(GraylistRequest request, GraylistCandidate candidate, GraylistWindow window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(window);

        if (candidate.ConditionId != request.ConditionId)
        {
            return false;
        }

        var lower = request.EventDate.AddDays(-window.DaysBefore);
        var upper = request.EventDate.AddDays(window.DaysAfter);

        return candidate.ReferenceDate >= lower && candidate.ReferenceDate <= upper;
    }

    /// <summary>
    /// The candidates from a sequence that match the request within the window,
    /// in the order they came.
    /// </summary>
    public static IEnumerable<GraylistCandidate> SelectMatches(
        GraylistRequest request, IEnumerable<GraylistCandidate> candidates, GraylistWindow window)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(window);

        return candidates.Where(candidate => Matches(request, candidate, window));
    }
}
