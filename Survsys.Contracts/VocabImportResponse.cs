namespace Survsys.Contracts;

/// <summary>
/// What a vocabulary CSV import did: how many rows it added, how many it saved
/// over an entry already on file by the table's key, and the rows it refused,
/// each with its line and why.
/// </summary>
/// <param name="Added">The rows added as new entries.</param>
/// <param name="Updated">The rows saved over an entry with the same key.</param>
/// <param name="Refused">The rows refused, the header being line 1.</param>
public record VocabImportResponse(
    int Added,
    int Updated,
    IReadOnlyList<ContactImportProblem> Refused);
