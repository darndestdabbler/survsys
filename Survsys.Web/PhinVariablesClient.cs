using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the PHIN variable catalogue
/// (<c>/api/notification-setup/phin-variables</c>).
/// </summary>
/// <remarks>
/// A variable is named by its identifier, which is escaped into the path. The
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
/// </remarks>
public sealed class PhinVariablesClient(HttpClient http)
{
    /// <summary>How a failure to load the catalogue starts.</summary>
    public const string LoadFailurePrefix = "The PHIN variables could not be loaded";

    /// <summary>How a failure to save or delete a variable starts.</summary>
    public const string SaveFailurePrefix = "The PHIN variable could not be saved";

    private const string Path = "api/notification-setup/phin-variables";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the PHIN variables");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the PHIN variable as saved");

    /// <summary>Every PHIN variable, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<PhinVariableRow>>> List(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<PhinVariableRow>>(http, Path, cancellationToken);

    /// <summary>Adds a variable, answering with it as added or why it was not.</summary>
    public Task<Asked<PhinVariableRow>> Create(PhinVariableRow row, CancellationToken cancellationToken = default) =>
        Saving.Post<PhinVariableRow, PhinVariableRow>(http, Path, row, cancellationToken);

    /// <summary>Saves over a variable, answering with it as saved or why it was not.</summary>
    public Task<Asked<PhinVariableRow>> Update(PhinVariableRow row, CancellationToken cancellationToken = default) =>
        Saving.Put<PhinVariableRow, PhinVariableRow>(http, Of(row.PhinVar), row, cancellationToken);

    /// <summary>Deletes a variable, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(string phinVar, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, Of(phinVar), cancellationToken);

    private static string Of(string phinVar) => $"{Path}/{Uri.EscapeDataString(phinVar.Trim())}";
}
