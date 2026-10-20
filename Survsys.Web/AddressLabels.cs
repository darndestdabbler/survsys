using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The labels the address screens show that a deployment may name its own way,
/// and a county code shown by its name.
/// </summary>
/// <remarks>
/// A Connecticut address's county equivalent is its planning region, which is
/// what Connecticut calls it; a deployment that would rather say what the
/// Census Bureau says sets <c>Survsys:CountyEquivalentLabel</c> to
/// "County equivalent".
/// </remarks>
public static class AddressLabels
{
    /// <summary>The configuration key the county equivalent's label is read from.</summary>
    public const string CountyEquivalentKey = "Survsys:CountyEquivalentLabel";

    /// <summary>The county equivalent's label where configuration names none.</summary>
    public const string DefaultCountyEquivalent = "Planning region";

    /// <summary>The label the county equivalent is shown under.</summary>
    public static string CountyEquivalent(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration[CountyEquivalentKey] is { } label && !string.IsNullOrWhiteSpace(label)
            ? label.Trim()
            : DefaultCountyEquivalent;
    }

    /// <summary>
    /// A county or county equivalent shown by its name: the description
    /// <paramref name="counties"/> — the county list, retired codes included —
    /// gives the code, or the code itself where the list has no description or
    /// no such code, or null where there is no code.
    /// </summary>
    public static string? CountyName(IReadOnlyList<CodeResponse> counties, string? code)
    {
        ArgumentNullException.ThrowIfNull(counties);

        return code is null
            ? null
            : counties.FirstOrDefault(county => county.Code == code)?.Description ?? code;
    }
}
