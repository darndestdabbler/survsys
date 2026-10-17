using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's laboratory
/// specimens: <c>GET</c> and <c>POST /api/person-conditions/{id}/labs</c>, and
/// <c>GET /api/labs/{id}</c> for one specimen whole.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about laboratory
/// specimens, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is.
///
/// Three asks and three prefixes, because a user whose list of specimens could
/// not be loaded, one whose single specimen could not be loaded and one whose
/// new specimen was refused are three different sentences to read. The create
/// endpoint answers 201 with the specimen whole, so <see cref="Record"/> hands
/// back the same <see cref="LabDetailResponse"/> <see cref="Detail"/> does.
/// </remarks>
public sealed class LabsClient(HttpClient http)
{
    /// <summary>How a failure to list a report's specimens starts.</summary>
    public const string ListFailurePrefix = "The laboratory specimens could not be loaded";

    /// <summary>How a failure to read one specimen starts.</summary>
    public const string FindFailurePrefix = "The laboratory specimen could not be loaded";

    /// <summary>How a failure to record a new specimen starts.</summary>
    public const string RecordFailurePrefix = "The laboratory specimen could not be recorded";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of laboratory specimens");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "a laboratory specimen");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the specimen recorded");

    /// <summary>
    /// One page of a report's laboratory specimens, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<LabResponse>>> Page(
        int reportId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<LabResponse>>(
            http,
            $"api/person-conditions/{reportId}/labs?page={page}&pageSize={pageSize}",
            cancellationToken);

    /// <summary>
    /// One laboratory specimen whole — its tests with their results and its
    /// susceptibility rows — or why there is none.
    /// </summary>
    public Task<Asked<LabDetailResponse>> Detail(
        int labId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<LabDetailResponse>(http, $"api/labs/{labId}", cancellationToken);

    /// <summary>
    /// Records a laboratory specimen against a report and answers with it
    /// whole, or with why none was recorded.
    /// </summary>
    public Task<Asked<LabDetailResponse>> Record(
        int reportId,
        CreateLabRequest specimen,
        CancellationToken cancellationToken = default) =>
        Recording.Post<CreateLabRequest, LabDetailResponse>(
            http, $"api/person-conditions/{reportId}/labs", specimen, cancellationToken);
}
