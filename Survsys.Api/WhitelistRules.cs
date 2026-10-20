namespace Survsys.Api;

/// <summary>
/// How a condition's whitelist rule chooses the existing case a message
/// updates: the legacy rule types this port keeps.
/// </summary>
public enum WhitelistRuleType
{
    /// <summary>Every update is a reviewer's.</summary>
    Exception,

    /// <summary>The newest of the person's cases on the condition.</summary>
    Newest,

    /// <summary>Any of the person's cases on the condition, whatever its date.</summary>
    NeverNew,

    /// <summary>No case: every message opens a new one.</summary>
    AlwaysNew,

    /// <summary>A case whose event date plus <see cref="WhitelistRule.Days"/> is after the message's date.</summary>
    TimeOnset,

    /// <summary>
    /// A case where the message's date is no more than
    /// <see cref="WhitelistRule.DaysBefore"/> before its event date and no more
    /// than <see cref="WhitelistRule.DaysAfter"/> after it.
    /// </summary>
    BeforeAfter,
}

/// <summary>
/// A condition's whitelist rule: its type, the days a time rule allows, and
/// whether a case that is Not a Case is matched like any other. Structured,
/// where the legacy parsed free text.
/// </summary>
public sealed class WhitelistRule
{
    public WhitelistRuleType Type { get; set; } = WhitelistRuleType.TimeOnset;

    /// <summary>For <see cref="WhitelistRuleType.TimeOnset"/>: days after the event date.</summary>
    public int Days { get; set; } = 90;

    /// <summary>For <see cref="WhitelistRuleType.BeforeAfter"/>: days before the event date.</summary>
    public int DaysBefore { get; set; }

    /// <summary>For <see cref="WhitelistRuleType.BeforeAfter"/>: days after the event date.</summary>
    public int DaysAfter { get; set; }

    /// <summary>Whether a Not a Case case is matched like any other.</summary>
    public bool IgnoreCaseStatus { get; set; }
}

/// <summary>An open case a whitelist rule is tested against: its id, event date, and whether it is Not a Case.</summary>
public sealed record WhitelistCase(int Id, DateOnly? EventDate, bool NotACase);

/// <summary>
/// The cases a whitelist rule matched, best first, or why the message is a
/// reviewer's instead.
/// </summary>
public sealed record WhitelistMatch(IReadOnlyList<WhitelistCase> Matched, string? Exception);

/// <summary>
/// The legacy whitelist rules engine for morbidity events: which of a
/// person's open cases on a condition a message updates.
/// </summary>
public static class WhitelistRules
{
    /// <summary>
    /// The rule for a condition: its override by name under
    /// <see cref="MessageProcessingOptions.WhitelistByCondition"/>, else
    /// <see cref="MessageProcessingOptions.Whitelist"/>.
    /// </summary>
    public static WhitelistRule For(MessageProcessingOptions options, string conditionName)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.WhitelistByCondition
            .FirstOrDefault(entry => string.Equals(entry.Key, conditionName, StringComparison.OrdinalIgnoreCase))
            .Value ?? options.Whitelist;
    }

    /// <summary>
    /// The cases <paramref name="rule"/> matches for a message dated
    /// <paramref name="referenceDate"/> — its specimen's collection date.
    /// </summary>
    /// <remarks>
    /// An exception rule, and a message with no date to test, are a
    /// reviewer's. Newest keeps the latest match by event date. Then the legacy
    /// pruning: where several cases match and some are Not a Case, those are
    /// dropped, unless the rule ignores case status or all of them are.
    /// </remarks>
    public static WhitelistMatch Match(
        WhitelistRule rule, DateOnly? referenceDate, IReadOnlyList<WhitelistCase> cases)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(cases);

        if (rule.Type == WhitelistRuleType.Exception)
        {
            return new WhitelistMatch([], "The condition's whitelist rule leaves updating a case to a reviewer.");
        }

        if (rule.Type == WhitelistRuleType.AlwaysNew || cases.Count == 0)
        {
            return new WhitelistMatch([], null);
        }

        if (referenceDate is not { } reference)
        {
            return new WhitelistMatch([], "The message has no specimen collection date to choose a case by.");
        }

        var matched = cases
            .Where(report => rule.Type switch
            {
                WhitelistRuleType.TimeOnset =>
                    report.EventDate is { } date && date.AddDays(rule.Days) > reference,
                WhitelistRuleType.BeforeAfter =>
                    report.EventDate is { } date
                    && reference >= date.AddDays(-rule.DaysBefore)
                    && reference <= date.AddDays(rule.DaysAfter),
                _ => true,
            })
            .OrderByDescending(report => report.EventDate)
            .ThenByDescending(report => report.Id)
            .ToList();

        if (rule.Type == WhitelistRuleType.Newest && matched.Count > 1)
        {
            matched = [matched[0]];
        }

        if (!rule.IgnoreCaseStatus && matched.Count > 1 && matched.Any(report => !report.NotACase))
        {
            matched = matched.Where(report => !report.NotACase).ToList();
        }

        return new WhitelistMatch(matched, null);
    }
}
