using System.Globalization;
using System.Text.RegularExpressions;

namespace Survsys.Messaging;

/// <summary>
/// The Connecticut ELR conformance profile, from
/// <c>CT-ELR-HL7-251_Implementation_Guide_v1.0.pdf</c> under
/// <c>../nedss/docs/specs/site-specs</c>: the elements its Table 4 makes
/// required beyond the national ELR guide, its OID-or-CLIA identifiers
/// (CT-ELR-003 and CT-ELR-004), its town names and its next of kin for a
/// child's blood lead.
/// </summary>
/// <remarks>
/// The CT OIDs in MSH-5 and MSH-6 differ between testing and production, so
/// the caller passes the ones its environment expects. A patient's town is
/// checked only where the address is in CT, and only as a warning: the guide
/// prefers its 169 town names but does not require them. The identifier checks
/// read the HL7 default component (<c>^</c>) and subcomponent (<c>&amp;</c>)
/// separators, the ones the guide's examples use.
/// </remarks>
public static class ConnecticutElrProfile
{
    /// <summary>The profile's name, as configuration chooses it for a sender.</summary>
    public const string Name = "CT";

    /// <summary>
    /// The blood-lead LOINCs: lead in blood, in capillary blood and in venous
    /// blood.
    /// </summary>
    public static IReadOnlySet<string> BloodLeadLoincs { get; } =
        new HashSet<string>(["5671-3", "10368-9", "77307-7"], StringComparer.Ordinal);

    /// <summary>The age, in whole years at observation, below which a patient is a child.</summary>
    public const int ChildAgeLimit = 18;

    /// <summary>The guide's User-Defined Table of Connecticut Town Names.</summary>
    public static IReadOnlySet<string> Towns { get; } = new HashSet<string>(
    [
        "Andover", "Ansonia", "Ashford", "Avon", "Barkhamsted", "Beacon Falls", "Berlin", "Bethany", "Bethel",
        "Bethlehem", "Bloomfield", "Bolton", "Bozrah", "Branford", "Bridgeport", "Bridgewater", "Bristol",
        "Brookfield", "Brooklyn", "Burlington", "Canaan", "Canterbury", "Canton", "Chaplin", "Cheshire", "Chester",
        "Clinton", "Colchester", "Colebrook", "Columbia", "Cornwall", "Coventry", "Cromwell", "Danbury", "Darien",
        "Deep River", "Derby", "Durham", "Eastford", "East Granby", "East Haddam", "East Hampton", "East Hartford",
        "East Haven", "East Lyme", "East Windsor", "Easton", "Ellington", "Enfield", "Essex", "Fairfield",
        "Farmington", "Franklin", "Glastonbury", "Goshen", "Granby", "Greenwich", "Griswold", "Groton", "Guilford",
        "Haddam", "Hamden", "Hampton", "Hartford", "Hartland", "Harwinton", "Hebron", "Kent", "Killingly",
        "Killingworth", "Lebanon", "Ledyard", "Lisbon", "Litchfield", "Lyme", "Madison", "Manchester", "Mansfield",
        "Marlborough", "Meriden", "Middlebury", "Middlefield", "Middletown", "Milford", "Monroe", "Montville",
        "Morris", "Naugatuck", "New Britain", "New Canaan", "New Fairfield", "New Hartford", "New Haven",
        "New London", "New Milford", "Newington", "Newtown", "Norfolk", "North Branford", "North Canaan",
        "North Haven", "North Stonington", "Norwalk", "Norwich", "Old Lyme", "Old Saybrook", "Orange", "Oxford",
        "Plainfield", "Plainville", "Plymouth", "Pomfret", "Portland", "Preston", "Prospect", "Putnam", "Redding",
        "Ridgefield", "Rocky Hill", "Roxbury", "Salem", "Salisbury", "Scotland", "Seymour", "Sharon", "Shelton",
        "Sherman", "Simsbury", "Somers", "South Windsor", "Southbury", "Southington", "Sprague", "Stafford",
        "Stamford", "Sterling", "Stonington", "Stratford", "Suffield", "Thomaston", "Thompson", "Tolland",
        "Torrington", "Trumbull", "Union", "Vernon", "Voluntown", "Wallingford", "Warren", "Washington",
        "Waterbury", "Waterford", "Watertown", "West Hartford", "West Haven", "Westbrook", "Weston", "Westport",
        "Wethersfield", "Willington", "Wilton", "Winchester", "Windham", "Windsor Locks", "Windsor", "Wolcott",
        "Woodbridge", "Woodbury", "Woodstock",
    ],
    StringComparer.OrdinalIgnoreCase);

