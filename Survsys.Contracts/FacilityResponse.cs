namespace Survsys.Contracts;

/// <summary>
/// A facility — a hospital, laboratory or clinic — with the agency it sits
/// under and its address.
/// </summary>
/// <param name="Id">The facility's identifier.</param>
/// <param name="Name">The facility's name.</param>
/// <param name="ShortName">The facility's abbreviation, or null where it has none.</param>
/// <param name="AgencyId">The agency the facility sits under, as <c>GET /api/agencies</c> lists it.</param>
/// <param name="Street">The street address, or null.</param>
/// <param name="City">The city, or null.</param>
/// <param name="State">The state's two-letter code, or null.</param>
/// <param name="Zip">The five-digit zip code, or null.</param>
/// <param name="Phone">The telephone number, or null.</param>
public record FacilityResponse(
    int Id,
    string Name,
    string? ShortName,
    int AgencyId,
    string? Street,
    string? City,
    string? State,
    string? Zip,
    string? Phone);
