namespace Survsys.Contracts;

/// <summary>
/// A notification guide's own fields, as
/// <c>/api/notification-setup/guides</c> lists, creates and saves them.
/// </summary>
/// <param name="Id">The guide's id; ignored when one is created.</param>
/// <param name="Name">The guide's name.</param>
/// <param name="Version">
/// The guide's version, the profile its message is generated for (for example
/// <c>Arbo_Case_Map_v1.0</c>); or null.
/// </param>
public record GuideRow(int Id, string Name, string? Version);
