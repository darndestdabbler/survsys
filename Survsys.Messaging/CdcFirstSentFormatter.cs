using System.Globalization;

namespace Survsys.Messaging;

/// <summary>
/// The <c>cdcFirstSent</c> ruleset: formats the date a notification was first
/// sent to CDC, by the configured date format.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>cdcFirstSent</c> formatter described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the record's
/// <c>cdc_first_sent_at</c> field is formatted when it is set, the current time
/// is formatted when it is empty, and a missing date format is an error. The
/// current time comes from an injected <see cref="TimeProvider"/> so the
/// empty-field path is deterministic in a test.
/// </remarks>
public sealed class CdcFirstSentFormatter : IRuleSetFormatter
{
    private const string FieldName = "cdc_first_sent_at";

    private readonly TimeProvider _timeProvider;

    /// <summary>Builds the formatter over a clock, the system clock by default.</summary>
    public CdcFirstSentFormatter(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(record);

        var format = ruleSet.Data.TryGetValue("dateFormat", out var f) ? f : null;
        if (string.IsNullOrWhiteSpace(format))
        {
            return RepeatingValues.Apply(ruleSet.Repeating, [new RuleSetFormattedData(
                Message: "the cdc first sent date needs a date format",
                MessageKind: RuleSetMessageKind.Error)]);
        }

        var raw = record.Value(FieldName);
        string formatted;
        if (string.IsNullOrWhiteSpace(raw))
        {
            formatted = _timeProvider.GetUtcNow().ToString(format, CultureInfo.InvariantCulture);
        }
        else if (DateTimeOffset.TryParse(
            raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            formatted = parsed.ToString(format, CultureInfo.InvariantCulture);
        }
        else
        {
            formatted = raw;
        }

        return RepeatingValues.Apply(ruleSet.Repeating, [new RuleSetFormattedData(Value: formatted)]);
    }
}
