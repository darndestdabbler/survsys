namespace Survsys.Contracts;

/// <summary>
/// One PHIN variable of an exported guide, with the rule set that formats it.
/// The variable's rule set ids are the exporting system's and are not read on
/// import: the type travels by <paramref name="RuleSetType"/> and the
/// configuration whole as <paramref name="RuleSet"/>.
/// </summary>
/// <param name="Variable">The variable's definition.</param>
/// <param name="RuleSetType">The label of the rule set type that formats it (for example <c>fieldValueMap</c>), or null.</param>
/// <param name="RuleSet">The rule set configuration that formats it, or null.</param>
public record GuideExportVariable(PhinVariableRow Variable, string? RuleSetType, RuleSetConfigRow? RuleSet);
