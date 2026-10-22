namespace Survsys.Contracts;

/// <summary>
/// One row a batch import rejects, and why: part of what
/// <c>POST /api/person-conditions/{id}/contacts/import</c> answers with,
/// inside <see cref="ContactImportResponse"/>.
/// </summary>
/// <remarks>
/// The legacy import records a status per <c>contact_import_row</c>, as
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c> declares it; here nothing is recorded, so the row is
/// named by the line it was read from and the reason is spelled out for the
/// person who will fix the file. The reason names what was wrong — a column
/// count that does not match the header, a required field missing or
/// unparseable, or a contact already in the batch — so the file can be
/// corrected and posted again.
/// </remarks>
/// <param name="Line">The one-based line of the CSV the row was read from.</param>
/// <param name="Reason">Why the row was rejected, naming what was wrong.</param>
public record ContactImportProblem(int Line, string Reason);
