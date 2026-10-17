using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's administrative
/// data: <c>GET</c> and <c>PUT /api/person-conditions/{id}/administrative</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about administrative
/// data, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is.
///
/// The two asks carry two prefixes, since a user whose administrative data
/// could not be read and one whose administrative data could not be saved are
/// being told two different things — the shape <see cref="ClinicalClient"/>
/// uses for the same reason. The save answers with the same
/// <see cref="AdministrativeResponse"/> the read does, so the Administrative
/// tab replaces what it shows with what was stored rather than with what it
/// sent.
/// </remarks>
public sealed class AdministrativeClient(HttpClient http)
{
    /// <summary>How a failure to read a report's administrative data starts.</summary>
    public const string LoadFailurePrefix = "The administrative data could not be loaded";

    /// <summary>How a failure to save a report's administrative data starts.</summary>
    public const string SaveFailurePrefix = "The administrative data could not be saved";

    private static readonly ApiAsk Finding = new(LoadFailurePrefix, "a report's administrative data");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the administrative data as saved");

    /// <summary>
    /// One report's administrative data, or why there is none — an identifier
    /// nobody has among the reasons, which the API tier answers with a 404.
    /// </summary>
    public Task<Asked<AdministrativeResponse>> Find(
        int reportId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<AdministrativeResponse>(
            http, $"api/person-conditions/{reportId}/administrative", cancellationToken);

    /// <summary>
    /// Saves a report's administrative data whole and answers with it as the
    /// API tier now holds it, or with why nothing was saved.
    /// </summary>
    /// <remarks>
    /// What the Administrative tab's Save &amp; Continue does. The answer is
    /// the same shape <see cref="Find"/> returns, so a screen replaces what it
    /// shows with what was stored rather than with what it sent.
    /// </remarks>
    public Task<Asked<AdministrativeResponse>> Save(
        int reportId,
        UpdateAdministrativeRequest administrative,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateAdministrativeRequest, AdministrativeResponse>(
            http, $"api/person-conditions/{reportId}/administrative", administrative, cancellationToken);
}
