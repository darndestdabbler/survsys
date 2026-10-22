namespace Survsys.Contracts;

/// <summary>
/// A notification guide as JSON, exported from one system and imported into
/// another: its name and version, which an import matches it by, and the PHIN
/// variables it carries with how each is formatted.
/// </summary>
/// <param name="Name">The guide's name.</param>
/// <param name="Version">The guide's version, or null.</param>
/// <param name="PhinVars">The variables it carries, in order.</param>
public record GuideExport(string Name, string? Version, IReadOnlyList<GuideExportVariable> PhinVars);
