using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's exposure events:
/// <c>GET</c> and <c>POST /api/person-conditions/{id}/exposures</c>, and
/// <c>GET /api/exposures/{id}</c> for one exposure whole.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about exposures, and
/// the failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Three asks and three prefixes, as <see cref="LabsClient"/> has, because a
/// user whose list of exposures could not be loaded, one whose single exposure
/// could not be loaded and one whose new exposure was refused are three
/// different sentences to read. The create endpoint answers 201 with the
/// exposure whole, so <see cref="Record"/> hands back the same
/// <see cref="ExposureDetailResponse"/> <see cref="Detail"/> does.
/// </remarks>
public sealed class ExposuresClient(HttpClient http)
{
    /// <summary>How a failure to list a report's exposures starts.</summary>
    public const string ListFailurePrefix = "The exposures could not be loaded";

    /// <summary>How a failure to read one exposure starts.</summary>
    public const string FindFailurePrefix = "The exposure could not be loaded";

    /// <summary>How a failure to record a new exposure starts.</summary>
    public const string RecordFailurePrefix = "The exposure could not be recorded";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of exposures");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "an exposure");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the exposure recorded");

    /// <summary>One page of a report's exposure events, or why there is none.</summary>
    public Task<Asked<PagedResult<ExposureResponse>>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<ExposureResponse>>(
            http,
            $"api/person-conditions/{reportId}/exposures?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// One exposure whole — its place, its contacts and its sources — or why
    /// there is none.
    /// </summary>
    public Task<Asked<ExposureDetailResponse>> Detail(
        int exposureId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<ExposureDetailResponse>(http, $"api/exposures/{exposureId}", cancellationToken);

    /// <summary>
    /// Records an exposure against a report and answers with it whole, or
    /// with why none was recorded.
    /// </summary>
    public Task<Asked<ExposureDetailResponse>> Record(
        int reportId,
        CreateExposureRequest exposure,
        CancellationToken cancellationToken = default) =>
        Recording.Post<CreateExposureRequest, ExposureDetailResponse>(
            http, $"api/person-conditions/{reportId}/exposures", exposure, cancellationToken);
}
