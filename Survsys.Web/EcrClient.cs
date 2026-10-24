using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of the eCR documents taken in:
/// <c>GET /api/ecr-documents</c>, the queue paged and filterable by feed, and
/// <c>GET /api/ecr-documents/{id}</c>, one document with its pair, the eICR
/// rendered beside the RR's conditions and the validation findings; and
/// <c>GET /api/ecr-quality</c>, the data quality figures by sender and condition.
/// Beside the reads, the settling of an eICR held for review:
/// <c>GET /api/ecr-documents/{id}/person-matches</c>, the people its patient
/// may be, and <c>POST /api/ecr-documents/{id}/case</c>, which writes it to
/// the cases of the person a reviewer chose or of a new person.
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

    /// <summary>How a failure to load the eCR data quality figures starts.</summary>
    public const string QualityFailurePrefix = "The eCR data quality figures could not be loaded";

    private static readonly ApiAsk Reading = new(DocumentFailurePrefix, "the eCR document");

    private static readonly ApiAsk Measuring = new(QualityFailurePrefix, "the eCR data quality figures");

    /// <summary>How a failure to load a held eICR's person matches starts.</summary>
    public const string MatchFailurePrefix = "The eICR's person matches could not be loaded";

    private static readonly ApiAsk Matching = new(MatchFailurePrefix, "the eICR's person matches");

    /// <summary>How a failure to settle a held eICR starts.</summary>
    public const string SettleFailurePrefix = "The eICR could not be written to a case";

    private static readonly ApiAsk Settling = new(SettleFailurePrefix, "the eCR document as it was settled");

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

    /// <summary>
    /// The eCR data quality figures by sender and condition over the documents
    /// received from <paramref name="from"/> through <paramref name="to"/>,
    /// on one feed where one is given, or why there are none.
    /// </summary>
    public Task<Asked<IReadOnlyList<EcrQualityRowResponse>>> Quality(
        string? feed,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/ecr-quality?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}";

        if (!string.IsNullOrWhiteSpace(feed))
        {
            url += $"&feed={Uri.EscapeDataString(feed.Trim())}";
        }

        return Measuring.Get<IReadOnlyList<EcrQualityRowResponse>>(http, url, cancellationToken);
    }

    /// <summary>One eCR document with its pair, or why it could not be read.</summary>
    public Task<Asked<EcrDocumentDetailResponse>> Get(
        int id,
        CancellationToken cancellationToken = default) =>
        Reading.Get<EcrDocumentDetailResponse>(http, $"api/ecr-documents/{id}", cancellationToken);

    /// <summary>
    /// A held eICR's patient and the people on file who may be them, best
    /// first, or why they could not be read.
    /// </summary>
    public Task<Asked<StagedMessagePersonMatchResponse>> PersonMatches(
        int id,
        CancellationToken cancellationToken = default) =>
        Matching.Get<StagedMessagePersonMatchResponse>(
            http, $"api/ecr-documents/{id}/person-matches", cancellationToken);

    /// <summary>
    /// Settles a held eICR: writes it to the cases of the person on file
    /// <paramref name="personId"/> names, or of a new person made from its
    /// patient where it is null, and answers with the document as it now
    /// stands, or with why nothing was written.
    /// </summary>
    public Task<Asked<EcrDocumentDetailResponse>> Settle(
        int id,
        int? personId,
        CancellationToken cancellationToken = default) =>
        Settling.Post<LinkEcrCaseRequest, EcrDocumentDetailResponse>(
            http, $"api/ecr-documents/{id}/case", new LinkEcrCaseRequest(personId), cancellationToken);
}
