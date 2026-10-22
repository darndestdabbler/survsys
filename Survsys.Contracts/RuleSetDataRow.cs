namespace Survsys.Contracts;

/// <summary>A named parameter of a rule set configuration.</summary>
/// <param name="Name">The parameter's name (for example <c>dateFormat</c>).</param>
/// <param name="Value">Its value, or null.</param>
public record RuleSetDataRow(string Name, string? Value);
