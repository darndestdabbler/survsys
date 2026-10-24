namespace Survsys.Contracts;

/// <summary>
/// One address of a person, as the Addresses section of the Demographic tab
/// shows it.
/// </summary>
/// <remarks>
/// It carries its own identifier, so a screen showing two addresses can tell
/// them apart and say which one it is editing.
///
/// <see cref="Ending"/> being null is what "this is where they live now"
/// looks like on the wire; nothing here says which address is current,
/// because the dates already do.
/// </remarks>
/// <param name="Id">The address's identifier.</param>
/// <param name="Street">The street line, house number included.</param>
/// <param name="UnitNumber">An apartment, suite or unit within the street address.</param>
/// <param name="City">The city.</param>
/// <param name="State">The state, as the legacy two-letter code.</param>
/// <param name="PostalCode">The zip code, as the screen labels it.</param>
/// <param name="County">The county, as the legacy county code — what a case is routed by.</param>
/// <param name="Beginning">The earliest date this is known to have been the person's address.</param>
/// <param name="Ending">The date the person moved out, and null while the address is still theirs.</param>
/// <param name="Note">Whatever the investigator wrote about this address.</param>
/// <param name="ResidenceTypeId">The legacy residence type, as its bare identifier.</param>
/// <param name="CountyEquivalent">
/// The county equivalent, as a county code of that kind: a Connecticut
/// address's planning region, beside its historical county.
/// </param>
/// <param name="Latitude">The latitude, in decimal degrees, and null where none is known.</param>
/// <param name="Longitude">The longitude, in decimal degrees, and null where none is known.</param>
/// <param name="GeocodedBy">Where the coordinates came from: census, google or manual.</param>
public record PersonAddressResponse(
    int Id,
    string? Street,
    string? UnitNumber,
    string? City,
    string? State,
    string? PostalCode,
    string? County,
    DateOnly? Beginning,
    DateOnly? Ending,
    string? Note,
    int? ResidenceTypeId,
    string? CountyEquivalent = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? GeocodedBy = null);
