using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/ecr-condition-mappings</c>:
/// the condition codes a reportability response can carry and the catalogue
/// condition each maps to, with their CSV export and import.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class EcrConditionMappingsClient(HttpClient http)
{
    /// <summary>How a failure to load the mapping starts.</summary>
    public const string ListFailurePrefix = "The eCR condition mapping could not be loaded";

    /// <summary>How a failure to add, save or delete a mapping starts.</summary>
    public const string SaveFailurePrefix = "The condition mapping could not be saved";

    /// <summary>How a failure to import a CSV file starts.</summary>
    public const string ImportFailurePrefix = "The CSV file could not be imported";

    /// <summary>How a failure to export the mapping starts.</summary>
    public const string ExportFailurePrefix = "The eCR condition mapping could not be exported";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "the eCR condition mapping");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the condition mapping as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    private static readonly ApiAsk Exporting = new(ExportFailurePrefix, "the eCR condition mapping as CSV");

    /// <summary>Every mapping, by code system and then code, or why there is none.</summary>
    public Task<Asked<List<EcrConditionMappingResponse>>> List(CancellationToken cancellationToken = default) =>
        Listing.Get<List<EcrConditionMappingResponse>>(http, "api/ecr-condition-mappings", cancellationToken);

    /// <summary>Adds a mapping, answering with it or why it was not.</summary>
    public Task<Asked<EcrConditionMappingResponse>> Add(
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Post<SaveEcrConditionMappingRequest, EcrConditionMappingResponse>(
            http, "api/ecr-condition-mappings", request, cancellationToken);

    /// <summary>Saves over a mapping, answering with it or why it was not.</summary>
    public Task<Asked<EcrConditionMappingResponse>> Save(
        int id,
        SaveEcrConditionMappingRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveEcrConditionMappingRequest, EcrConditionMappingResponse>(
            http, $"api/ecr-condition-mappings/{id}", request, cancellationToken);

    /// <summary>Deletes a mapping, answering with null on success or why it was not.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/ecr-condition-mappings/{id}", cancellationToken);

    /// <summary>The mapping as CSV, or why there is none.</summary>
    public Task<Asked<string>> Export(CancellationToken cancellationToken = default) =>
        Exporting.GetText(http, "api/ecr-condition-mappings/csv", cancellationToken);

    /// <summary>
    /// What importing a CSV file into the mapping would do, saved only where
    /// <paramref name="confirm"/> is true, or why it did nothing.
    /// </summary>
    public async Task<Asked<ReferenceImportResponse>> Import(
        string csv, bool confirm, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await Importing.PostContent<ReferenceImportResponse>(
            http, $"api/ecr-condition-mappings/csv{(confirm ? "?confirm=true" : "")}", content, cancellationToken);
    }
}
