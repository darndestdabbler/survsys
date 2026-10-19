using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Survsys.Web;

/// <summary>
/// Sends the signed-in person's API token on every request a typed client
/// makes, so that the API tier authorises the person and not this tier.
/// </summary>
/// <remarks>
/// The person is read from the circuit's authentication state while a circuit
/// is running, and from the HTTP request while a page is being prerendered,
/// which is the one time there is a request and no circuit. A person with no
/// token sends none, and the API tier answers 401 as it would anyone.
/// </remarks>
public sealed class ApiTokenHandler(CircuitServicesAccessor circuit, IHttpContextAccessor request)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message,
        CancellationToken cancellationToken)
    {
        if ((await SignedIn())?.FindFirstValue(AccountClient.ApiTokenClaim) is { Length: > 0 } token)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(message, cancellationToken);
    }

    private async Task<ClaimsPrincipal?> SignedIn() =>
        circuit.Services?.GetService<AuthenticationStateProvider>() is { } state
            ? (await state.GetAuthenticationStateAsync()).User
            : request.HttpContext?.User;
}
