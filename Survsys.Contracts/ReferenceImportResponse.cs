namespace Survsys.Contracts;

/// <summary>
/// What a reference table's CSV import would do, or did: the rows it adds as
/// new entries, the rows it saves over an entry with the same key, and the
/// rows it refuses, each with its line and why.
/// </summary>
/// <remarks>
/// An import is first asked for as a preview, which writes nothing, and then
/// confirmed, which writes the same rows; <see cref="Applied"/> says which this
/// is. A row matching its entry field for field is neither added nor changed,
/// and an entry the file leaves out is kept as it is.
/// </remarks>
/// <param name="Applied">True where the rows were written; false for a preview.</param>
/// <param name="Added">The rows added as new entries.</param>
/// <param name="Changed">The rows saved over an entry with the same key.</param>
/// <param name="Refused">The rows refused, the header being line 1.</param>
public record ReferenceImportResponse(
    bool Applied,
    IReadOnlyList<ReferenceImportRow> Added,
    IReadOnlyList<ReferenceImportRow> Changed,
    IReadOnlyList<ContactImportProblem> Refused);
