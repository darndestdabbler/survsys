using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/core-elements</c>, and
/// of the fields core element configuration hides on one report.
/// </summary>
/// <remarks>
/// The only thing in this tier that speaks HTTP about core element
/// configuration, and the failure handling is <see cref="ApiAsk"/>'s, as
/// every client's is.
/// </remarks>
public sealed class CoreElementsClient(HttpClient http)
{
    /// <summary>How a failure to load the elements or their overrides starts.</summary>
    public const string LoadFailurePrefix = "The core element configuration could not be loaded";

    /// <summary>How a failure to save an element's default or an override starts.</summary>
    public const string SaveFailurePrefix = "The core element configuration could not be saved";

    /// <summary>How a failure to delete an override starts.</summary>
    public const string DeleteFailurePrefix = "The override could not be deleted";

    /// <summary>How a failure to load what a report hides starts.</summary>
    public const string HiddenFailurePrefix = "The report's field visibility could not be loaded";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the core element configuration");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the configuration as saved");

    private static readonly ApiAsk Deleting = new(DeleteFailurePrefix, "nothing");

    private static readonly ApiAsk Hiding = new(HiddenFailurePrefix, "the report's hidden fields");

    /// <summary>Every core element with its default, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<CoreElementResponse>>> All(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<CoreElementResponse>>(http, "api/core-elements", cancellationToken);

    /// <summary>Sets an element's default, answering with it as saved or why it was not.</summary>
    public Task<Asked<CoreElementResponse>> SetDefault(
        int id,
        bool hidden,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveCoreElementRequest, CoreElementResponse>(
            http, $"api/core-elements/{id}", new SaveCoreElementRequest(hidden), cancellationToken);

    /// <summary>Every override, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<CoreElementOverrideResponse>>> Overrides(
        CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<CoreElementOverrideResponse>>(
            http, "api/core-elements/overrides", cancellationToken);

    /// <summary>Saves an override, answering with it as saved or why it was not.</summary>
    public Task<Asked<CoreElementOverrideResponse>> SaveOverride(
        SaveCoreElementOverrideRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveCoreElementOverrideRequest, CoreElementOverrideResponse>(
            http, "api/core-elements/overrides", request, cancellationToken);

    /// <summary>Deletes an override, answering null where it was deleted or why it was not.</summary>
    public Task<string?> DeleteOverride(int id, CancellationToken cancellationToken = default) =>
        Deleting.Delete(http, $"api/core-elements/overrides/{id}", cancellationToken);

    /// <summary>The uids of the fields hidden on a report, or why they could not be read.</summary>
    public Task<Asked<IReadOnlyList<string>>> HiddenOn(int reportId, CancellationToken cancellationToken = default) =>
        Hiding.Get<IReadOnlyList<string>>(
            http, $"api/person-conditions/{reportId}/hidden-elements", cancellationToken);
}
