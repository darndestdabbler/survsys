namespace Survsys.Contracts;

/// <summary>
/// One contact a batch import accepts, as its row parsed: part of what
/// <c>POST /api/person-conditions/{id}/contacts/import</c> answers with,
/// inside <see cref="ContactImportResponse"/>.
/// </summary>
/// <remarks>
/// The contact fields of the legacy <c>contact_import_row</c> table, as
/// declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the <c>contact_</c> prefix each carries there
/// and less the batch, status and exposure columns a batch history would
/// fill. The last name and the birth date are required, because together
/// with the first and middle names they are what tells one contact in a batch
/// from another; everything else is carried as given, or null where the row
/// left it empty. Each contact the import accepts is saved as a new person and
/// a contact event of theirs, and <see cref="PersonConditionId"/> names that
/// event.
/// </remarks>
/// <param name="Line">The one-based line of the CSV the contact was read from.</param>
/// <param name="FirstName">The contact's first name, or null where the row left it empty.</param>
/// <param name="MiddleName">The contact's middle name, or null.</param>
/// <param name="LastName">The contact's last name.</param>
/// <param name="BirthDate">The contact's date of birth.</param>
/// <param name="Sex">The contact's sex, as a code, or null.</param>
/// <param name="Street">The street line of the contact's address, or null.</param>
/// <param name="Unit">The unit within the street address, or null.</param>
/// <param name="City">The city, or null.</param>
/// <param name="State">The state, as a code, or null.</param>
/// <param name="Zip">The postal code, or null.</param>
/// <param name="County">The county, as a code, or null.</param>
/// <param name="Phone">The contact's phone number, or null.</param>
/// <param name="Email">The contact's email address, or null.</param>
/// <param name="PersonConditionId">The contact event the row was saved as, or null before it is saved.</param>
public record ImportedContact(
    int Line,
    string? FirstName,
    string? MiddleName,
    string LastName,
    DateOnly BirthDate,
    string? Sex,
    string? Street,
    string? Unit,
    string? City,
    string? State,
    string? Zip,
    string? County,
    string? Phone,
    string? Email,
    int? PersonConditionId = null);
