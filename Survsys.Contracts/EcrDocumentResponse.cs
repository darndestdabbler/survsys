namespace Survsys.Contracts;

/// <summary>
/// One eCR document, an eICR or an RR, as the API tier returns it in the eCR
/// queue.
/// </summary>
/// <remarks>
/// The raw XML is left out, because it is large and the queue shows many rows.
/// The detail renders it instead. The finding counts let a reviewer see which
/// documents failed validation without opening each one.
/// </remarks>
/// <param name="Id">The document's identifier, as the detail endpoint refers to it.</param>
/// <param name="Kind">"eICR" or "RR".</param>
/// <param name="Feed">"test" or "production": the feed the document arrived on.</param>
/// <param name="DocumentId">The document's own id (ClinicalDocument/id).</param>
/// <param name="VersionNumber">The document's version within its set, or null where it has none.</param>
/// <param name="Source">The name of the file the document was read from, or null.</param>
/// <param name="ReceivedAt">When the document was taken in, in UTC.</param>
/// <param name="ErrorCount">How many fatal and error findings validation made on it.</param>
/// <param name="WarningCount">How many warning findings validation made on it.</param>
public record EcrDocumentResponse(
    int Id,
    string Kind,
    string Feed,
    string DocumentId,
    int? VersionNumber,
    string? Source,
    DateTime ReceivedAt,
    int ErrorCount,
    int WarningCount);
