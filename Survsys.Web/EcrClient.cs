using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the eCR documents taken in:
/// <c>GET /api/ecr-documents</c>, the queue paged and filterable by feed, and
/// <c>GET /api/ecr-documents/{id}</c>, one document with its pair, the eICR
/// rendered beside the RR's conditions and the validation findings.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is. The
/// feed is left out of the URL when none is chosen, which the endpoint reads
/// as every feed.
/// </remarks>
public sealed class EcrClient(HttpClient http)
{
    /// <summary>How a failure to load the eCR queue starts.</summary>
    public const string QueueFailurePrefix = "The eCR queue could not be loaded";

    /// <summary>How a failure to load one eCR document starts.</summary>
    public const string DocumentFailurePrefix = "The eCR document could not be loaded";

    private static readonly ApiAsk Queuing = new(QueueFailurePrefix, "a page of the eCR queue");

    private static readonly ApiAsk Reading = new(DocumentFailurePrefix, "the eCR document");

    /// <summary>
    /// One page of the eCR queue, narrowed to one feed where one is given and
    /// to the RRs with a condition to review where <paramref name="review"/>
    /// is true, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<EcrDocumentResponse>>> List(
        string? feed,
        bool review,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/ecr-documents?page={page}&pageSize={pageSize}";

        if (!string.IsNullOrWhiteSpace(feed))
        {
            url += $"&feed={Uri.EscapeDataString(feed.Trim())}";
        }

        if (review)
        {
            url += "&review=true";
        }

        return Queuing.Get<PagedResult<EcrDocumentResponse>>(http, url, cancellationToken);
    }

    /// <summary>One eCR document with its pair, or why it could not be read.</summary>
    public Task<Asked<EcrDocumentDetailResponse>> Get(
        int id,
        CancellationToken cancellationToken = default) =>
        Reading.Get<EcrDocumentDetailResponse>(http, $"api/ecr-documents/{id}", cancellationToken);
}
