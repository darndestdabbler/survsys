using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of <c>GET /api/agencies</c>: the agencies a
/// case morbidity report may be assigned to.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about agencies, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// It asks for no particular page, because the endpoint takes none: there are
/// sixteen agencies and they arrive in one page, which the API tier's own
/// remark explains. The page still comes back in
/// <see cref="PagedResult{T}"/>, because that is the one collection shape this
/// solution puts on the wire.
/// </remarks>
public sealed class AgenciesClient(HttpClient http)
{
    /// <summary>
    /// How every failure message starts. Public because the screens and the
    /// unit suite read it rather than repeating the wording.
    /// </summary>
    public const string FailurePrefix = "The agencies could not be loaded";

    private static readonly ApiAsk Ask = new(FailurePrefix, "a list of agencies");

    /// <summary>
    /// Every agency a report may be assigned to, in name order, or why there
    /// is no list.
    /// </summary>
    public Task<Asked<PagedResult<AgencyResponse>>> All(
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<AgencyResponse>>(http, "api/agencies", cancellationToken);
}
