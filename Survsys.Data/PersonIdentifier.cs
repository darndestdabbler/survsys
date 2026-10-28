namespace Survsys.Data;

/// <summary>
/// One identifier a person is known by elsewhere — a medical record number, a
/// state identifier, a laboratory's patient id — with the authority that
/// assigned it.
/// </summary>
/// <remarks>
/// The authority is named the way HL7 names one: by a CLIA number for a
/// laboratory, or by an OID for anything else, and
/// <see cref="AssigningAuthorityType"/> says which of the two
/// <see cref="AssigningAuthority"/> holds. A value is only unique within its
/// authority, which is why the two are kept together on the row.
/// </remarks>
public class PersonIdentifier
{
    /// <summary>The <see cref="AssigningAuthorityType"/> of a CLIA number.</summary>
    public const string Clia = "CLIA";

    /// <summary>The <see cref="AssigningAuthorityType"/> of an OID.</summary>
    public const string Oid = "OID";

    public int Id { get; set; }

    /// <summary>The person this identifier names.</summary>
    public int PersonId { get; set; }

    /// <summary>What kind of identifier this is, such as <c>MR</c> for a medical record number.</summary>
    public string? IdentifierType { get; set; }

    /// <summary>The identifier itself.</summary>
    public required string Value { get; set; }

    /// <summary>The CLIA number or OID of the authority that assigned it.</summary>
    public string? AssigningAuthority { get; set; }

    /// <summary><see cref="Clia"/> or <see cref="Oid"/>: what <see cref="AssigningAuthority"/> is.</summary>
    public string? AssigningAuthorityType { get; set; }
}
