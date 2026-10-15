using System.Net.Http.Json;
using System.Text.Json;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// What asking the API tier for one page of conditions produced: the page, or
/// the reason a screen has none to show.
/// </summary>
/// <remarks>
/// Two states in one record rather than an exception, because the tier's
/// contract is that "an API error becomes a message visible on the screen,
/// never an unhandled exception in a Blazor circuit". A method that threw
/// would leave every screen to remember to catch; a method that returns this
/// leaves a screen with a value it has to look at.
/// </remarks>
/// <param name="Result">The page the API returned, or null when the ask failed.</param>
/// <param name="Failure">
/// Why there is no page, in words a user of the screen can act on, or null
/// when there is one.
/// </param>
public sealed record ConditionsPage(PagedResult<ConditionResponse>? Result, string? Failure)
{
    /// <summary>Whether there is a page to show.</summary>
    public bool Succeeded => Result is not null;

    internal static ConditionsPage Of(PagedResult<ConditionResponse> result) => new(result, null);

    internal static ConditionsPage Failed(string failure) => new(null, failure);
}

/// <summary>
/// The application tier's reader of <c>GET /api/conditions</c>.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP to the API, and it
/// swallows no failure silently: each one it catches comes back as a sentence
/// beginning <see cref="FailurePrefix"/>, which is what the screen shows.
///
/// What a message may say is deliberately narrow. The API tier answers a
/// failure with Problem Details carrying a correlation id, and the detail of
/// what went wrong is in that tier's log under the same id — readable there by
/// someone entitled to read it. A screen in front of a user is not that place,
/// so a message here names the status the API gave and never its body.
/// </remarks>
public sealed class ConditionsClient(HttpClient http)
{
    /// <summary>
    /// How every failure message starts. Public because the screen's own
    /// tests and the unit suite both read it rather than repeating the
    /// wording.
    /// </summary>
    public const string FailurePrefix = "The conditions could not be loaded";

    /// <summary>
    /// One page of conditions, or why there is none.
    /// </summary>
    /// <remarks>
    /// The cancellation token is the screen's: a circuit that goes away while
    /// the ask is in flight cancels it, and that cancellation is rethrown
    /// rather than turned into a message, because there is no longer a screen
    /// to show one on. A timeout — the same exception type, raised while the
    /// caller's token is not cancelled — is a failure like any other.
    /// </remarks>
    public async Task<ConditionsPage> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await http.GetAsync(
                $"api/conditions?page={page}&pageSize={pageSize}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return ConditionsPage.Failed(
                    $"{FailurePrefix}: the API tier answered {(int)response.StatusCode}. "
                    + "Try again, and if it keeps happening the API tier's log holds what "
                    + "went wrong.");
            }

            var result = await response.Content
                .ReadFromJsonAsync<PagedResult<ConditionResponse>>(cancellationToken);

            return result is null
                ? ConditionsPage.Failed(
                    $"{FailurePrefix}: the API tier answered with an empty body where a page "
                    + "of conditions was expected.")
                : ConditionsPage.Of(result);
        }
        catch (HttpRequestException)
        {
            return ConditionsPage.Failed(
                $"{FailurePrefix}: the API tier did not answer. It may not be running, or "
                + "this tier may be pointed at the wrong address.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ConditionsPage.Failed(
                $"{FailurePrefix}: the API tier did not answer in time.");
        }
        // JsonException is a body that is not the page; NotSupportedException
        // is a body that was never JSON at all -- an HTML error page from
        // something between the tiers, answered with a 200 of its own.
        catch (Exception unreadable) when (unreadable is JsonException or NotSupportedException)
        {
            return ConditionsPage.Failed(
                $"{FailurePrefix}: the API tier's answer was not a page of conditions.");
        }
    }
}
