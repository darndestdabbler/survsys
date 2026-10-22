using System.Text;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the notification guides
/// (<c>/api/notification-setup/guides</c>) and of the header variable
/// defaults (<c>/api/notification-setup/guide-variable-defaults</c>).
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is. A
/// guide's JSON is read and imported as a <see cref="GuideExport"/>; the
/// variable defaults' CSV export answers text.
/// </remarks>
public sealed class GuidesClient(HttpClient http)
{
    /// <summary>How a failure to load the guides, a guide or the variable defaults starts.</summary>
    public const string LoadFailurePrefix = "The notification guides could not be loaded";

    /// <summary>How a failure to save or delete a guide or a variable default starts.</summary>
    public const string SaveFailurePrefix = "The notification guide could not be saved";

    /// <summary>How a failure to import a guide's JSON or the defaults' CSV starts.</summary>
    public const string ImportFailurePrefix = "The file could not be imported";

    private const string Guides = "api/notification-setup/guides";
    private const string DefaultsPath = "api/notification-setup/guide-variable-defaults";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the notification guides");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the notification guide as saved");

    private static readonly ApiAsk Importing = new(ImportFailurePrefix, "what the import did");

    /// <summary>Every guide, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<GuideRow>>> List(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<GuideRow>>(http, Guides, cancellationToken);

    /// <summary>A guide whole, or why there is none.</summary>
    public Task<Asked<GuideDetailResponse>> Get(int id, CancellationToken cancellationToken = default) =>
        Loading.Get<GuideDetailResponse>(http, $"{Guides}/{id}", cancellationToken);

    /// <summary>Creates a guide, answering with it or why it was not created.</summary>
    public Task<Asked<GuideDetailResponse>> Create(GuideRow row, CancellationToken cancellationToken = default) =>
        Saving.Post<GuideRow, GuideDetailResponse>(http, Guides, row, cancellationToken);

    /// <summary>Saves a guide's name and version, answering with it or why it was not saved.</summary>
    public Task<Asked<GuideDetailResponse>> Update(int id, GuideRow row, CancellationToken cancellationToken = default) =>
        Saving.Put<GuideRow, GuideDetailResponse>(http, $"{Guides}/{id}", row, cancellationToken);

    /// <summary>Deletes a guide, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"{Guides}/{id}", cancellationToken);

    /// <summary>Replaces the PHIN variables a guide carries.</summary>
    public Task<Asked<GuideDetailResponse>> SavePhinVariables(
        int id, IReadOnlyList<string> phinVars, CancellationToken cancellationToken = default) =>
        Saving.Put<IReadOnlyList<string>, GuideDetailResponse>(http, $"{Guides}/{id}/phin-variables", phinVars, cancellationToken);

    /// <summary>Replaces the guides a guide links.</summary>
    public Task<Asked<GuideDetailResponse>> SaveLinks(
        int id, IReadOnlyList<int> linkedGuideIds, CancellationToken cancellationToken = default) =>
        Saving.Put<IReadOnlyList<int>, GuideDetailResponse>(http, $"{Guides}/{id}/linked-guides", linkedGuideIds, cancellationToken);

    /// <summary>Replaces a guide's own header variable values.</summary>
    public Task<Asked<GuideDetailResponse>> SaveVariables(
        int id, IReadOnlyList<GuideVariableRow> variables, CancellationToken cancellationToken = default) =>
        Saving.Put<IReadOnlyList<GuideVariableRow>, GuideDetailResponse>(http, $"{Guides}/{id}/variables", variables, cancellationToken);

    /// <summary>A guide as JSON, or why there is none.</summary>
    public Task<Asked<GuideExport>> Export(int id, CancellationToken cancellationToken = default) =>
        Loading.Get<GuideExport>(http, $"{Guides}/{id}/json", cancellationToken);

    /// <summary>Imports a guide's JSON, answering with the guide as imported or why it was not.</summary>
    public Task<Asked<GuideDetailResponse>> Import(GuideExport export, CancellationToken cancellationToken = default) =>
        Importing.Post<GuideExport, GuideDetailResponse>(http, $"{Guides}/json", export, cancellationToken);

    /// <summary>Every header variable default, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<GuideVariableDefaultRow>>> Defaults(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<GuideVariableDefaultRow>>(http, DefaultsPath, cancellationToken);

    /// <summary>Adds a header variable default, or saves over the one with <paramref name="row"/>'s id.</summary>
    public Task<Asked<GuideVariableDefaultRow>> SaveDefault(GuideVariableDefaultRow row, CancellationToken cancellationToken = default) =>
        row.Id == 0
            ? Saving.Post<GuideVariableDefaultRow, GuideVariableDefaultRow>(http, DefaultsPath, row, cancellationToken)
            : Saving.Put<GuideVariableDefaultRow, GuideVariableDefaultRow>(http, $"{DefaultsPath}/{row.Id}", row, cancellationToken);

    /// <summary>Deletes a header variable default, answering null or why it was not deleted.</summary>
    public Task<string?> DeleteDefault(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"{DefaultsPath}/{id}", cancellationToken);

    /// <summary>What importing a CSV file of defaults would do, saved only where <paramref name="confirm"/> is true.</summary>
    public async Task<Asked<ReferenceImportResponse>> ImportDefaults(
        string csv, bool confirm, CancellationToken cancellationToken = default)
    {
        using var content = new StringContent(csv, Encoding.UTF8, "text/csv");
        return await Importing.PostContent<ReferenceImportResponse>(
            http, $"{DefaultsPath}/csv{(confirm ? "?confirm=true" : "")}", content, cancellationToken);
    }

    /// <summary>The header variable defaults as CSV, or why there are none.</summary>
    public Task<Asked<string>> ExportDefaults(CancellationToken cancellationToken = default) =>
        Loading.GetText(http, $"{DefaultsPath}/csv", cancellationToken);
}
