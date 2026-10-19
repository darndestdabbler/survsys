namespace Survsys.Contracts;

/// <summary>
/// One person's whole demographic record: what the Demographic tab of a case
/// morbidity report shows, in one response.
/// </summary>
/// <remarks>
/// Everything <see cref="PersonResponse"/> carries, and then what a list has
/// no use for: the rest of the demographic fields, and the rows that hang off
/// a person.
///
/// The three multi-valued fields that are nothing but a value — the races, the
/// aliases and the parents or guardians — are string lists rather than record
/// lists. A multi-select race is a list of codes, an alias is a name, and the
/// Demographic tab's Parent/Guardian field is a text box; none of the three
/// has a second field for a screen to show, so none of them needs a shape.
/// The addresses, telephones and emails do, and have one each.
///
/// Collections are <see cref="IReadOnlyList{T}"/>, as
/// <see cref="PagedResult{T}"/>'s items already are: a response is read, not
/// added to.
/// </remarks>
/// <param name="Id">The person's identifier, as every other resource refers to them.</param>
/// <param name="LastName">The person's last name, which every person has.</param>
/// <param name="FirstName">The person's first name, where it is known.</param>
/// <param name="MiddleName">The person's middle name, where it is known.</param>
/// <param name="BirthDate">The person's date of birth, where it is known.</param>
/// <param name="DaysOldNoBirthday">
/// The person's age in days, for a person whose age is known but whose
/// birthday is not; null for everyone else. The ages the screen shows —
/// current age, age at onset, age at event date — are computed from this and
/// <paramref name="BirthDate"/>, and none of them is stored.
/// </param>
/// <param name="BirthGender">The sex recorded at birth, as the legacy gender code.</param>
/// <param name="CurrentGender">The person's current gender, as the legacy gender code.</param>
/// <param name="Ethnicity">The person's ethnicity, as the legacy ethnicity code.</param>
/// <param name="Language">The person's primary language, as the legacy language code.</param>
/// <param name="CountryOfBirth">The country the person was born in, as the legacy country code.</param>
/// <param name="Races">Every race the person reported, as the legacy race codes.</param>
/// <param name="Aliases">Every other name the person is known by.</param>
/// <param name="ParentGuardians">
/// The person's parents or guardians, by name — the names the Demographic
/// tab's Parent/Guardian field holds. A guardian recorded only as a link to
/// another person in the system, with no name typed against them, has no name
/// to show and is not in this list.
/// </param>
/// <param name="Addresses">Every address the person has had, the current one included.</param>
/// <param name="Telephones">Every telephone number recorded for the person.</param>
/// <param name="Emails">Every email address recorded for the person.</param>
public record PersonDetailResponse(
    int Id,
    string LastName,
    string? FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    int? DaysOldNoBirthday,
    string? BirthGender,
    string? CurrentGender,
    string? Ethnicity,
    string? Language,
    string? CountryOfBirth,
    IReadOnlyList<string> Races,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> ParentGuardians,
    IReadOnlyList<PersonAddressResponse> Addresses,
    IReadOnlyList<PersonTelephoneResponse> Telephones,
    IReadOnlyList<PersonEmailResponse> Emails)
{
    /// <summary>Every identifier other systems know the person by.</summary>
    /// <remarks>
    /// This and <see cref="FormerNames"/> are init-only with empty defaults
    /// rather than positional, so a response built without them is a person
    /// with none.
    /// </remarks>
    public IReadOnlyList<PersonIdentifierResponse> Identifiers { get; init; } = [];

    /// <summary>Every name the person was on file under before, the latest change first.</summary>
    public IReadOnlyList<PersonFormerNameResponse> FormerNames { get; init; } = [];
}
