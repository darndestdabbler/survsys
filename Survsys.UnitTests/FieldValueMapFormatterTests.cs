using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The field-value-map formatter: a field value mapped to text, a pipe-delimited
/// field split into several values, the null_value fallback on an empty field,
/// and the default_value fallback when no mapping matches.
/// </summary>
public class FieldValueMapFormatterTests
{
    private static LoadedRuleSet Rule(
        string formatType,
        IReadOnlyList<string> fields,
        IReadOnlyList<ValueMapping> valueMaps,
        bool repeating = false)
        => new(
            TypeLabel: "fieldValueMap",
            FormatType: formatType,
            Repeating: repeating,
            ValueSetName: null,
            Fields: fields,
            Data: new Dictionary<string, string?>(),
            ValueMaps: valueMaps,
            Expressions: [],
            ValueSet: []);

    [Fact]
    public void AFieldValueIsMappedToText()
    {
        var rule = Rule(
            "formValueToText",
            ["sex"],
            [new ValueMapping("M", "Male", null)]);
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["sex"] = "M" });

        var result = new FieldValueMapFormatter().Format(rule, record);

        Assert.Equal("Male", Assert.Single(result).Value);
    }

    [Fact]
    public void APipeDelimitedFieldIsSplitIntoSeveralValues()
    {
        var rule = Rule(
            "formValueToText",
            ["organism"],
            [new ValueMapping("A", "Alpha", null), new ValueMapping("B", "Beta", null)],
            repeating: true);
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["organism"] = "A|B" });

        var result = new FieldValueMapFormatter().Format(rule, record);

        Assert.Equal(["Alpha", "Beta"], result.Select(r => r.Value));
    }

    [Fact]
    public void AnEmptyFieldFallsBackToTheNullValueMapping()
    {
        var rule = Rule(
            "formValueToText",
            ["race"],
            [new ValueMapping("null_value", "Unknown", null)]);
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["race"] = "" });

        var result = new FieldValueMapFormatter().Format(rule, record);

        Assert.Equal("Unknown", Assert.Single(result).Value);
    }

    [Fact]
    public void AnUnmatchedValueFallsBackToTheDefaultValueMapping()
    {
        var rule = Rule(
            "formValueToText",
            ["sex"],
            [new ValueMapping("M", "Male", null), new ValueMapping("default_value", "Other", null)]);
        var record = new EpiTraxRecord(new Dictionary<string, string?> { ["sex"] = "X" });

        var result = new FieldValueMapFormatter().Format(rule, record);

        Assert.Equal("Other", Assert.Single(result).Value);
    }
}
