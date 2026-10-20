namespace Survsys.Contracts;

/// <summary>
/// A facility to create or to save over an existing one: what
/// <c>POST /api/facilities</c> and <c>PUT /api/facilities/{id}</c> take.
/// </summary>
/// <remarks>
/// A blank optional value is stored as null, and a ZIP+4 as its five-digit
/// zip. The agency is one <c>GET /api/agencies</c> lists.
/// </remarks>
/// <param name="Name">The facility's name. Required.</param>
/// <param name="AgencyId">The agency the facility sits under. Required.</param>
/// <param name="ShortName">The facility's abbreviation, or null.</param>
/// <param name="Street">The street address, or null.</param>
/// <param name="City">The city, or null.</param>
/// <param name="State">The state's two-letter code, or null.</param>
/// <param name="Zip">The zip code, five digits or ZIP+4, or null.</param>
/// <param name="Phone">The telephone number, or null.</param>
public record SaveFacilityRequest(
    string Name,
    int AgencyId,
    string? ShortName = null,
    string? Street = null,
    string? City = null,
    string? State = null,
    string? Zip = null,
    string? Phone = null);
