using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/vocabulary</c>, the ELR
/// vocabulary intake translates through, and of a condition's intake rules
/// (<c>/api/conditions/{id}/rules</c>).
/// </summary>
/// <remarks>
/// The five tables share one shape of endpoint, so a table is named by its
/// path segment and its row type rather than by a method of its own. The
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is, except
/// the export, which answers CSV rather than JSON and reads it as the saved
/// view export does.
/// </remarks>
public sealed class VocabularyClient(HttpClient http)
{
    public const string MasterLoincs = "master-loincs";
    public const string ChildLoincs = "child-loincs";
    public const string Organisms = "organisms";
    public const string MasterValues = "master-values";
    public const string ChildValues = "child-values";

    /// <summary>How a failure to load a table or a condition's rules starts.</summary>
    public const string LoadFailurePrefix = "The vocabulary could not be loaded";

    /// <summary>How a failure to save or delete an entry, or to save the rules, starts.</summary>
    public const string SaveFailurePrefix = "The vocabulary could not be saved";

    /// <summary>How a failure to import a CSV file starts.</summary>
    public const string ImportFailurePrefix = "The CSV file could not be imported";

    /// <summary>How a failure to export a table starts.</summary>
    public const string ExportFailurePrefix = "The vocabulary could not be exported";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the vocabulary");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the vocabulary as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    /// <summary>Every entry of a table, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<TRow>>> List<TRow>(string table, CancellationToken cancellationToken = default)
        where TRow : class =>
        Loading.Get<IReadOnlyList<TRow>>(http, Path(table), cancellationToken);

    /// <summary>Adds an entry to a table, answering with it as added or why it was not.</summary>
    public Task<Asked<TRow>> Create<TRow>(string table, TRow row, CancellationToken cancellationToken = default)
        where TRow : class =>
        Saving.Post<TRow, TRow>(http, Path(table), row, cancellationToken);

    /// <summary>Saves over an entry, answering with it as saved or why it was not.</summary>
    public Task<Asked<TRow>> Update<TRow>(
        string table, int id, TRow row, CancellationToken cancellationToken = default)
        where TRow : class =>
        Saving.Put<TRow, TRow>(http, $"{Path(table)}/{id}", row, cancellationToken);

    /// <summary>Deletes an entry, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(string table, int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"{Path(table)}/{id}", cancellationToken);

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

    /// <summary>A table as CSV, or why there is none.</summary>
    public async Task<Asked<string>> Export(string table, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync($"{Path(table)}/csv", cancellationToken);

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

    /// <summary>A condition's intake rules in execution order, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<VocabRuleRow>>> Rules(int conditionId, CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<VocabRuleRow>>(http, $"api/conditions/{conditionId}/rules", cancellationToken);

    /// <summary>Replaces a condition's intake rules, answering with them as saved or why they were not.</summary>
    public Task<Asked<IReadOnlyList<VocabRuleRow>>> SaveRules(
        int conditionId, IReadOnlyList<VocabRuleRow> rules, CancellationToken cancellationToken = default) =>
        Saving.Put<IReadOnlyList<VocabRuleRow>, IReadOnlyList<VocabRuleRow>>(
            http, $"api/conditions/{conditionId}/rules", rules, cancellationToken);

    private static string Path(string table) => $"api/vocabulary/{Uri.EscapeDataString(table)}";
}
