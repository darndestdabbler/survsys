namespace Survsys.Contracts;

/// <summary>
/// One address of a person, as the Demographic tab sends it to be saved.
/// </summary>
/// <remarks>
/// <see cref="PersonAddressResponse"/> without its identifier: an identifier
/// is the API tier's to assign, and a save replaces the stored rows with what
/// is sent rather than matching them up, so there is nothing for a caller to
/// name a row by. Every other field is the response's, with the response's
/// meaning — <see cref="Ending"/> null is an address the person still lives
/// at.
/// </remarks>
/// <param name="Street">The street line, house number included.</param>
/// <param name="UnitNumber">An apartment, suite or unit within the street address.</param>
/// <param name="City">The city.</param>
/// <param name="State">The state, as the legacy two-letter code.</param>
/// <param name="PostalCode">The zip code, as the screen labels it.</param>
/// <param name="County">The county, as the legacy county code.</param>
/// <param name="Beginning">The earliest date this is known to have been the person's address.</param>
/// <param name="Ending">The date the person moved out, and null while the address is still theirs.</param>
/// <param name="Note">Whatever the investigator wrote about this address.</param>
/// <param name="ResidenceTypeId">The legacy residence type, as its bare identifier.</param>
public record PersonAddressRequest(
    string? Street = null,
    string? UnitNumber = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? County = null,
    DateOnly? Beginning = null,
    DateOnly? Ending = null,
    string? Note = null,
    int? ResidenceTypeId = null);
