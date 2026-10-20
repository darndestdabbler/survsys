namespace Survsys.Contracts;

/// <summary>
/// An agency to create or to save over an existing one: what
/// <c>POST /api/agencies</c> and <c>PUT /api/agencies/{id}</c> take.
/// </summary>
/// <remarks>
/// The zip codes are the whole of the agency's jurisdiction, saved with it: a
/// save replaces the list rather than adding to it, so the screen sends what it
/// shows. A ZIP+4 is accepted and stored as its five-digit zip; a value that is
/// not a zip code is refused.
/// </remarks>
/// <param name="Name">The agency's name. Required.</param>
/// <param name="ShortName">The agency's abbreviation, or null for none.</param>
/// <param name="ZipCodes">The zip codes of the agency's jurisdiction, or null for none.</param>
/// <param name="NotificationEmails">
/// The addresses the agency's batch notification of assigned messages is sent
/// to, separated by semicolons or commas, or null for none.
/// </param>
public record SaveAgencyRequest(
    string Name,
    string? ShortName = null,
    IReadOnlyList<string>? ZipCodes = null,
    string? NotificationEmails = null);
