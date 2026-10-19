namespace Survsys.Contracts;

/// <summary>
/// What a batch import makes of a CSV of contacts: the contacts it
/// accepts and saves, and the rows it rejects — what
/// <c>POST /api/person-conditions/{id}/contacts/import</c> answers with.
/// </summary>
/// <remarks>
/// The legacy import writes a <c>contact_import_batch</c> and one
/// <c>contact_import_row</c> per line with a status each, as
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> declares them and the Contacts tab's "Import" button
/// in
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// starts it. Here the batch is parsed and validated, every accepted contact
/// is saved as a new person and a contact event, and the result handed back;
/// no import row is persisted. A caller reads the two lists together — every
/// line of the file is in one of them or is blank.
///
/// Both collections are <see cref="IReadOnlyList{T}"/>, as a response is read
/// and not added to, and are present but empty where a batch has nothing to
/// accept or nothing to reject.
/// </remarks>
/// <param name="Accepted">The contacts the batch imported, in file order.</param>
/// <param name="Rejected">The rows the batch refused, in file order, each with its reason.</param>
public record ContactImportResponse(
    IReadOnlyList<ImportedContact> Accepted,
    IReadOnlyList<ContactImportProblem> Rejected);
