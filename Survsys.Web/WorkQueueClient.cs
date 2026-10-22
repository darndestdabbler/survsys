using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the work queue and sharing:
/// <c>GET /api/work-queue</c>, and the
/// <c>/api/person-conditions/{id}/shares</c> pair that shares a report
/// read-only with another agency and lists who it is shared with.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about the work queue and
/// shares, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is. Three asks, three prefixes: a user whose queue could not be
/// loaded, one whose report could not be shared, and one whose shares could not
/// be read are being told three different things.
///
/// The queue is an agency's, so <see cref="Queue"/> carries the agency and the
/// page in the query string, as <c>ConditionsClient</c> carries its page; the
/// reports come back in <see cref="PagedResult{T}"/>, the one collection shape
/// this solution puts on the wire. A share comes back named by the agency it
/// shares with, which is how <see cref="Shares"/> lists them too.
/// </remarks>
public sealed class WorkQueueClient(HttpClient http)
{
    /// <summary>How a failure to load an agency's work queue starts.</summary>
    public const string QueueFailurePrefix = "The work queue could not be loaded";

    /// <summary>How a failure to share a report starts.</summary>
    public const string ShareFailurePrefix = "The case morbidity report could not be shared";

    /// <summary>How a failure to load a report's sharing starts.</summary>
    public const string SharesFailurePrefix = "The case morbidity report's sharing could not be loaded";

    private static readonly ApiAsk Queuing = new(QueueFailurePrefix, "a page of the work queue");

    private static readonly ApiAsk Sharing = new(ShareFailurePrefix, "the agency the report was shared with");

    private static readonly ApiAsk Listing = new(SharesFailurePrefix, "the agencies the report is shared with");

    /// <summary>
    /// One page of an agency's work queue — the reports it investigates and the
    /// reports shared read-only to it — or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<WorkQueueItemResponse>>> Queue(
        int agencyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Queuing.Get<PagedResult<WorkQueueItemResponse>>(
            http,
            $"api/work-queue?agencyId={agencyId}&page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// Shares a report read-only with an agency and answers with the agency it
    /// was shared with, or with why nothing was shared.
    /// </summary>
    public Task<Asked<AgencyResponse>> Share(
        int id,
        ShareReportRequest share,
        CancellationToken cancellationToken = default) =>
        Sharing.Post<ShareReportRequest, AgencyResponse>(
            http, $"api/person-conditions/{id}/shares", share, cancellationToken);

    /// <summary>
    /// The agencies a report is shared read-only with, in name order, or why
    /// there is no list.
    /// </summary>
    public Task<Asked<PagedResult<AgencyResponse>>> Shares(
        int id,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<AgencyResponse>>(
            http, $"api/person-conditions/{id}/shares", cancellationToken);
}
