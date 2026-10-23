namespace Survsys.Messaging;

/// <summary>
/// The <c>conditionMap</c> ruleset: evaluates ordered conditional expressions
/// against the record and formats the matched value, falling back to a
/// configured default when none match.
/// </summary>
/// <remarks>
/// Follows the expression-based path of the legacy NMI <c>conditionMap</c>
/// formatter described in <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>:
/// the expressions are evaluated in execution order through the
/// <see cref="ExpressionEvaluator"/>, each matching expression contributes its
/// value, and the <c>defaultValue</c> data parameter is used when none match.
/// The matched value is formatted by the format type: <c>text</c> returns it
/// as-is, <c>phinValueSet</c> or <c>unitOfMeasure</c> resolve it as a concept
/// code through the value set, and <c>field</c> treats it as a field name and
/// extracts that field's value.
/// </remarks>
public sealed class ConditionMapFormatter : IRuleSetFormatter
{
    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(record);

        var matched = ruleSet.Expressions
            .OrderBy(expression => expression.ExecutionOrder)
            .Where(expression => ExpressionEvaluator.Matches(expression, record))
            .Select(expression => expression.Value)
            .Where(value => value is not null)
            .Select(value => value!)
            .ToList();

        if (matched.Count == 0)
        {
            var defaultValue = ruleSet.Data.TryGetValue("defaultValue", out var d) ? d : null;
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                matched.Add(defaultValue);
            }
        }

        var responses = matched.Select(value => FormatMatched(ruleSet, record, value)).ToList();
        return RepeatingValues.Apply(ruleSet.Repeating, responses);
    }

    private static RuleSetFormattedData FormatMatched(LoadedRuleSet ruleSet, EpiTraxRecord record, string value)
    {
        switch (ruleSet.FormatType)
        {
            case "phinValueSet":
            case "unitOfMeasure":
                return Coded(ruleSet.ValueSet, value) ?? new RuleSetFormattedData(Value: value);

            case "field":
                return new RuleSetFormattedData(Value: record.Value(value));

            default:
                return new RuleSetFormattedData(Value: value);
        }
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
