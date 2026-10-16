namespace Survsys.Contracts;

/// <summary>
/// A person's whole demographic record, as the Demographic tab saves it: what
/// <c>PUT /api/persons/{id}</c> takes.
/// </summary>
/// <remarks>
/// Everything <see cref="PersonDetailResponse"/> carries except the
/// identifier, which is in the address of the request, and the identifiers of
/// the child rows, which are the API tier's to assign. The request is the
/// whole record and not a change to one: the person's own columns become what
/// it says, and the stored addresses, telephones, emails, races, aliases and
/// named guardians are replaced by the ones it carries. A row a user took off
/// the screen is simply not in the next request, which is the whole of how a
/// row is removed.
///
/// The six collections are init-only properties with empty defaults rather
/// than constructor parameters, so that a request which says nothing about,
/// say, aliases is a request with no aliases — on the wire and in a test
/// alike — and a screen never sends null for a list it has nothing in. The
/// scalar fields are positional, as <see cref="CreatePersonRequest"/>'s are.
///
/// <see cref="LastName"/> is nullable here although the schema requires it,
/// for the reason <see cref="CreatePersonRequest.LastName"/> is: a request
/// that left it out is answered with a 400 naming the field.
/// </remarks>
/// <param name="LastName">The person's last name: the one field the schema requires.</param>
/// <param name="FirstName">The person's first name, where it is known.</param>
/// <param name="MiddleName">The person's middle name, where it is known.</param>
/// <param name="BirthDate">The person's date of birth, where it is known.</param>
/// <param name="DaysOldNoBirthday">
/// The person's age in days, for a person whose age is known but whose
/// birthday is not; null for everyone else.
/// </param>
/// <param name="BirthGender">The sex recorded at birth, as the legacy gender code.</param>
/// <param name="CurrentGender">The person's current gender, as the legacy gender code.</param>
/// <param name="Ethnicity">The person's ethnicity, as the legacy ethnicity code.</param>
/// <param name="Language">The person's primary language, as the legacy language code.</param>
/// <param name="CountryOfBirth">The country the person was born in, as the legacy country code.</param>
public record UpdatePersonRequest(
    string? LastName,
    string? FirstName = null,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    int? DaysOldNoBirthday = null,
    string? BirthGender = null,
    string? CurrentGender = null,
    string? Ethnicity = null,
    string? Language = null,
    string? CountryOfBirth = null)
{
    /// <summary>Every race the person reported, as the legacy race codes.</summary>
    public IReadOnlyList<string> Races { get; init; } = [];

    /// <summary>Every other name the person is known by.</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];

    /// <summary>
    /// The person's parents or guardians, by name. A guardian stored only as
    /// a link to another person has no name here and is not touched by a
    /// save.
    /// </summary>
    public IReadOnlyList<string> ParentGuardians { get; init; } = [];

    /// <summary>Every address the person has had, the current one included.</summary>
    public IReadOnlyList<PersonAddressRequest> Addresses { get; init; } = [];

    /// <summary>Every telephone number recorded for the person.</summary>
    public IReadOnlyList<PersonTelephoneRequest> Telephones { get; init; } = [];

    /// <summary>Every email address recorded for the person.</summary>
    public IReadOnlyList<PersonEmailRequest> Emails { get; init; } = [];
}
