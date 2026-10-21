using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The static-value formatter across its format types: a string literal, a
/// validated number, the numeric-validation error, the current time by a date
/// format over an injected clock, and a literal resolved as a concept code
/// through the supplied value set.
/// </summary>
public class StaticValueFormatterTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static LoadedRuleSet Rule(
        string? formatType,
        IReadOnlyDictionary<string, string?> data,
        IReadOnlyList<ValueSetConcept>? valueSet = null,
        string? valueSetName = null)
        => new(
            TypeLabel: "staticValue",
            FormatType: formatType,
            Repeating: false,
            ValueSetName: valueSetName,
            Fields: [],
            Data: data,
            ValueMaps: [],
            Expressions: [],
            ValueSet: valueSet ?? []);

    private static readonly EpiTraxRecord NoRecord = new(new Dictionary<string, string?>());

    [Fact]
    public void StringFormatReturnsTheLiteralUnchanged()
    {
        var rule = Rule("string", new Dictionary<string, string?> { ["staticValue"] = "Utah" });

        var result = new StaticValueFormatter().Format(rule, NoRecord);

        Assert.Equal("Utah", Assert.Single(result).Value);
    }

    [Fact]
    public void NumericFormatReturnsAValidNumber()
    {
        var rule = Rule("numeric", new Dictionary<string, string?> { ["staticValue"] = "49" });

        var result = new StaticValueFormatter().Format(rule, NoRecord);

        Assert.Equal("49", Assert.Single(result).Value);
    }

    [Fact]
    public void NumericFormatRejectsANonNumber()
    {
        var rule = Rule("numeric", new Dictionary<string, string?> { ["staticValue"] = "4a9" });

        var only = Assert.Single(new StaticValueFormatter().Format(rule, NoRecord));

        Assert.Null(only.Value);
        Assert.Equal(RuleSetMessageKind.Error, only.MessageKind);
    }

    [Fact]
    public void DateFormatFormatsTheCurrentTime()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 3, 13, 14, 30, 22, TimeSpan.Zero));
        var rule = Rule("date", new Dictionary<string, string?> { ["dateFormat"] = "yyyyMMddHHmmss" });

        var result = new StaticValueFormatter(clock).Format(rule, NoRecord);

        Assert.Equal("20260313143022", Assert.Single(result).Value);
    }

    [Fact]
    public void DateFormatWithoutAFormatIsAnError()
    {
        var rule = Rule("date", new Dictionary<string, string?>());

        var only = Assert.Single(new StaticValueFormatter().Format(rule, NoRecord));

        Assert.Equal(RuleSetMessageKind.Error, only.MessageKind);
    }

    [Fact]
    public void ValueSetFormatResolvesTheLiteralAsAConceptCode()
    {
        var rule = Rule(
            "phinValueSet",
            new Dictionary<string, string?> { ["staticValue"] = "11000" },
            valueSet:
            [
                new ValueSetConcept("Salmonellosis", "11000H", "11000", "2.16.840.1.114222.1"),
            ],
            valueSetName: "disease");

        var only = Assert.Single(new StaticValueFormatter().Format(rule, NoRecord));

        Assert.Equal("Salmonellosis", only.Value);
        Assert.Equal("11000H", only.Code);
        Assert.Equal("11000", only.Id);
        Assert.Equal("2.16.840.1.114222.1", only.CodeSystemOid);
    }
}