    private const string AuthorityDescription = "an OID or CLIA universal ID with its ISO or CLIA type";

    private static readonly Regex Oid = new(@"^[0-9]+(\.[0-9]+)+$", RegexOptions.CultureInvariant);

    private static readonly Regex Clia = new("^[0-9]{2}D[0-9]{7}$", RegexOptions.CultureInvariant);

    /// <summary>
    /// The Connecticut profile, expecting <paramref name="receivingApplicationOid"/>
    /// in MSH-5.2 and <paramref name="receivingFacilityOid"/> in MSH-6.2.
    /// </summary>
    public static ConformanceProfile Create(string receivingApplicationOid, string receivingFacilityOid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receivingApplicationOid);
        ArgumentException.ThrowIfNullOrWhiteSpace(receivingFacilityOid);

        var entityIdentifier = Authority('^', 2);

        return new ConformanceProfile(Name,
        [
            ConformanceRule.Of("MSH-5.2", Usage.R, value => value == receivingApplicationOid, receivingApplicationOid),
            ConformanceRule.Of("MSH-6.2", Usage.R, value => value == receivingFacilityOid, receivingFacilityOid),
            ConformanceRule.Of("PID-3.4", Usage.R, Authority('&', 1), AuthorityDescription),
            ConformanceRule.Of("PID-11", Usage.O, IsConnecticutTownOrOutOfState, "in one of the 169 CT towns"),
            ConformanceRule.Conditional("NK1", ConditionalUsage.RequiredElseOptional, IsChildBloodLead),
            ConformanceRule.Of("ORC-2", Usage.R, entityIdentifier, AuthorityDescription),
            ConformanceRule.Of("ORC-3", Usage.R, entityIdentifier, AuthorityDescription),
            ConformanceRule.Of("ORC-24", Usage.R),
            ConformanceRule.Of("OBR-2", Usage.R, entityIdentifier, AuthorityDescription),
            ConformanceRule.Of("OBR-3", Usage.R, entityIdentifier, AuthorityDescription),
            ConformanceRule.Of("OBR-16.2", Usage.R),
            ConformanceRule.Of("OBR-16.3", Usage.R),
            ConformanceRule.Of("OBR-17", Usage.R),
            ConformanceRule.Of("OBX-2", Usage.R),
            ConformanceRule.Of("SPM-2.2", Usage.R, Authority('&', 2), AuthorityDescription),
        ]);
    }

    /// <summary>
    /// The check of an assigning authority: split on <paramref name="separator"/>,
    /// the universal ID at <paramref name="idIndex"/> is an OID typed ISO or a
    /// CLIA number typed CLIA in the part after it.
    /// </summary>
    private static Func<string, bool> Authority(char separator, int idIndex) => value =>
    {
        var parts = value.Split(separator);
        var id = idIndex < parts.Length ? parts[idIndex] : string.Empty;
        var type = idIndex + 1 < parts.Length ? parts[idIndex + 1] : string.Empty;

        return (type == "ISO" && Oid.IsMatch(id)) || (type == "CLIA" && Clia.IsMatch(id));
    };

    /// <summary>An address (XAD) outside CT, or in CT with a town the guide names.</summary>
    private static bool IsConnecticutTownOrOutOfState(string address)
    {
        var parts = address.Split('^');
        var state = parts.Length > 3 ? parts[3].Trim() : string.Empty;
        var city = parts.Length > 2 ? parts[2].Trim() : string.Empty;

        return !string.Equals(state, "CT", StringComparison.OrdinalIgnoreCase) || Towns.Contains(city);
    }

    /// <summary>
    /// Whether the message reports a blood-lead result (OBX-3.1) for a patient
    /// under <see cref="ChildAgeLimit"/> at the observation (OBR-7). A patient
    /// whose age cannot be read is not taken for a child.
    /// </summary>
    private static bool IsChildBloodLead(Er7Message message)
    {
        if (!message.Values("OBX-3.1").Any(BloodLeadLoincs.Contains))
        {
            return false;
        }

        var birth = ReadDate(message.Values("PID-7").FirstOrDefault());
        var observed = ReadDate(message.Values("OBR-7").FirstOrDefault());
        if (birth is not { } born || observed is not { } on)
        {
            return false;
        }

        return born.AddYears(ChildAgeLimit) > on;
    }

    private static DateOnly? ReadDate(string? hl7) =>
        hl7 is { Length: >= 8 }
        && DateOnly.TryParseExact(hl7[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
}
