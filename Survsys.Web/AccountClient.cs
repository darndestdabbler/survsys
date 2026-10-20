using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's way of signing a person in to the API tier: a user
/// name and password exchanged for a bearer token, and that token exchanged
/// for who it says the person is.
/// </summary>
/// <remarks>
/// The one client registered without <see cref="ApiTokenHandler"/>, because it
/// is what obtains the token that handler sends. <see cref="PrincipalFor"/> is
/// what this tier's cookie then carries: the person's names, their privileges
/// for the navigation to read, and the token for every later API call.
/// </remarks>
public sealed class AccountClient(HttpClient http)
{
    /// <summary>The claim type the API tier's bearer token travels as, inside this tier's cookie.</summary>
    public const string ApiTokenClaim = "api_token";

    /// <summary>The claim type a signed-in person's display name travels as.</summary>
    public const string DisplayNameClaim = "display_name";

    /// <summary>The claim type a signed-in person's privileges travel as.</summary>
    public const string PrivilegeClaim = "privilege";

    /// <summary>What a person is told when their user name or password was refused.</summary>
    public const string RefusedFailure = "The user name or password is not right, or the user may not sign in.";

    /// <summary>How a failure to reach the API tier while signing in starts.</summary>
    public const string FailurePrefix = "Signing in could not be completed";

    /// <summary>
    /// The bearer token for this user name and password, or why there is none:
    /// <see cref="RefusedFailure"/> where the API tier refused them.
    /// </summary>
    public async Task<Asked<string>> SignIn(SignInRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/sign-in")
        {
            Content = JsonContent.Create(request),
        };

        var asked = await Send<TokenBody>(message, cancellationToken);

        return asked.Value?.AccessToken is { Length: > 0 } token
            ? Asked<string>.Of(token)
            : Asked<string>.Failed(asked.Failure ?? $"{FailurePrefix}: the API tier's answer carried no token.");
    }

    /// <summary>
    /// Who the token says the person is, or why that could not be read.
    /// </summary>
    /// <remarks>
    /// The token is sent on this one request rather than through
    /// <see cref="ApiTokenHandler"/>, because the person is not signed in to
    /// this tier yet: their cookie is written from what this answers.
    /// </remarks>
    public async Task<Asked<CurrentUserResponse>> Me(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        using var message = new HttpRequestMessage(HttpMethod.Get, "api/me");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await Send<CurrentUserResponse>(message, cancellationToken);
    }

    /// <summary>
    /// Sends one request of the sign-in exchange and reads its answer, in the
    /// words <see cref="ApiAsk"/> would use but for a 401, which here means
    /// the person was refused.
    /// </summary>
    /// <remarks>
    /// Not <see cref="ApiAsk"/> itself, because a request here carries a
    /// header of its own and a 401 is an answer to show rather than a fault.
    /// </remarks>
    private async Task<Asked<T>> Send<T>(HttpRequestMessage message, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            using var response = await http.SendAsync(message, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Asked<T>.Failed(RefusedFailure);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Asked<T>.Failed($"{FailurePrefix}: the API tier answered {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken) is { } value
                ? Asked<T>.Of(value)
                : Asked<T>.Failed($"{FailurePrefix}: the API tier answered with an empty body.");
        }
        catch (HttpRequestException)
        {
            return Asked<T>.Failed($"{FailurePrefix}: the API tier did not answer.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Asked<T>.Failed($"{FailurePrefix}: the API tier did not answer in time.");
        }
    }

    /// <summary>
    /// The principal this tier's cookie carries for a signed-in person: their
    /// names, their privileges and the API tier's token.
    /// </summary>
    public static ClaimsPrincipal PrincipalFor(CurrentUserResponse user, string token)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(token);

        Claim[] identity =
        [
            new(ClaimTypes.Name, user.UserName),
            new(DisplayNameClaim, user.DisplayName),
            new(ApiTokenClaim, token),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(
            identity.Concat(user.Privileges.Select(privilege => new Claim(PrivilegeClaim, privilege))),
            CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>The part of the API tier's token answer this tier reads.</summary>
    private sealed record TokenBody(string? AccessToken);
}
