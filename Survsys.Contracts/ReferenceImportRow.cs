namespace Survsys.Contracts;

/// <summary>
/// One row of a reference CSV import that adds an entry or changes one: part
/// of <see cref="ReferenceImportResponse"/>.
/// </summary>
/// <param name="Line">The one-based line of the CSV the row was read from, the header being line 1.</param>
/// <param name="Key">The row's natural key as the table matches it — a code, a town, a zip code.</param>
public record ReferenceImportRow(int Line, string Key);
