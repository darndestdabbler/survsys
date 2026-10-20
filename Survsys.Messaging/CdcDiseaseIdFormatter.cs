namespace Survsys.Messaging;

/// <summary>
/// The <c>cdcDiseaseId</c> ruleset: a placeholder. The EVENT (disease code)
/// field is resolved from the condition's own ruleset, not through the standard
/// formatter pipeline, so this type returns only an informational note when it
/// is driven directly.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>cdcDiseaseId</c> formatter described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>: the XML generator
/// substitutes the condition's ruleset for a PHIN variable of this type, so the
/// formatter itself produces no value.
/// </remarks>
public sealed class CdcDiseaseIdFormatter : IRuleSetFormatter
{
    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null) =>
        [new RuleSetFormattedData(
            Message: "cdcDiseaseId is resolved from the condition's own ruleset, "
                + "not through the standard formatter",
            MessageKind: RuleSetMessageKind.Info)];
}
