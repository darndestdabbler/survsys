namespace Survsys.Contracts;

/// <summary>
/// A person to put on file: what <c>POST /api/persons</c> takes.
/// </summary>
/// <remarks>
/// The five fields the legacy person search form carries into a new person,
/// which is where a person is created from — the search that found nobody, and
/// its "Create Person and CMR" button. A person created this way is a person
/// nobody had on file a moment ago, and these are the only things known about
/// them.
///
/// The rest of the Demographic tab — the ethnicity, the language, the country
/// of birth, the addresses, telephones, emails, races, aliases and the
/// parent or guardian — is not here, and a person created by this request has
/// none of it. That is the correct state for a person at the moment they are
/// created, and filling it in is the Demographics screen's, which is a later
/// session's.
///
/// <see cref="LastName"/> is nullable here although the schema requires it,
/// and deliberately: a request that left it out is a request this tier
/// answers with a 400 naming the field, which is better than the bodiless
/// failure a required property's deserialization would produce.
/// </remarks>
/// <param name="LastName">
/// The person's last name: the one name part the schema requires, so a
/// request without one is refused.
/// </param>
/// <param name="FirstName">The person's first name, where it is known.</param>
/// <param name="MiddleName">The person's middle name, where it is known.</param>
/// <param name="BirthDate">The person's date of birth, where it is known.</param>
/// <param name="BirthGender">
/// The sex recorded at birth, as the legacy gender code.
/// </param>
public record CreatePersonRequest(
    string? LastName,
    string? FirstName = null,
    string? MiddleName = null,
    DateOnly? BirthDate = null,
    string? BirthGender = null);
