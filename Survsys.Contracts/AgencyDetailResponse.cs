namespace Survsys.Contracts;

/// <summary>
/// One agency as its administration screen shows it: the agency, and the zip
/// codes of its jurisdiction.
/// </summary>
/// <param name="Id">The agency's identifier.</param>
/// <param name="Name">The agency's name.</param>
/// <param name="ShortName">The agency's abbreviation, or null where it has none.</param>
/// <param name="ZipCodes">
/// The five-digit zip codes mapped to the agency, in order. A zip code another
/// agency also claims is listed under both.
/// </param>
/// <param name="NotificationEmails">
/// The addresses the agency's batch notification is sent to, separated by
/// semicolons, or null where it is sent none.
/// </param>
public record AgencyDetailResponse(
    int Id,
    string Name,
    string? ShortName,
    IReadOnlyList<string> ZipCodes,
    string? NotificationEmails = null);
