using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the outbound CDC notification queue:
/// <c>GET /api/notification-queue</c>, paged and oldest first, and
/// <c>PATCH /api/notification-queue/{personConditionId}/hold</c>, which raises
/// or lowers a queued case's hold.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about the notification
/// queue, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is. Two asks, two prefixes: a reviewer whose queue could not be
/// loaded and one whose hold could not be changed are being told two different
/// things. The rows come back in <see cref="PagedResult{T}"/>, the one
/// collection shape this solution puts on the wire; a hold answers with the one
/// entry in its new state.
/// </remarks>
public sealed class NotificationQueueClient(HttpClient http)
{
    /// <summary>
    /// How a failure to load the notification queue starts. Public because the
    /// screen and the unit suite both read it rather than repeating the
    /// wording.
    /// </summary>
    public const string QueueFailurePrefix = "The notification queue could not be loaded";

    /// <summary>How a failure to raise or lower a case's hold starts.</summary>
    public const string HoldFailurePrefix = "The case's hold could not be changed";

    /// <summary>How a failure to queue a case for notification by hand starts.</summary>
    public const string QueueingFailurePrefix = "The case could not be queued for notification";

    private static readonly ApiAsk Listing = new(QueueFailurePrefix, "a page of the notification queue");

    private static readonly ApiAsk Holding = new(HoldFailurePrefix, "the queued case in its new state");

    private static readonly ApiAsk Queueing = new(QueueingFailurePrefix, "the queued case");

    /// <summary>
    /// One page of the notification queue, oldest first, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<NotificationQueueItemResponse>>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<NotificationQueueItemResponse>>(
            http,
            $"api/notification-queue?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// Raises or lowers a queued case's hold, answering with the entry in its
    /// new state or with why it did not change.
    /// </summary>
    /// <remarks>
    /// The case is named in the URL and the hold is the body, as the API tier
    /// sets the hold on a case in place. A case the API tier has no entry for
    /// comes back as this client's hold failure, the status named and never the
    /// body, as every other ask does.
    /// </remarks>
    public Task<Asked<NotificationQueueItemResponse>> SetHold(
        int personConditionId,
        bool hold,
        CancellationToken cancellationToken = default) =>
        Holding.Patch<SetNotificationHoldRequest, NotificationQueueItemResponse>(
            http,
            $"api/notification-queue/{personConditionId}/hold",
            new SetNotificationHoldRequest(hold),
            cancellationToken);

    /// <summary>
    /// Queues a case for CDC notification by hand, answering with the queued
    /// entry or with why it was not queued.
    /// </summary>
    /// <remarks>
    /// The case is named in the URL and there is nothing to send with it: the
    /// action is the post, as the API tier queues a case in place. A case the
    /// API tier has none of comes back as this client's queueing failure, the
    /// status named and never the body, as every other ask does.
    /// </remarks>
    public Task<Asked<NotificationQueueItemResponse>> Queue(
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        Queueing.Post<object?, NotificationQueueItemResponse>(
            http,
            $"api/person-conditions/{personConditionId}/notification-queue",
            body: null,
            cancellationToken);
}
