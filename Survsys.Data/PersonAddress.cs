namespace Survsys.Data;

/// <summary>
/// One address of a person, over the dates it was theirs.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>person_address</c> table, with the columns the Addresses
/// section of the Demographic tab shows — street, unit number, city, state,
/// zip code, county, residence type, earliest known, moved and a note, as
/// <c>../nedss/docs/functional/epitrax-cmr-workflow.md</c> lists them.
///
/// A person has as many of these as they have had addresses, which is why
/// <see cref="Beginning"/> and <see cref="Ending"/> are here: the one with no
/// end date is where they live now, and the ones with end dates are where they
/// lived when an exposure happened. Both are dates and not timestamps, because
/// <c>V0143__add_person_address_begin_end_date.sql</c> made them
/// <c>date</c> in the legacy schema.
///
/// <see cref="County"/> is the column the legacy system routes a case by — a
/// report belongs to the local health department of the county the person
/// lives in — so it is its own column rather than something read back out of
/// the city.
///
/// The geocoding columns (<c>latitude</c>, <c>longitude</c>), the verification
/// timestamp (<c>last_verified</c>) and <c>homelessness</c> are not here: none
/// of them is a field the Demographic tab shows, and this session's schema is
/// what that tab saves.
/// </remarks>
public class PersonAddress
{
    public int Id { get; set; }

    /// <summary>The person whose address this is.</summary>
    public int PersonId { get; set; }

    /// <summary>The street line, house number included.</summary>
    public string? Street { get; set; }

    /// <summary>An apartment, suite or unit within the street address.</summary>
    public string? UnitNumber { get; set; }

    public string? City { get; set; }

    /// <summary>The state, as the legacy two-letter code.</summary>
    public string? State { get; set; }

    /// <summary>The zip code, as the screen labels it.</summary>
    public string? PostalCode { get; set; }

    /// <summary>
    /// The county, as the legacy county code. What a case is routed to a local
    /// health department by.
    /// </summary>
    public string? County { get; set; }

    /// <summary>
    /// The earliest date this is known to have been the person's address.
    /// </summary>
    public DateOnly? Beginning { get; set; }

    /// <summary>
    /// The date the person moved out of it, and null while it is still theirs.
    /// </summary>
    public DateOnly? Ending { get; set; }

    /// <summary>Whatever the investigator wrote about this address.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// The kind of residence this is — the legacy <c>residence_type</c> the
    /// screen offers as a dropdown. A bare identifier rather than a
    /// navigation, until that table exists.
    /// </summary>
    public int? ResidenceTypeId { get; set; }
}
