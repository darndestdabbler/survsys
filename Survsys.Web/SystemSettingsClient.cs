using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of <c>/api/system-settings</c>:
/// the system configuration settings the System settings screen edits.
/// </summary>
/// <remarks>
/// The failure handling is <see cref="ApiAsk"/>'s, as every client's is. A
/// name goes into the address escaped, since it is the setting's key there.
/// </remarks>
public sealed class SystemSettingsClient(HttpClient http)
{
    /// <summary>How a failure to load the settings starts.</summary>
    public const string LoadFailurePrefix = "The system settings could not be loaded";

    /// <summary>How a failure to save or delete a setting starts.</summary>
    public const string SaveFailurePrefix = "The system setting could not be saved";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the system settings");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the system setting as saved");

    /// <summary>Every setting, by name, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<SystemSettingResponse>>> All(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<SystemSettingResponse>>(http, "api/system-settings", cancellationToken);

    /// <summary>
    /// Saves a setting under its name, adding it where there is none, and
    /// answers with it as saved or why it was not.
    /// </summary>
    public Task<Asked<SystemSettingResponse>> Save(
        string name,
        SaveSystemSettingRequest request,
        CancellationToken cancellationToken = default) =>
        Saving.Put<SaveSystemSettingRequest, SystemSettingResponse>(
            http, $"api/system-settings/{Uri.EscapeDataString(name)}", request, cancellationToken);

    /// <summary>Deletes a setting, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(string name, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"api/system-settings/{Uri.EscapeDataString(name)}", cancellationToken);
}
