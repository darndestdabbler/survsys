namespace Survsys.Contracts;

/// <summary>
/// A batch of contacts to import as CSV: what
/// <c>POST /api/person-conditions/{id}/contacts/import</c> takes.
/// </summary>
/// <remarks>
/// The legacy Contacts tab's "Import" button, as
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// lists it, takes a file of contacts and writes it to the
/// <c>contact_import_batch</c> and <c>contact_import_row</c> tables declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>. Here the file's text is posted whole and parsed, and
/// every row accepted is saved as a new person and a contact event under the
/// exposure the batch names; the answer says which rows were saved and which
/// were refused. No batch history is kept.
///
/// The CSV is a header row naming the columns, then one contact per row, with
/// the columns named after the legacy <c>contact_import_row</c> fields. The
/// parser says which names it reads.
/// </remarks>
/// <param name="Csv">The CSV text, header row first.</param>
/// <param name="ExposureId">The exposure of the report the contacts were at.</param>
public record ImportContactsRequest(string? Csv = null, int? ExposureId = null);
