using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/providers</c>.
/// </summary>
/// <remarks>
/// The only thing in this tier that speaks HTTP about providers, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class ProvidersClient(HttpClient http)
{
    /// <summary>How a failure to load the list starts.</summary>
    public const string PageFailurePrefix = "The providers could not be loaded";

    /// <summary>How a failure to load one provider starts.</summary>
    public const string FindFailurePrefix = "The provider could not be loaded";

    /// <summary>How a failure to create or save a provider starts.</summary>
    public const string SaveFailurePrefix = "The provider could not be saved";

    /// <summary>How a failure to import a CSV file starts.</summary>
    public const string ImportFailurePrefix = "The CSV file could not be imported";

    /// <summary>How a failure to export the providers starts.</summary>
    public const string ExportFailurePrefix = "The providers could not be exported";

    private static readonly ApiAsk Paging = new(PageFailurePrefix, "a page of providers");

    private static readonly ApiAsk Finding = new(FindFailurePrefix, "the provider's record");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the provider as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    private static readonly ApiAsk Exporting = new(ExportFailurePrefix, "the providers as CSV");

    /// <summary>
    /// One page of providers, those whose last name starts with
    /// <paramref name="lastName"/> where it is given, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<ProviderResponse>>> Page(
        int page,
        int pageSize,
        string? lastName = null,
        CancellationToken cancellationToken = default) =>
        Paging.Get<PagedResult<ProviderResponse>>(
            http,
            $"api/providers?page={page}&pageSize={pageSize}"
                + (string.IsNullOrWhiteSpace(lastName) ? "" : $"&lastName={Uri.EscapeDataString(lastName.Trim())}"),
            cancellationToken);

    /// <summary>One provider, or why there is none.</summary>
    public Task<Asked<ProviderResponse>> Find(
        int id,
        CancellationToken cancellationToken = default) =>
        Finding.Get<ProviderResponse>(http, $"api/providers/{id}", cancellationToken);

    /// <summary>Creates a provider, answering with them as created or why they were not.</summary>
    public Task<Asked<ProviderResponse>> Create(
        SaveProviderRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveProviderRequest, ProviderResponse>(http, "api/providers", request, cancellationToken);

    /// <summary>Saves over a provider, answering with them as saved or why they were not.</summary>
    public Task<Asked<ProviderResponse>> Update(
        int id,
        SaveProviderRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveProviderRequest, ProviderResponse>(http, $"api/providers/{id}", request, cancellationToken);

    /// <summary>Every provider as CSV, or why there is none.</summary>
    public Task<Asked<string>> Export(CancellationToken cancellationToken = default) =>
        Exporting.GetText(http, "api/providers/csv", cancellationToken);

    /// <summary>
    /// What importing a CSV file of providers would do, saved only where
    /// <paramref name="confirm"/> is true, or why it did nothing.
    /// </summary>
    public async Task<Asked<ReferenceImportResponse>> Import(
        string csv, bool confirm, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await Importing.PostContent<ReferenceImportResponse>(
            http, $"api/providers/csv{(confirm ? "?confirm=true" : "")}", content, cancellationToken);
    }
}
