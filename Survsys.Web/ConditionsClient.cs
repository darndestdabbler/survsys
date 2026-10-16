using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader of <c>GET /api/conditions</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about conditions, and it
/// swallows no failure silently: each one comes back as a sentence beginning
/// <see cref="FailurePrefix"/>, which is what the screen shows.
///
/// The failure handling itself is <see cref="ApiAsk"/>'s. It was written here
/// in session 6, when this was the only client in the tier; it moved when
/// there were four, for the reason the page-bounds rule moved out of
/// <c>ConditionEndpoints</c> into <c>Paging</c> — a second copy of a contract
/// is a second contract, and they agree until one of them is changed. Every
/// message a user can see is the same sentence it was before the move.
/// </remarks>
public sealed class ConditionsClient(HttpClient http)
{
    /// <summary>
    /// How every failure message starts. Public because the screen's own
    /// tests and the unit suite both read it rather than repeating the
    /// wording.
    /// </summary>
    public const string FailurePrefix = "The conditions could not be loaded";

    private static readonly ApiAsk Ask = new(FailurePrefix, "a page of conditions");

    /// <summary>
    /// One page of conditions, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<ConditionResponse>>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Ask.Get<PagedResult<ConditionResponse>>(
            http, $"api/conditions?page={page}&pageSize={pageSize}", cancellationToken);
}
