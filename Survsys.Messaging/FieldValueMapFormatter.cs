using System.Globalization;

namespace Survsys.Messaging;

/// <summary>
/// The <c>fieldValueMap</c> ruleset: extracts the configured EpiTrax fields,
/// splits pipe-delimited values, and formats each by the configuration's
/// format type, translating through the value map where the type is coded.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>fieldValueMap</c> formatter described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>. The value-map fallback
/// chain is preserved exactly: an exact case-insensitive match first, then the
/// <c>null_value</c> entry when the field value is empty, then the
/// <c>default_value</c> entry when nothing matched. <c>string</c> passes the
/// value through, <c>numeric</c> validates it, <c>date</c> formats it by the
/// configured date format, <c>formValueToText</c> maps it to plain text, and
/// <c>phinValueSet</c>, <c>unitOfMeasure</c> or <c>textToValueSet</c> map it and
/// then resolve the mapped value as a concept code through the value set.
/// </remarks>
public sealed class FieldValueMapFormatter : IRuleSetFormatter
{
    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(record);

        var responses = new List<RuleSetFormattedData>();
        foreach (var field in ruleSet.Fields)
        {
            var values = record.Values(field);
            if (values.Count == 0)
            {
                // An empty field still formats once, so the null_value fallback applies.
                responses.Add(FormatValue(ruleSet, string.Empty));
                continue;
            }

            foreach (var value in values)
            {
                responses.Add(FormatValue(ruleSet, value));
            }
        }

        return RepeatingValues.Apply(ruleSet.Repeating, responses);
    }

    private static RuleSetFormattedData FormatValue(LoadedRuleSet ruleSet, string value)
    {
        switch (ruleSet.FormatType)
        {
            case "numeric":
                return IsNumeric(value)
                    ? new RuleSetFormattedData(Value: value)
                    : new RuleSetFormattedData(
                        Message: $"field value '{value}' is not numeric",
                        MessageKind: RuleSetMessageKind.Error);

            case "date":
                var format = ruleSet.Data.TryGetValue("dateFormat", out var f) ? f : null;
                if (string.IsNullOrWhiteSpace(value))
                {
                    return new RuleSetFormattedData();
                }

                return !string.IsNullOrWhiteSpace(format)
                        && DateTimeOffset.TryParse(
                            value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                    ? new RuleSetFormattedData(Value: parsed.ToString(format, CultureInfo.InvariantCulture))
                    : new RuleSetFormattedData(Value: value);

            case "formValueToText":
                return new RuleSetFormattedData(Value: MapValue(ruleSet, value));

            case "phinValueSet":
            case "unitOfMeasure":
            case "textToValueSet":
                var mapped = MapValue(ruleSet, value);
                if (string.IsNullOrWhiteSpace(mapped))
                {
                    return new RuleSetFormattedData();
                }

                return Coded(ruleSet.ValueSet, mapped) ?? new RuleSetFormattedData(Value: mapped);

            default:
                return new RuleSetFormattedData(Value: value);
        }
    }

    /// <summary>
    /// The value-map translation with its fallback chain: exact match, then the
    /// <c>null_value</c> entry for an empty value, then the <c>default_value</c>
    /// entry when nothing matched.
    /// </summary>
    private static string? MapValue(LoadedRuleSet ruleSet, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Sentinel(ruleSet, "null_value");
        }

        var match = ruleSet.ValueMaps.FirstOrDefault(map =>
            string.Equals(map.ValueFrom, value, StringComparison.OrdinalIgnoreCase));
        return match is not null ? match.ValueTo : Sentinel(ruleSet, "default_value");
    }

    private static string? Sentinel(LoadedRuleSet ruleSet, string name) =>
        ruleSet.ValueMaps
            .FirstOrDefault(map => string.Equals(map.ValueFrom, name, StringComparison.OrdinalIgnoreCase))
            ?.ValueTo;

    private static bool IsNumeric(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    private static RuleSetFormattedData? Coded(IReadOnlyList<ValueSetConcept> valueSet, string? conceptCode)
    {
        if (string.IsNullOrWhiteSpace(conceptCode))
        {
            return null;
        }

        var concept = valueSet.FirstOrDefault(c =>
            string.Equals(c.ConceptCode, conceptCode, StringComparison.OrdinalIgnoreCase));
        return concept is null
            ? null
            : new RuleSetFormattedData(
                Value: concept.PreferredConceptName,
                Code: concept.Hl7Code,
                Id: concept.ConceptCode,
                CodeSystemOid: concept.CodeSystemOid);
    }
}
