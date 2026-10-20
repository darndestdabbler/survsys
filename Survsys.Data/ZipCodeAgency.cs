namespace Survsys.Data;

/// <summary>
/// One zip code in one agency's jurisdiction: the row the zip-to-agency
/// mapping is made of.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>zip_code_agency</c> table, keyed as the legacy keys it on
/// the pair rather than on the zip alone. A zip code that straddles a
/// jurisdiction line can be claimed by two agencies, and
/// <see cref="ZipAgencyRules.Resolve"/> is what decides what such a zip
/// resolves to. The zip is stored as its five digits, as
/// <see cref="ZipAgencyRules.Normalize"/> gives them.
/// </remarks>
public class ZipCodeAgency
{
    /// <summary>The five-digit zip code.</summary>
    public required string ZipCode { get; set; }

    /// <summary>The agency whose jurisdiction includes the zip code.</summary>
    public int AgencyId { get; set; }
}
