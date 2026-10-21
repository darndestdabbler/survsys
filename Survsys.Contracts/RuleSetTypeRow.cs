namespace Survsys.Contracts;

/// <summary>One of the seven rule set types a PHIN variable's value is formatted by.</summary>
/// <param name="Id">The type's fixed id.</param>
/// <param name="Label">The type's label (for example <c>fieldValueMap</c>).</param>
public record RuleSetTypeRow(int Id, string Label);
