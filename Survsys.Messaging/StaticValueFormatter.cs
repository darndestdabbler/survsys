using System.Globalization;

namespace Survsys.Messaging;

/// <summary>
/// The <c>staticValue</c> ruleset: outputs a literal from the configuration's
/// data, with no EpiTrax lookup, formatted by the configuration's format type.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>staticValue</c> formatter described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: <c>string</c> returns
/// the literal as-is, <c>numeric</c> validates it, <c>date</c> formats the
/// current time by the configured date format, and <c>phinValueSet</c> or
/// <c>unitOfMeasure</c> resolve the literal as a concept code through the
/// supplied value set. The date format is a .NET format the caller supplies;
/// the current time comes from an injected <see cref="TimeProvider"/> so the
/// date path is deterministic in a test.
/// </remarks>
public sealed class StaticValueFormatter : IRuleSetFormatter
{
    private readonly TimeProvider _timeProvider;

    /// <summary>Builds the formatter over a clock, the system clock by default.</summary>
    public StaticValueFormatter(TimeProvider? timeProvider = null) =>
        _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var literal = ruleSet.Data.TryGetValue("staticValue", out var value) ? value : null;
        var response = Produce(ruleSet, literal);
        return RepeatingValues.Apply(ruleSet.Repeating, [response]);
    }

    private RuleSetFormattedData Produce(LoadedRuleSet ruleSet, string? literal)
    {
        switch (ruleSet.FormatType)
        {
            case "numeric":
                return IsNumeric(literal)
                    ? new RuleSetFormattedData(Value: literal)
                    : new RuleSetFormattedData(
                        Message: $"static value '{literal}' is not numeric",
                        MessageKind: RuleSetMessageKind.Error);

            case "date":
                var format = ruleSet.Data.TryGetValue("dateFormat", out var f) ? f : null;
                return string.IsNullOrWhiteSpace(format)
                    ? new RuleSetFormattedData(
                        Message: "a date static value needs a date format",
                        MessageKind: RuleSetMessageKind.Error)
                    : new RuleSetFormattedData(
                        Value: _timeProvider.GetUtcNow().ToString(format, CultureInfo.InvariantCulture));

            case "phinValueSet":
            case "unitOfMeasure":
                return Coded(ruleSet.ValueSet, literal)
                    ?? new RuleSetFormattedData(
                        Message: $"static value '{literal}' is not in value set '{ruleSet.ValueSetName}'",
                        MessageKind: RuleSetMessageKind.Error);

            default:
                return new RuleSetFormattedData(Value: literal);
        }
    }

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
