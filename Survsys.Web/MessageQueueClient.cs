using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the inbound ELR intake queue:
/// <c>GET /api/staged-messages</c>, paged and filterable by review state, and
/// <c>GET /api/staged-messages/{id}</c>, one staged message whole with the raw
/// HL7 it arrived as.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about the staged-message
/// queue, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is. Two asks, two prefixes: a reviewer whose queue could not be
/// loaded and one whose message could not be read are being told two different
/// things.
///
/// The queue filters by review state, so <see cref="Page"/> carries the status
/// alongside the page in the query string, as <c>ConditionsClient</c> carries
/// its page — and leaves the status out of the URL when none is chosen, which
/// the endpoint reads as the whole queue rather than an empty one. The rows
/// come back in <see cref="PagedResult{T}"/>, the one collection shape this
/// solution puts on the wire.
///
/// Read-only: this slice shows the queue and a message on its own. The promote
/// action a reviewer takes from the review screen is a later step's, and it is
/// added to this client there rather than reaching for a second one.
/// </remarks>
public sealed class MessageQueueClient(HttpClient http)
{
    /// <summary>
    /// How a failure to load the staged-message queue starts. Public because
    /// the screen and the unit suite both read it rather than repeating the
    /// wording.
    /// </summary>
    public const string QueueFailurePrefix = "The staged-message queue could not be loaded";

    /// <summary>How a failure to load one staged message starts.</summary>
    public const string MessageFailurePrefix = "The staged message could not be loaded";

    /// <summary>How a failure to promote a message to a case starts.</summary>
    public const string PromoteFailurePrefix = "The message could not be promoted to a case";

    private static readonly ApiAsk Queuing = new(QueueFailurePrefix, "a page of the staged-message queue");

    private static readonly ApiAsk Reading = new(MessageFailurePrefix, "the staged message");

    private static readonly ApiAsk Promoting = new(PromoteFailurePrefix, "the case the message was promoted to");

    /// <summary>
    /// One page of the intake queue, oldest first, narrowed to one review state
    /// when a status code is given and the whole queue when it is not, or why
    /// there is none.
    /// </summary>
    /// <remarks>
    /// The status is carried in the query string only when it is present: a
    /// blank filter is the whole queue, so the URL leaves the parameter out
    /// rather than sending an empty one for the endpoint to interpret.
    /// </remarks>
    public Task<Asked<PagedResult<StagedMessageResponse>>> Page(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(status)
            ? $"api/staged-messages?page={page}&pageSize={pageSize}"
            : $"api/staged-messages?status={Uri.EscapeDataString(status.Trim())}&page={page}&pageSize={pageSize}";

        return Queuing.Get<PagedResult<StagedMessageResponse>>(http, url, cancellationToken);
    }

    /// <summary>
    /// One staged message whole — its queue summary and the raw HL7 it arrived
    /// as — or why there is none.
    /// </summary>
    public Task<Asked<StagedMessageDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Reading.Get<StagedMessageDetailResponse>(http, $"api/staged-messages/{id}", cancellationToken);

    /// <summary>
    /// Promotes a reviewed message to a case, answering with the report it
    /// opened and the batch notification the promotion raised, or with why
    /// nothing was promoted.
    /// </summary>
    /// <remarks>
    /// The reviewer's confirmed decision is the body; the message is named in
    /// the URL, as the API tier promotes a message in place rather than taking
    /// its id in the body. A message the API tier would not promote — one not
    /// there, or one whose state refuses it — comes back as this client's
    /// promote failure, the status named and never the body, as every other
    /// ask does.
    /// </remarks>
    public Task<Asked<PromoteMessageResponse>> Promote(
        int id,
        PromoteMessageRequest request,
        CancellationToken cancellationToken = default) =>
        Promoting.Post<PromoteMessageRequest, PromoteMessageResponse>(
            http, $"api/staged-messages/{id}/case", request, cancellationToken);
}
