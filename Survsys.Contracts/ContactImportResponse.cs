namespace Survsys.Contracts;

/// <summary>
/// What a batch import makes of a CSV of contacts: the contacts it would
/// accept, and the rows it rejects — what
/// <c>POST /api/person-conditions/{id}/contacts/import</c> answers with.
/// </summary>
/// <remarks>
/// The legacy import writes a <c>contact_import_batch</c> and one
/// <c>contact_import_row</c> per line with a status each, as
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> declares them and the Contacts tab's "Import" button
/// in
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// starts it. This is that answer without the writing: the batch is parsed
/// and validated and the result handed back, and no person, no contact and no
/// import row is persisted. A caller reads the two lists together — every
/// line of the file is in one of them or is blank.
///
/// Both collections are <see cref="IReadOnlyList{T}"/>, as a response is read
/// and not added to, and are present but empty where a batch has nothing to
/// accept or nothing to reject.
/// </remarks>
/// <param name="Accepted">The contacts the batch would import, in file order.</param>
/// <param name="Rejected">The rows the batch would not import, in file order, each with its reason.</param>
public record ContactImportResponse(
    IReadOnlyList<ImportedContact> Accepted,
    IReadOnlyList<ContactImportProblem> Rejected);
