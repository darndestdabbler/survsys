namespace Survsys.Contracts;

/// <summary>
/// One export of a case to CDC, as the API tier returns it in the export-log
/// list.
/// </summary>
/// <remarks>
/// The spine of a log row a reader scans the history by: its own identifier,
/// the record number the export was made under, the notification guide it was
/// generated under, when it was queued and when it was sent, and the outcome.
/// The generated message a row carries is left out of the list, because it is
/// unbounded and a log shows many rows; it is
/// <see cref="CdcExportLogDetailResponse"/>'s, read one export at a time.
///
/// The record number is the one kept on the log, which stood when the export
/// was made and does not follow a case whose number later changes. The
/// outcome is the stored code, not a resolved name, as the other slices return
/// a code and resolve it on the screen.
/// </remarks>
/// <param name="Id">The export's identifier, as the detail endpoint refers to it.</param>
/// <param name="RecordNumber">The case's record number as it stood when the export was made.</param>
/// <param name="GuideId">
/// The notification guide the export was generated under, or null where none
/// was recorded.
/// </param>
/// <param name="QueuedAt">When the case was queued for this export, in UTC.</param>
/// <param name="SentAt">
/// When the export was sent to CDC, in UTC, or null while it is still queued
/// and unsent.
/// </param>
/// <param name="Outcome">
/// The outcome of the export as a short code, or null while it is unsent.
/// </param>
public record CdcExportLogItemResponse(
    int Id,
    string RecordNumber,
    int? GuideId,
    DateTime QueuedAt,
    DateTime? SentAt,
    string? Outcome);
