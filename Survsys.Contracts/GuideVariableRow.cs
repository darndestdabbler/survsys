namespace Survsys.Contracts;

/// <summary>A guide's value for one header variable.</summary>
/// <param name="GuideVariableDefaultId">The variable, by its id in the variable defaults.</param>
/// <param name="Name">The variable's name; ignored when saved.</param>
/// <param name="Value">The guide's own value, or null where it uses the default.</param>
/// <param name="DefaultValue">The variable's default value; ignored when saved.</param>
public record GuideVariableRow(int GuideVariableDefaultId, string? Name, string? Value, string? DefaultValue);
