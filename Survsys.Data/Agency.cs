namespace Survsys.Data;

/// <summary>
/// A public health agency: the body a case morbidity report is assigned to,
/// and the owner of a jurisdiction.
/// </summary>
/// <remarks>
/// The columns are EpiTrax's own <c>agency</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>: an identifier, a name and a short name.
///
/// There is no jurisdiction table, because the legacy has none: EpiTrax
/// serves its jurisdictions from the agency codeset
/// (<c>EMSA_EPITRAX_CONTRACT.md</c>, "Get Jurisdictions"), and an agency's
/// jurisdiction is the zip codes <see cref="ZipCodeAgency"/> maps to it.
///
/// <see cref="Name"/> is required where the legacy column was nullable, as
/// <see cref="Condition.Name"/> is: an agency with no name cannot be chosen
/// from a list.
/// </remarks>
public class Agency
{
    public int Id { get; set; }

    /// <summary>The agency's name, as a dropdown shows it.</summary>
    public required string Name { get; set; }

    /// <summary>The agency's abbreviation, where it has one.</summary>
    public string? ShortName { get; set; }
}
