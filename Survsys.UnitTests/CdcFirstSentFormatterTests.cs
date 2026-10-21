using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The cdc-first-sent formatter: a present date reformatted by the configured
/// format, an empty field falling back to the current time over an injected
/// clock, and a missing date format erroring.
/// </summary>
public class CdcFirstSentFormatterTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static LoadedRuleSet Rule(IReadOnlyDictionary<string, string?> data)
        => new(
            TypeLabel: "cdcFirstSent",
            FormatType: null,
            Repeating: false,
            ValueSetName: null,
            Fields: [],
            Data: data,
            ValueMaps: [],
            Expressions: [],
            ValueSet: []);

    [Fact]
    public void APresentDateIsReformatted()
    {
        var rule = Rule(new Dictionary<string, string?> { ["dateFormat"] = "yyyyMMdd" });
        var record = new EpiTraxRecord(new Dictionary<string, string?>
        {
            ["cdc_first_sent_at"] = "2026-03-13T14:30:22Z",
        });

        var result = new CdcFirstSentFormatter().Format(rule, record);

        Assert.Equal("20260313", Assert.Single(result).Value);
    }

    [Fact]
    public void AnEmptyFieldFallsBackToTheCurrentTime()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 13, 0, 0, 0, TimeSpan.Zero));
        var rule = Rule(new Dictionary<string, string?> { ["dateFormat"] = "yyyyMMdd" });
        var record = new EpiTraxRecord(new Dictionary<string, string?>());

        var result = new CdcFirstSentFormatter(clock).Format(rule, record);

        Assert.Equal("20260313", Assert.Single(result).Value);
    }

    [Fact]
    public void AMissingDateFormatIsAnError()
    {
        var rule = Rule(new Dictionary<string, string?>());
        var record = new EpiTraxRecord(new Dictionary<string, string?>
        {
            ["cdc_first_sent_at"] = "2026-03-13T14:30:22Z",
        });

        var only = Assert.Single(new CdcFirstSentFormatter().Format(rule, record));

        Assert.Equal(RuleSetMessageKind.Error, only.MessageKind);
    }
}
