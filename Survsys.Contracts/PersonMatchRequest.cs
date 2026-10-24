namespace Survsys.Contracts;

/// <summary>
/// The person a match is sought for: what <c>POST /api/persons/match</c>
/// scores the people on file against.
/// </summary>
/// <remarks>
/// Candidates are the people sharing the last name, the date of birth or an
/// identifier value, or — where <see cref="PersonIds"/> is given — exactly
/// those people, which is how a search screen scores the rows it already
/// shows. So a request needs a last name, a date of birth, an identifier or
/// person ids to find anyone by.
/// </remarks>
/// <param name="LastName">The last name.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="BirthDate">The date of birth.</param>
/// <param name="BirthGender">The legacy code for the sex recorded at birth.</param>
/// <param name="Identifiers">Identifier values, medical record numbers among them.</param>
/// <param name="Street">The street of the person's address.</param>
/// <param name="PostalCode">The zip of the person's address.</param>
/// <param name="PersonIds">The people to score, in place of a candidate search.</param>
public record PersonMatchRequest(
    string? LastName,
    string? FirstName = null,
    DateOnly? BirthDate = null,
    string? BirthGender = null,
    IReadOnlyList<string>? Identifiers = null,
    string? Street = null,
    string? PostalCode = null,
    IReadOnlyList<int>? PersonIds = null);
