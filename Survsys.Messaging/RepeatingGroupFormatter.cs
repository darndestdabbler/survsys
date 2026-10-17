namespace Survsys.Messaging;

/// <summary>
/// The <c>repeatingGroup</c> ruleset: a placeholder. Repeating-group processing
/// is handled by the repeating-group processor, not the standard formatter
/// pipeline, so this type returns a warning when it is driven directly.
/// </summary>
/// <remarks>
/// Follows the legacy NMI <c>repeatingGroup</c> formatter described in
/// <c>../nedss/docs/business-logic/NMI_RULESETS.md</c>, which warns if called
/// directly because the repeating groups are expanded at a higher level.
/// </remarks>
public sealed class RepeatingGroupFormatter : IRuleSetFormatter
{
    /// <inheritdoc />
    public IReadOnlyList<RuleSetFormattedData> Format(
        LoadedRuleSet ruleSet,
        EpiTraxRecord record,
        string? dataSet = null) =>
        [new RuleSetFormattedData(
            Message: "repeatingGroup is handled by the repeating-group processor, "
                + "not the standard formatter",
            MessageKind: RuleSetMessageKind.Warning)];
}
