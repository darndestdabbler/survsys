namespace Survsys.Contracts;

/// <summary>
/// A person, as a list of people shows them.
/// </summary>
/// <remarks>
/// The seven fields a list needs to tell one person from another: who they
/// are, when they were born and their gender. Everything else a person
/// carries — their ethnicity, their language, where they were born, their
/// addresses, telephones, emails, races, aliases and guardians — is
/// <see cref="PersonDetailResponse"/>'s, because a list that read all of it
/// would read it for every row on the page.
///
/// The mapping that chooses these fields lives in <c>Survsys.Api</c>, the tier
/// that owns the wire, because <c>Survsys.Data</c> may not reference this
/// project.
/// </remarks>
/// <param name="Id">The person's identifier, as every other resource refers to them.</param>
/// <param name="LastName">The person's last name, which every person has.</param>
/// <param name="FirstName">The person's first name, where it is known.</param>
/// <param name="MiddleName">The person's middle name, where it is known.</param>
/// <param name="BirthDate">The person's date of birth, where it is known.</param>
/// <param name="BirthGender">The sex recorded at birth, as the legacy gender code.</param>
/// <param name="CurrentGender">The person's current gender, as the legacy gender code.</param>
public record PersonResponse(
    int Id,
    string LastName,
    string? FirstName,
    string? MiddleName,
    DateOnly? BirthDate,
    string? BirthGender,
    string? CurrentGender);
