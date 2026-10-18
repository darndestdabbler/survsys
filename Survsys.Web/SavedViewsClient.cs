using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/saved-views</c>: the
/// signed-in person's saved views, and each one's export.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is,
/// except for the export, whose answer is CSV text rather than JSON.
/// </remarks>
public sealed class SavedViewsClient(HttpClient http)
{
    /// <summary>How a failure to load the saved views starts.</summary>
    public const string LoadFailurePrefix = "The saved views could not be loaded";

    /// <summary>How a failure to save or delete a saved view starts.</summary>
    public const string SaveFailurePrefix = "The saved view could not be saved";

    /// <summary>How a failure to export a saved view starts.</summary>
    public const string ExportFailurePrefix = "The saved view could not be exported";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the saved views");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the saved view as saved");

    /// <summary>The signed-in person's saved views, or why there are none.</summary>
    public Task<Asked<PagedResult<SavedViewResponse>>> All(CancellationToken cancellationToken = default) =>
        Loading.Get<PagedResult<SavedViewResponse>>(http, "api/saved-views", cancellationToken);

    /// <summary>Saves a new view, answering with it as saved or why it was not.</summary>
    public Task<Asked<SavedViewResponse>> Create(SaveSavedViewRequest request, CancellationToken cancellationToken = default) =>
        Saving.Post<SaveSavedViewRequest, SavedViewResponse>(http, "api/saved-views", request, cancellationToken);

    /// <summary>Saves over a view, answering with it as saved or why it was not.</summary>
    public Task<Asked<SavedViewResponse>> Update(
        int id,
        SaveSavedViewRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveSavedViewRequest, SavedViewResponse>(http, $"api/saved-views/{id}", request, cancellationToken);

    /// <summary>Deletes a view, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/saved-views/{id}", cancellationToken);

    /// <summary>The CSV of the reports a view selects, or why there is none.</summary>
    public async Task<Asked<string>> Export(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"api/saved-views/{id}/export", cancellationToken);

            return response.IsSuccessStatusCode
                ? Asked<string>.Of(await response.Content.ReadAsStringAsync(cancellationToken))
                : Asked<string>.Failed($"{ExportFailurePrefix}: the API tier answered {(int)response.StatusCode}.");
        }
        catch (HttpRequestException)
        {
            return Asked<string>.Failed($"{ExportFailurePrefix}: the API tier did not answer.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Asked<string>.Failed($"{ExportFailurePrefix}: the API tier did not answer in time.");
        }
    }
}
