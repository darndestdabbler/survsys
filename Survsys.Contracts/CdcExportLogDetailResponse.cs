namespace Survsys.Contracts;

/// <summary>
/// One CDC export whole: the log summary together with the message that was
/// generated for it — the shape <c>GET /api/export-log/{id}</c> returns.
/// </summary>
/// <remarks>
/// The summary is a <see cref="CdcExportLogItemResponse"/>, the same shape the
/// log list returns, so one export reads the same whether it is read in the
/// list or on its own. The generated message is what the list has no use for
/// and a reviewer of one export does: the ER7 or XML that was or would be sent,
/// or null where none has been generated yet.
/// </remarks>
/// <param name="Entry">The export's log summary.</param>
/// <param name="GeneratedMessage">
/// The message generated for the export, or null where none has been generated
/// yet.
/// </param>
public record CdcExportLogDetailResponse(
    CdcExportLogItemResponse Entry,
    string? GeneratedMessage);
