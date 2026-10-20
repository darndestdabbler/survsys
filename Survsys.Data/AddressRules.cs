using System.Globalization;
using System.Text.RegularExpressions;

namespace Survsys.Data;

/// <summary>
/// What an address is made into when it is saved: its text tidied, its state a
/// state code and its zip written in full, and of a person's addresses one
/// current and the rest historic.
/// </summary>
/// <remarks>
/// Pure functions, as <see cref="ZipAgencyRules"/> is, so the unit suite
/// proves them without a database. Where a value is not one a rule
/// recognises — a zip of the wrong length, a state no code list names — it is
/// kept as typed, tidied of its whitespace: a rule here makes a value
/// regular, and never throws one away.
/// </remarks>
public static partial class AddressRules
{
    private static readonly Dictionary<string, string> StateCodes = CodeValueSeed.Rows
        .Where(row => row.List == "state")
        .SelectMany(row => new[] { (Key: row.Code, row.Code), (Key: row.Description ?? row.Code, row.Code) })
        .DistinctBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(pair => pair.Key, pair => pair.Code, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, CtTown> ConnecticutTowns = CtTownSeed.Rows
        .ToDictionary(row => row.Town, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A value trimmed, with every run of whitespace inside it made one space,
    /// or null where nothing was said.
    /// </summary>
    public static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Whitespace().Replace(value.Trim(), " ");

    /// <summary>
    /// A value as <see cref="Text"/> gives it, in title case: <c>123 MAIN st</c>
    /// is <c>123 Main St</c>.
    /// </summary>
    public static string? Titled(string? value) =>
        Text(value) is { } text
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant())
            : null;

    /// <summary>
    /// The two-letter code of the state a value names, by its code or its name
    /// in either case — <c>utah</c> and <c>ut</c> are both <c>UT</c>.
    /// </summary>
    public static string? StateCode(string? value) =>
        Text(value) is { } text
            ? StateCodes.GetValueOrDefault(text, text)
            : null;

    /// <summary>
    /// A zip code as five digits, or as ZIP+4 written <c>84101-1234</c> where it
    /// has nine digits, whether they were typed together, with a space or with
    /// a hyphen.
    /// </summary>
    public static string? PostalCode(string? value)
    {
        if (Text(value) is not { } text)
        {
            return null;
        }

        var digits = text.Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!digits.All(char.IsAsciiDigit))
        {
            return text;
        }

        return digits.Length switch
        {
            ZipAgencyRules.ZipLength => digits,
            ZipAgencyRules.ZipLength + 4 => $"{digits[..ZipAgencyRules.ZipLength]}-{digits[ZipAgencyRules.ZipLength..]}",
            _ => text,
        };
    }

    /// <summary>
    /// Makes an address's text regular: the street, unit and city in title
    /// case, the state its code, the zip in full and the county code in upper
    /// case, each tidied of its whitespace, as is the county equivalent's.
    /// </summary>
    public static void Normalize(PersonAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        address.Street = Titled(address.Street);
        address.UnitNumber = Titled(address.UnitNumber);
        address.City = Titled(address.City);
        address.State = StateCode(address.State);
        address.PostalCode = PostalCode(address.PostalCode);
        address.County = Text(address.County)?.ToUpperInvariant();
        address.CountyEquivalent = Text(address.CountyEquivalent)?.ToUpperInvariant();
        address.Note = Text(address.Note);
    }

    /// <summary>
    /// Fills an address's county values where it was saved without them: for
    /// a Connecticut address, the county and the county equivalent (its
    /// planning region) from its town, and for any address a county still
    /// blank from its zip by <paramref name="countiesByZip"/> — five-digit
    /// zips to county codes.
    /// </summary>
    /// <remarks>
    /// The town comes first because a Connecticut zip can cross a planning
    /// region's boundary and a town cannot, so the zip is the fallback for an
    /// unknown town and never fills a planning region. A value given is kept.
    /// Called after <see cref="Normalize"/>, so a blank value is null.
    /// </remarks>
    public static void FillConnecticutCounties(
        PersonAddress address, IReadOnlyDictionary<string, string>? countiesByZip)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.State == "CT" && address.City is { } city
            && ConnecticutTowns.GetValueOrDefault(city) is { } town)
        {
            address.County ??= town.County;
            address.CountyEquivalent ??= town.PlanningRegion;
        }

        if (address.County is null
            && ZipAgencyRules.Normalize(address.PostalCode) is { } zip
            && countiesByZip?.GetValueOrDefault(zip) is { } county)
        {
            address.County = county;
        }
    }

    /// <summary>
    /// Leaves one of a person's addresses current and makes the rest historic.
    /// </summary>
    /// <remarks>
    /// Of the addresses with no ending, the one that began last is current —
    /// one with no beginning counts as earliest, and of two that began the
    /// same day the later in the list wins, because it is the one added last.
    /// Every other open address ends the day the current one began, or
    /// <paramref name="today"/> where it has no beginning: that is the day the
    /// person is known to have moved.
    /// </remarks>
    public static void CloseHistoric(IEnumerable<PersonAddress> addresses, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        var open = addresses.Where(address => address.Ending is null).ToList();
        if (open.Count < 2)
        {
            return;
        }

        var current = open
            .Select((address, at) => (address, at))
            .OrderBy(pair => pair.address.Beginning ?? DateOnly.MinValue)
            .ThenBy(pair => pair.at)
            .Last()
            .address;

        foreach (var address in open.Where(address => address != current))
        {
            address.Ending = current.Beginning ?? today;
        }
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
