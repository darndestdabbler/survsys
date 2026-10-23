namespace Survsys.Data;

/// <summary>
/// What a zip code resolves to against the zip-to-agency mapping.
/// </summary>
public enum ZipResolutionOutcome
{
    /// <summary>Exactly one agency's jurisdiction includes the zip.</summary>
    Resolved,

    /// <summary>No agency's jurisdiction includes the zip.</summary>
    Unmapped,

    /// <summary>
    /// More than one agency's jurisdiction includes the zip, so it names no
    /// single agency and a person has to choose.
    /// </summary>
    Ambiguous,

    /// <summary>The value is not a zip code at all.</summary>
    NotAZip,
}

/// <summary>
/// The answer <see cref="ZipAgencyRules.Resolve"/> gives: the outcome, and the
/// agency when, and only when, the outcome is
/// <see cref="ZipResolutionOutcome.Resolved"/>.
/// </summary>
public sealed record ZipResolution(ZipResolutionOutcome Outcome, int? AgencyId = null);

/// <summary>
/// The zip-to-agency rules: reading a zip code into the five digits the
/// mapping is keyed on, and resolving it to the one agency whose jurisdiction
/// includes it.
/// </summary>
/// <remarks>
/// The one jurisdiction rule, as <c>phase-1.6-cross-cutting-patterns.md</c>
/// section 7 recommends in place of the legacy's two independent mappings.
/// Pure, and public and static in the shape <see cref="CaseStatusRules"/> has,
/// because the unit suite proves it without a database: the caller reads the
/// mapping rows and hands them over.
/// </remarks>
public static class ZipAgencyRules
{
    /// <summary>How many digits a zip code is stored as.</summary>
    public const int ZipLength = 5;

    /// <summary>
    /// The five digits of a zip code, or null where the value is not one.
    /// </summary>
    /// <remarks>
    /// Surrounding whitespace is trimmed, and a ZIP+4 (<c>84108-1234</c>) is
    /// cut to its five-digit zip, because a jurisdiction is drawn on the
    /// five-digit zip and an address can carry either form. Anything else —
    /// empty, too short, letters, a ZIP+4 whose extension is not four digits —
    /// is not a zip code.
    /// </remarks>
    public static string? Normalize(string? zip)
    {
        var value = zip?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.Length == ZipLength + 5 && value[ZipLength] == '-'
            && value[(ZipLength + 1)..].All(char.IsAsciiDigit))
        {
            value = value[..ZipLength];
        }

        return value.Length == ZipLength && value.All(char.IsAsciiDigit) ? value : null;
    }

    /// <summary>
    /// The agency a zip code resolves to against the mapping, or why it
    /// resolves to none.
    /// </summary>
    /// <remarks>
    /// The mapping may hold rows for other zip codes; only the rows for this
    /// zip are read. A zip two agencies claim resolves to neither, because
    /// choosing one would assign a case to an agency on the order the rows came
    /// back in.
    /// </remarks>
    public static ZipResolution Resolve(string? zip, IEnumerable<ZipCodeAgency> mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (Normalize(zip) is not { } code)
        {
            return new ZipResolution(ZipResolutionOutcome.NotAZip);
        }

        var agencies = mapping
            .Where(row => row.ZipCode == code)
            .Select(row => row.AgencyId)
            .Distinct()
            .ToList();

        return agencies.Count switch
        {
            0 => new ZipResolution(ZipResolutionOutcome.Unmapped),
            1 => new ZipResolution(ZipResolutionOutcome.Resolved, agencies[0]),
            _ => new ZipResolution(ZipResolutionOutcome.Ambiguous),
        };
    }
}
