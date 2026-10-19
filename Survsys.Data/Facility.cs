namespace Survsys.Data;

/// <summary>
/// A facility: a hospital, laboratory or clinic that reports, diagnoses or
/// tests, and the agency it sits under.
/// </summary>
/// <remarks>
/// The columns are the legacy <c>facility</c> table's name, short name and
/// agency, as <c>../nedss/docs/db-snapshots/session-011/05-after-admin-explore.txt</c>
/// dumps them, with the legacy <c>facility_address</c> row's street, city,
/// state, zip and phone carried on the facility itself: every facility in
/// that snapshot has exactly one address, so a second table would hold one
/// row per facility and nothing more. The legacy facility type, licensing
/// and accreditation columns are not here; no screen this schema serves
/// reads them.
/// </remarks>
public class Facility
{
    public int Id { get; set; }

    /// <summary>The facility's name.</summary>
    public required string Name { get; set; }

    /// <summary>The facility's abbreviation, where it has one.</summary>
    public string? ShortName { get; set; }

    /// <summary>The agency the facility sits under.</summary>
    public int AgencyId { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    /// <summary>The state's two-letter code.</summary>
    public string? State { get; set; }

    /// <summary>The five-digit zip code.</summary>
    public string? Zip { get; set; }

    public string? Phone { get; set; }
}
