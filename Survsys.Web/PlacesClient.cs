using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/ct-towns</c> and
/// <c>/api/zip-codes</c>: the Connecticut towns with their county and
/// planning region, and the zip codes with their county.
/// </summary>
/// <remarks>
/// The two tables share one shape of endpoint, so a table is named by its
/// path segment and its row type, as <see cref="VocabularyClient"/> names its
/// tables. A row is saved over by its key — the town, or the zip — which is
/// never changed. The failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is.
/// </remarks>
public sealed class PlacesClient(HttpClient http)
{
    public const string Towns = "ct-towns";
    public const string ZipCodes = "zip-codes";

    /// <summary>How a failure to load a table starts.</summary>
    public const string LoadFailurePrefix = "The towns and zip codes could not be loaded";

    /// <summary>How a failure to add or save a row starts.</summary>
    public const string SaveFailurePrefix = "The town or zip code could not be saved";

    /// <summary>How a failure to import a CSV file starts.</summary>
    public const string ImportFailurePrefix = "The CSV file could not be imported";

    /// <summary>How a failure to export a table starts.</summary>
    public const string ExportFailurePrefix = "The towns or zip codes could not be exported";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the rows of the table");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the row as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    private static readonly ApiAsk Exporting = new(ExportFailurePrefix, "the table as CSV");

    /// <summary>Every row of a table, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<TRow>>> List<TRow>(string table, CancellationToken cancellationToken = default)
        where TRow : class =>
        Loading.Get<IReadOnlyList<TRow>>(http, Path(table), cancellationToken);

    /// <summary>Adds a row to a table, answering with it as added or why it was not.</summary>
    public Task<Asked<TRow>> Create<TRow>(string table, TRow row, CancellationToken cancellationToken = default)
        where TRow : class =>
        Saving.Post<TRow, TRow>(http, Path(table), row, cancellationToken);

    /// <summary>Saves over the row with this key, answering with it as saved or why it was not.</summary>
    public Task<Asked<TRow>> Update<TRow>(
        string table, string key, TRow row, CancellationToken cancellationToken = default)
        where TRow : class =>
        Saving.Put<TRow, TRow>(http, $"{Path(table)}/{Uri.EscapeDataString(key)}", row, cancellationToken);

    /// <summary>A table as CSV, or why there is none.</summary>
    public Task<Asked<string>> Export(string table, CancellationToken cancellationToken = default) =>
        Exporting.GetText(http, $"{Path(table)}/csv", cancellationToken);

    /// <summary>
    /// What importing a CSV file into a table would do, saved only where
    /// <paramref name="confirm"/> is true, or why it did nothing.
    /// </summary>
    public async Task<Asked<ReferenceImportResponse>> Import(
        string table, string csv, bool confirm, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await Importing.PostContent<ReferenceImportResponse>(
            http, $"{Path(table)}/csv{(confirm ? "?confirm=true" : "")}", content, cancellationToken);
    }

    private static string Path(string table) => $"api/{Uri.EscapeDataString(table)}";
}
