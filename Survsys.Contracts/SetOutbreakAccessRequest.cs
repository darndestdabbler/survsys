namespace Survsys.Contracts;

/// <summary>
/// The agencies to grant sight of an outbreak: what
/// <c>PUT /api/outbreaks/{id}/access</c> takes.
/// </summary>
/// <remarks>
/// The list is the whole of the outbreak's access, saved over it: a save
/// replaces the grants rather than adding to them, so the screen sends what it
/// shows. Each agency must be one this tier knows, which the endpoint refuses
/// where it is not, and an agency named twice is granted once.
/// </remarks>
/// <param name="AgencyIds">The agencies granted sight of the outbreak, or null for none.</param>
public record SetOutbreakAccessRequest(IReadOnlyList<int>? AgencyIds = null);
