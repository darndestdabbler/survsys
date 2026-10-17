using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/facilities</c>.
/// </summary>
/// <remarks>
/// The only thing in this tier that speaks HTTP about facilities, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class FacilitiesClient(HttpClient http)
{
    /// <summary>How a failure to load the list starts.</summary>
    public const string PageFailurePrefix = "The facilities could not be loaded";

    /// <summary>How a failure to load one facility starts.</summary>
    public const string FindFailurePrefix = "The facility could not be loaded";

    /// <summary>How a failure to create or save a facility starts.</summary>
    public const string SaveFailurePrefix = "The facility could not be saved";

    private static readonly ApiAsk Paging = new(PageFailurePrefix, "a page of facilities");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the facility's record");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the facility as saved");

    /// <summary>One page of facilities, or why there is none.</summary>
    public Task<Asked<PagedResult<FacilityResponse>>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Paging.Get<PagedResult<FacilityResponse>>(
            http, $"api/facilities?page={page}&pageSize={pageSize}", cancellationToken);

    /// <summary>One facility, or why there is none.</summary>
    public Task<Asked<FacilityResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<FacilityResponse>(http, $"api/facilities/{id}", cancellationToken);

    /// <summary>Creates a facility, answering with it as created or why it was not.</summary>
    public Task<Asked<FacilityResponse>> Create(
        SaveFacilityRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveFacilityRequest, FacilityResponse>(http, "api/facilities", request, cancellationToken);

    /// <summary>Saves over a facility, answering with it as saved or why it was not.</summary>
    public Task<Asked<FacilityResponse>> Update(
        int id,
        SaveFacilityRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveFacilityRequest, FacilityResponse>(http, $"api/facilities/{id}", request, cancellationToken);
}
