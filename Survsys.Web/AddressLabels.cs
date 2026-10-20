namespace Survsys.Web;

/// <summary>
/// The labels the address screens show that a deployment may name its own way.
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
}
