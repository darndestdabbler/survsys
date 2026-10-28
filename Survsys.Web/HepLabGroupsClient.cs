using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of the hepatitis lab groups
/// (<c>/api/notification-setup/hep-lab-groups</c>), each read and saved whole
/// with its conditions and test types.
/// </summary>
/// <remarks>The failure handling is <see cref="ApiAsk"/>'s, as every client's is.</remarks>
public sealed class HepLabGroupsClient(HttpClient http)
{
    /// <summary>How a failure to load the groups starts.</summary>
    public const string LoadFailurePrefix = "The hepatitis lab groups could not be loaded";

    /// <summary>How a failure to save or delete a group starts.</summary>
    public const string SaveFailurePrefix = "The hepatitis lab group could not be saved";

    private const string Path = "api/notification-setup/hep-lab-groups";

    private static readonly ApiAsk Loading = new(LoadFailurePrefix, "the hepatitis lab groups");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the hepatitis lab group as saved");

    /// <summary>Every group, or why there are none.</summary>
    public Task<Asked<IReadOnlyList<HepLabGroupRow>>> List(CancellationToken cancellationToken = default) =>
        Loading.Get<IReadOnlyList<HepLabGroupRow>>(http, Path, cancellationToken);

    /// <summary>Adds a group where its id is 0, or saves over the group with its id; answers with it or why not.</summary>
    public Task<Asked<HepLabGroupRow>> Save(HepLabGroupRow row, CancellationToken cancellationToken = default) =>
        row.Id == 0
            ? Saving.Post<HepLabGroupRow, HepLabGroupRow>(http, Path, row, cancellationToken)
            : Saving.Put<HepLabGroupRow, HepLabGroupRow>(http, $"{Path}/{row.Id}", row, cancellationToken);

    /// <summary>Deletes a group, answering null or why it was not deleted.</summary>
    public Task<string?> Delete(int id, CancellationToken cancellationToken = default) =>
        Saving.Delete(http, $"{Path}/{id}", cancellationToken);
}
