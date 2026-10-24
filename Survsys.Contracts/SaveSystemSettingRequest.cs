namespace Survsys.Contracts;

/// <summary>
/// A system setting's value and description, which
/// <c>PUT /api/system-settings/{name}</c> saves under the name in its address,
/// adding the setting where there is none by that name.
/// </summary>
/// <param name="Value">The setting's value, as text; empty sets it to nothing. Required.</param>
/// <param name="Description">What the setting is for, or null.</param>
public sealed record SaveSystemSettingRequest(string Value, string? Description = null);
