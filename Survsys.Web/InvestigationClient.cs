using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of a report's investigation data:
/// <c>GET</c> and <c>PUT /api/person-conditions/{id}/investigation</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about investigation
/// data, and the failure handling is <see cref="ApiAsk"/>'s, as every
/// client's is.
///
/// The two asks carry two prefixes, since a user whose investigation data
/// could not be read and one whose investigation data could not be saved are
/// being told two different things — the shape <see cref="ClinicalClient"/>
/// uses for the same reason. The save answers with the same
/// <see cref="InvestigationResponse"/> the read does, so the Investigation tab
/// replaces what it shows with what was stored rather than with what it sent.
/// </remarks>
public sealed class InvestigationClient(HttpClient http)
{
    /// <summary>How a failure to read a report's investigation data starts.</summary>
    public const string LoadFailurePrefix = "The investigation data could not be loaded";

    /// <summary>How a failure to save a report's investigation data starts.</summary>
    public const string SaveFailurePrefix = "The investigation data could not be saved";

    private static readonly ApiAsk Finding = new(LoadFailurePrefix, "a report's investigation data");

    private static readonly ApiAsk Saving = new(SaveFailurePrefix, "the investigation data as saved");

    /// <summary>
    /// One report's investigation data, or why there is none — an identifier
    /// nobody has among the reasons, which the API tier answers with a 404.
    /// </summary>
    public Task<Asked<InvestigationResponse>> Find(
        int reportId,
        CancellationToken cancellationToken = default) =>
        Finding.Get<InvestigationResponse>(
            http, $"api/person-conditions/{reportId}/investigation", cancellationToken);

    /// <summary>
    /// Saves a report's investigation data whole and answers with it as the
    /// API tier now holds it, or with why nothing was saved.
    /// </summary>
    /// <remarks>
    /// What the Investigation tab's Save &amp; Continue does. The answer is the
    /// same shape <see cref="Find"/> returns, so a screen replaces what it
    /// shows with what was stored rather than with what it sent.
    /// </remarks>
    public Task<Asked<InvestigationResponse>> Save(
        int reportId,
        UpdateInvestigationRequest investigation,
        CancellationToken cancellationToken = default) =>
        Saving.Put<UpdateInvestigationRequest, InvestigationResponse>(
            http, $"api/person-conditions/{reportId}/investigation", investigation, cancellationToken);
}
