using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's clinical data:
/// <c>GET</c> and <c>PUT /api/person-conditions/{id}/clinical</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about clinical data, and
/// the failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// The two asks carry two prefixes, since a user whose clinical data could not
/// be read and one whose clinical data could not be saved are being told two
/// different things — the shape <see cref="PersonsClient"/> uses for the same
/// reason. The save answers with the same <see cref="ClinicalResponse"/> the
/// read does, so the Clinical tab replaces what it shows with what was stored
/// rather than with what it sent.
/// </remarks>
public sealed class ClinicalClient(HttpClient http)
{
    /// <summary>How a failure to read a report's clinical data starts.</summary>
    public const string LoadFailurePrefix = "The clinical data could not be loaded";

    /// <summary>How a failure to save a report's clinical data starts.</summary>
    public const string SaveFailurePrefix = "The clinical data could not be saved";

    private static readonly ApiAsk Finding = new(LoadFailurePrefix, "a report's clinical data");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the clinical data as saved");

    /// <summary>
    /// One report's clinical data, or why there is none — an identifier nobody
    /// has among the reasons, which the API tier answers with a 404.
    /// </summary>
    public Task<Asked<ClinicalResponse>> Find(
        int reportId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<ClinicalResponse>(
            http, $"api/person-conditions/{reportId}/clinical", cancellationToken);

    /// <summary>
    /// Saves a report's clinical data whole and answers with it as the API
    /// tier now holds it, or with why nothing was saved.
    /// </summary>
    /// <remarks>
    /// What the Clinical tab's Save &amp; Continue does. The answer is the same
    /// shape <see cref="Find"/> returns, so a screen replaces what it shows
    /// with what was stored rather than with what it sent.
    /// </remarks>
    public Task<Asked<ClinicalResponse>> Save(
        int reportId,
        UpdateClinicalRequest clinical,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateClinicalRequest, ClinicalResponse>(
            http, $"api/person-conditions/{reportId}/clinical", clinical, cancellationToken);
}
