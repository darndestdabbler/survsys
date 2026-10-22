namespace Survsys.Contracts;

/// <summary>
/// One system configuration setting as <c>/api/system-settings</c> answers it.
/// </summary>
/// <param name="Name">The setting's name, by which it is chosen.</param>
/// <param name="Value">The setting's value, as text.</param>
/// <param name="Description">What the setting is for, or null.</param>
public sealed record SystemSettingResponse(string Name, string Value, string? Description);
