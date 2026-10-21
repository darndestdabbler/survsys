using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the CDC export log:
/// <c>GET /api/export-log</c>, paged and newest first, and
/// <c>GET /api/export-log/{id}</c>, one export whole with the message that was
/// generated for it.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about the export log, and
/// the failure handling is <see cref="ApiAsk"/>'s, as every client's is. Two
/// asks, two prefixes: a reviewer whose log could not be loaded and one whose
/// export could not be read are being told two different things. The rows come
/// back in <see cref="PagedResult{T}"/>, the one collection shape this solution
/// puts on the wire; one export comes back whole with the generated message the
/// list leaves out.
/// </remarks>
public sealed class ExportLogClient(HttpClient http)
{
    /// <summary>
    /// How a failure to load the export log starts. Public because the screen
    /// and the unit suite both read it rather than repeating the wording.
    /// </summary>
    public const string LogFailurePrefix = "The export log could not be loaded";

    /// <summary>How a failure to load one export starts.</summary>
    public const string ExportFailurePrefix = "The export could not be loaded";

    private static readonly ApiAsk Listing = new(LogFailurePrefix, "a page of the export log");

    /// <summary>How a failure to resend an export starts.</summary>
    public const string ResendFailurePrefix = "The export could not be resent";

    /// <summary>How a failure to retract a sent notification starts.</summary>
    public const string RetractFailurePrefix = "The notification could not be retracted";

    private static readonly ApiAsk Reading = new(ExportFailurePrefix, "the export");

    private static readonly ApiAsk Resending = new(ResendFailurePrefix, "the queued case");

    private static readonly ApiAsk Retracting = new(RetractFailurePrefix, "the queued retraction");

    /// <summary>
    /// One page of the export log, newest first, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<CdcExportLogItemResponse>>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<CdcExportLogItemResponse>>(
            http,
            $"api/export-log?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// One export whole — its log summary and the message generated for it — or
    /// why there is none.
    /// </summary>
    public Task<Asked<CdcExportLogDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Reading.Get<CdcExportLogDetailResponse>(http, $"api/export-log/{id}", cancellationToken);

    /// <summary>
    /// Queues the case of an export for notification again, answering with the
    /// queued entry or with why it was not queued.
    /// </summary>
    public Task<Asked<NotificationQueueItemResponse>> Resend(
        int id,
        CancellationToken cancellationToken = default) =>
        Resending.Post<object?, NotificationQueueItemResponse>(
            http, $"api/export-log/{id}/resend", body: null, cancellationToken);

    /// <summary>
    /// Queues a retraction of the case's accepted notification, sent with
    /// NOT118 = X, answering with the queued entry or with why it was not —
    /// a case CDC never accepted is refused 409.
    /// </summary>
    public Task<Asked<NotificationQueueItemResponse>> Retract(
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        Retracting.Post<object?, NotificationQueueItemResponse>(
            http, $"api/notification-queue/{personConditionId}/retract", body: null, cancellationToken);
}
