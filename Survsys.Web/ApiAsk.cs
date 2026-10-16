using System.Net.Http.Json;
using System.Text.Json;

namespace Survsys.Web;

/// <summary>
/// What asking the API tier for something produced: the thing, or the reason a
/// screen has none to show.
/// </summary>
/// <remarks>
/// Two states in one record rather than an exception, because the tier's
/// contract is that "an API error becomes a message visible on the screen,
/// never an unhandled exception in a Blazor circuit". A method that threw
/// would leave every screen to remember to catch; a method that returns this
/// leaves a screen with a value it has to look at.
///
/// Session 6 wrote this as <c>ConditionsPage</c>, beside the one client there
/// was. It is generic and its own type now because a page of people, a list of
/// agencies and a created report are not a page of conditions, and three more
/// copies of the same two states would be three more places for that contract
/// to drift.
///
/// <typeparamref name="T"/> is always a reference type here — every wire shape
/// in <c>Survsys.Contracts</c> is a record — which is what lets
/// <see cref="Succeeded"/> be read off the value being there.
/// </remarks>
/// <param name="Value">What the API returned, or null when the ask failed.</param>
/// <param name="Failure">
/// Why there is nothing, in words a user of the screen can act on, or null
/// when there is something.
/// </param>
public sealed record Asked<T>(T? Value, string? Failure)
    where T : class
{
    /// <summary>Whether there is something to show.</summary>
    public bool Succeeded => Value is not null;

    internal static Asked<T> Of(T value) => new(value, null);

    internal static Asked<T> Failed(string failure) => new(null, failure);
}

/// <summary>
/// How this tier asks the API tier for something, and what it says when the
/// asking fails.
/// </summary>
/// <remarks>
/// One of these per client, holding the two phrases a message is built from:
/// what could not be loaded, and what was expected instead. The asking itself
/// is the same for every client, and that is the point of this type — session
/// 6's one client carried the whole chain of failure cases inside itself, and
/// a second copy of a tier's contract is a second contract.
///
/// What a message may say is deliberately narrow, and this is where that is
/// kept. The API tier answers a failure with Problem Details carrying a
/// correlation id, and the detail of what went wrong is in that tier's log
/// under the same id — readable there by someone entitled to read it. A screen
/// in front of a user is not that place, so a message here names the status
/// the API gave and never its body. A 400 this tier caused by sending
/// something wrong is included: a screen validates before it posts, so a
/// caller reading "the API tier answered 400" is reading about a bug rather
/// than about something a user should fix by hand.
///
/// Every method takes the caller's cancellation token and rethrows its
/// cancellation rather than turning it into a message, because a circuit that
/// has gone away has no screen to show one on. A timeout — the same exception
/// type, raised while the caller's token is not cancelled — is a failure like
/// any other.
/// </remarks>
/// <param name="FailurePrefix">
/// How every message from this client starts, naming what could not be
/// loaded: "The conditions could not be loaded".
/// </param>
/// <param name="Expected">
/// What the client expected the API to answer with, named as a message would
/// name it: "a page of conditions".
/// </param>
public sealed record ApiAsk(string FailurePrefix, string Expected)
{
    /// <summary>
    /// Asks the API tier for something, and answers with it or with why there
    /// is none.
    /// </summary>
    public async Task<Asked<T>> Get<T>(
        HttpClient http,
        string url,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(http);

        try
        {
            using var response = await http.GetAsync(url, cancellationToken);

            return await Read<T>(response, cancellationToken);
        }
        catch (Exception failed) when (Caught(failed, cancellationToken) is { } failure)
        {
            return Asked<T>.Failed(failure);
        }
    }

    /// <summary>
    /// Sends the API tier something to create, and answers with what it
    /// created or with why nothing was.
    /// </summary>
    public async Task<Asked<T>> Post<TBody, T>(
        HttpClient http,
        string url,
        TBody body,
        CancellationToken cancellationToken)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(http);

        try
        {
            using var response = await http.PostAsJsonAsync(url, body, cancellationToken);

            return await Read<T>(response, cancellationToken);
        }
        catch (Exception failed) when (Caught(failed, cancellationToken) is { } failure)
        {
            return Asked<T>.Failed(failure);
        }
    }

    /// <summary>
    /// What the API tier answered, read: the value, or why the answer is not
    /// one.
    /// </summary>
    private async Task<Asked<T>> Read<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!response.IsSuccessStatusCode)
        {
            return Asked<T>.Failed(
                $"{FailurePrefix}: the API tier answered {(int)response.StatusCode}. "
                + "Try again, and if it keeps happening the API tier's log holds what "
                + "went wrong.");
        }

        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);

        return value is null
            ? Asked<T>.Failed(
                $"{FailurePrefix}: the API tier answered with an empty body where "
                + $"{Expected} was expected.")
            : Asked<T>.Of(value);
    }

    /// <summary>
    /// The message for a failure worth catching, or null for one that is not
    /// — in which case it carries on out.
    /// </summary>
    /// <remarks>
    /// Used as the <c>when</c> of a catch filter, so a cancellation the caller
    /// asked for is never caught at all rather than being caught and
    /// rethrown.
    /// </remarks>
    private string? Caught(Exception failed, CancellationToken cancellationToken) => failed switch
    {
        HttpRequestException =>
            $"{FailurePrefix}: the API tier did not answer. It may not be running, or "
            + "this tier may be pointed at the wrong address.",

        // The same exception type the caller's own cancellation arrives as,
        // which is why the token is asked rather than the exception: a request
        // that timed out is a failure to report, and a circuit that went away
        // is not.
        TaskCanceledException when !cancellationToken.IsCancellationRequested =>
            $"{FailurePrefix}: the API tier did not answer in time.",

        // JsonException is a body that is not the value; NotSupportedException
        // is a body that was never JSON at all -- an HTML error page from
        // something between the tiers, answered with a 200 of its own.
        JsonException or NotSupportedException =>
            $"{FailurePrefix}: the API tier's answer was not {Expected}.",

        _ => null,
    };
}
