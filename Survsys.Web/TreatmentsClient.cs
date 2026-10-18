using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/treatments</c>: the
/// treatment catalogue and the conditions each treatment is listed for.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about treatments, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is. It reads
/// the conditions too, from <c>/api/conditions</c>, so the editor can offer the
/// conditions a treatment may be listed for without a second client.
///
/// The list asks for one wide page, as <see cref="OutbreaksClient"/> does: the
/// screen shows the treatments in one list rather than a pager, and the page
/// still comes back in <see cref="PagedResult{T}"/>, the one collection shape
/// this solution puts on the wire.
/// </remarks>
public sealed class TreatmentsClient(HttpClient http)
{
    /// <summary>How every failure to load the list starts.</summary>
    public const string FailurePrefix = "The treatments could not be loaded";

    /// <summary>How a failure to load one treatment starts.</summary>
    public const string FindFailurePrefix = "The treatment could not be loaded";

    /// <summary>How a failure to create or save a treatment starts.</summary>
    public const string SaveFailurePrefix = "The treatment could not be saved";

    /// <summary>How a failure to load the conditions starts.</summary>
    public const string ConditionsFailurePrefix = "The conditions could not be loaded";

    /// <summary>The largest page the lists ask for, wide enough to be the only one.</summary>
    private const int PageSize = 200;

    private static readonly ApiAsk Ask = new(FailurePrefix, "a list of treatments");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the treatment's record");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the treatment as saved");

    private static readonly ApiAsk AskingConditions = new(ConditionsFailurePrefix, "a list of conditions");

    /// <summary>Every treatment, ordered by name, or why there is no list.</summary>
    public Task<Asked<PagedResult<TreatmentResponse>>> All(
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<TreatmentResponse>>(
            http,
            $"api/treatments?pageSize={PageSize}",
            cancellationToken);

    /// <summary>One treatment with the conditions it is listed for, or why there is none.</summary>
    public Task<Asked<TreatmentDetailResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<TreatmentDetailResponse>(http, $"api/treatments/{id}", cancellationToken);

    /// <summary>Creates a treatment, answering with it as created or why it was not.</summary>
    public Task<Asked<TreatmentDetailResponse>> Create(
        SaveTreatmentRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveTreatmentRequest, TreatmentDetailResponse>(
            http,
            "api/treatments",
            request,
            cancellationToken);

    /// <summary>Saves over a treatment, answering with it as saved or why it was not.</summary>
    public Task<Asked<TreatmentDetailResponse>> Update(
        int id,
        SaveTreatmentRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveTreatmentRequest, TreatmentDetailResponse>(
            http,
            $"api/treatments/{id}",
            request,
            cancellationToken);

    /// <summary>The conditions a treatment can be listed for, or why there is no list.</summary>
    public Task<Asked<PagedResult<ConditionResponse>>> Conditions(
        CancellationToken cancellationToken = default) =>
        AskingConditions.Get<PagedResult<ConditionResponse>>(
            http,
            $"api/conditions?pageSize={PageSize}",
            cancellationToken);
}
