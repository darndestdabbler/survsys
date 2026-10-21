namespace Survsys.Contracts;

/// <summary>
/// A PHIN variable, keyed by its identifier, as
/// <c>/api/notification-setup/phin-variables</c> lists and saves it.
/// </summary>
/// <param name="PhinVar">The variable's identifier (for example <c>INV163</c>); the key.</param>
/// <param name="Description">The variable's name.</param>
/// <param name="Identifier">The LOINC or other standard identifier it carries, or null.</param>
/// <param name="Priority">The legacy processing priority, or null.</param>
/// <param name="Hl7DataType">The HL7 data type its value is written as, or null.</param>
/// <param name="RuleSetId">The rule set type that formats it, as GET /api/notification-setup/rule-set-types lists; or null.</param>
/// <param name="RuleSetConfigId">The rule set configuration that formats it, or null.</param>
/// <param name="Status">The variable's status, or null.</param>
/// <param name="Note">An administrator's note, or null.</param>
/// <param name="CodeSystem">The code system its coded values come from, or null.</param>
public record PhinVariableRow(
    string PhinVar,
    string Description,
    string? Identifier,
    string? Priority,
    string? Hl7DataType,
    int? RuleSetId,
    int? RuleSetConfigId,
    string? Status,
    string? Note,
    string? CodeSystem);
