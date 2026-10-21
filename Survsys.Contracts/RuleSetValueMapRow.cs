namespace Survsys.Contracts;

/// <summary>
/// A value translation of a rule set configuration; <c>null_value</c> and
/// <c>default_value</c> in <paramref name="ValueFrom"/> are its fallbacks.
/// </summary>
/// <param name="ValueFrom">The value it matches, or null.</param>
/// <param name="ValueTo">The value it maps to, or null.</param>
/// <param name="OtherSpecified">The "other, specify" text it carries, or null.</param>
public record RuleSetValueMapRow(string? ValueFrom, string? ValueTo, string? OtherSpecified);
