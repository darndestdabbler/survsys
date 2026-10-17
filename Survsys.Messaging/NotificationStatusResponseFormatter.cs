namespace Survsys.Messaging;

/// <summary>
/// The <c>notificationStatusResponse</c> ruleset: maps a notification response
/// status value to a PHIN concept code and resolves it through the value set.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>notificationStatusResponse</c> formatter described
/// in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the configured
/// fields' values are extracted and pipe-delimited values split, each is mapped
/// case-insensitively through the value map to a concept code, and the code is
/// resolved in the value set to its full coded value. The repeating flag then
/// decides whether every response or only the first is kept.
/// </remarks>
public sealed class NotificationStatusResponseFormatter : IRuleSetFormatter
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
            foreach (var value in record.Values(field))
            {
                var code = ruleSet.ValueMaps
                    .FirstOrDefault(map =>
                        string.Equals(map.ValueFrom, value, StringComparison.OrdinalIgnoreCase))
                    ?.ValueTo;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                responses.Add(Coded(ruleSet.ValueSet, code) ?? new RuleSetFormattedData(Value: code));
            }
        }

        return RepeatingValues.Apply(ruleSet.Repeating, responses);
    }

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
