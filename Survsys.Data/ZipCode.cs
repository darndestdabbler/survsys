namespace Survsys.Data;

/// <summary>
/// One zip code and the county it lies in: what fills an address's county
/// when it was saved without one.
/// </summary>
/// <remarks>
/// EpiTrax's own <c>zip_code</c> table. The county is the code the
/// <c>county</c> code list holds, which is what an address stores. The table
/// is created empty; its rows come with the legacy data.
/// </remarks>
public class ZipCode
{
    /// <summary>The five-digit zip code.</summary>
    public required string Code { get; set; }

    /// <summary>The county code of the county the zip code lies in.</summary>
    public string? County { get; set; }
}
