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

    /// <summary>How a failure to save a correction to an exposure row starts.</summary>
    public const string SaveFailurePrefix = "The exposure record could not be saved";

    /// <summary>How a failure to remove an exposure row starts.</summary>
    public const string RemoveFailurePrefix = "The exposure record could not be removed";

    /// <summary>
    /// The page size <see cref="All"/> walks a report's exposures at: the
    /// largest the API tier allows, so a report's exposures arrive in as few
    /// asks as they can.
    /// </summary>
    public const int FullPage = 200;

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of exposures");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "an exposure");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the exposure recorded");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the exposure record as saved");

    private static readonly ApiAsk Removing = new(RemoveFailurePrefix, "the exposure record removed");

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
    /// Every exposure event of a report, read page by page until the total
    /// the API states has arrived, or why there is none.
    /// </summary>
    /// <remarks>
    /// A report's exposures are listed whole on the Contacts tab, so the tab
    /// asks for all of them rather than for a page: this walks
    /// <see cref="Page"/> at the largest page size the API allows and stops
    /// when as many have arrived as the API said there were, or when a page
    /// comes back empty. A page that fails is the failure of the whole, in
    /// the list's own words, because a list with a hole in it is not the
    /// list. Here rather than in the tab so the stub transport can prove the
    /// second page is asked for.
    /// </remarks>
    public async Task<Asked<IReadOnlyList<ExposureResponse>>> All(
        int reportId,
        CancellationToken cancellationToken = default)
    {
        var all = new List<ExposureResponse>();

        for (var page = 1; ; page++)
        {
            var asked = await Page(reportId, page, FullPage, cancellationToken);

            if (asked.Value is not { } listed)
            {
                return Asked<IReadOnlyList<ExposureResponse>>.Failed(
                    asked.Failure ?? $"{ListFailurePrefix}: page {page} could not be read.");
            }

            all.AddRange(listed.Items);

            if (listed.Items.Count == 0 || all.Count >= listed.TotalCount)
            {
                return Asked<IReadOnlyList<ExposureResponse>>.Of(all);
            }
        }
    }

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

    /// <summary>
    /// Corrects an exposure's own columns and answers with it as stored, or
    /// with why nothing was saved.
    /// </summary>
    public Task<Asked<ExposureResponse>> UpdateExposure(
        int exposureId,
        UpdateExposureRequest exposure,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateExposureRequest, ExposureResponse>(
            http, $"api/exposures/{exposureId}", exposure, cancellationToken);

    /// <summary>
    /// Corrects an exposure place and answers with it as stored, or with why
    /// nothing was saved.
    /// </summary>
    public Task<Asked<ExposurePlaceResponse>> UpdatePlace(
        int placeId,
        ExposurePlaceRequest place,
        CancellationToken cancellationToken = default) =>
        Saving.Put<ExposurePlaceRequest, ExposurePlaceResponse>(
            http, $"api/exposure-places/{placeId}", place, cancellationToken);

    /// <summary>
    /// Corrects a contact — its disposition and the date it was dispositioned
    /// among its columns — and answers with it as stored, or with why nothing
    /// was saved.
    /// </summary>
    public Task<Asked<ExposureContactResponse>> UpdateContact(
        int contactId,
        ExposureContactRequest contact,
        CancellationToken cancellationToken = default) =>
        Saving.Put<ExposureContactRequest, ExposureContactResponse>(
            http, $"api/exposure-contacts/{contactId}", contact, cancellationToken);

    /// <summary>
    /// Corrects a source and answers with it as stored, or with why nothing
    /// was saved.
    /// </summary>
    public Task<Asked<ExposureSourceResponse>> UpdateSource(
        int sourceId,
        ExposureSourceRequest source,
        CancellationToken cancellationToken = default) =>
        Saving.Put<ExposureSourceRequest, ExposureSourceResponse>(
            http, $"api/exposure-sources/{sourceId}", source, cancellationToken);

    /// <summary>
    /// Removes an exposure, or answers with why it was not — an exposure that
    /// still has contacts or sources is refused, the 409 the API answers
    /// reaching the screen as the other failures do.
    /// </summary>
    public Task<string?> DeleteExposure(int exposureId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/exposures/{exposureId}", cancellationToken);

    /// <summary>
    /// Removes an exposure place, or answers with why it was not — a place an
    /// exposure still points at is refused.
    /// </summary>
    public Task<string?> DeletePlace(int placeId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/exposure-places/{placeId}", cancellationToken);

    /// <summary>Removes a contact, or answers with why it was not.</summary>
    public Task<string?> DeleteContact(int contactId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/exposure-contacts/{contactId}", cancellationToken);

    /// <summary>Removes a source, or answers with why it was not.</summary>
    public Task<string?> DeleteSource(int sourceId, CancellationToken cancellationToken = default) =>
        Removing.Delete(http, $"api/exposure-sources/{sourceId}", cancellationToken);
}
