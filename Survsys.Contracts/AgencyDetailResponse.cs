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
public record AgencyDetailResponse(
    int Id,
    string Name,
    string? ShortName,
    IReadOnlyList<string> ZipCodes);
