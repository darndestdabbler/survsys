using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The handler that sends the signed-in person's API token: from the circuit
/// while one runs, from the request while a page is prerendered, and nothing
/// for a person with no token.
/// </summary>
public class ApiTokenHandlerTests
{
    [Fact]
    public async Task SendAsyncSendsTheCircuitsPersonsToken()
    {
        var circuit = new CircuitServicesAccessor
        {
            Services = new ServiceCollection()
                .AddSingleton<AuthenticationStateProvider>(new Signed(Person("circuit-token")))
                .BuildServiceProvider(),
        };

        try
        {
            Assert.Equal(
                new AuthenticationHeaderValue("Bearer", "circuit-token"),
                await AuthorizationSent(circuit, Request(Person("request-token"))));
        }
        finally
        {
            circuit.Services = null;
        }
    }

    [Fact]
    public async Task SendAsyncSendsTheRequestsPersonsTokenOutsideACircuit()
    {
        Assert.Equal(
            new AuthenticationHeaderValue("Bearer", "request-token"),
            await AuthorizationSent(new CircuitServicesAccessor(), Request(Person("request-token"))));
    }

    [Fact]
    public async Task SendAsyncSendsNoTokenForAPersonWithNone()
    {
        Assert.Null(await AuthorizationSent(new CircuitServicesAccessor(), Request(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    [Fact]
    public async Task SendAsyncSendsNoTokenWithNeitherACircuitNorARequest()
    {
        Assert.Null(await AuthorizationSent(new CircuitServicesAccessor(), new HttpContextAccessor()));
    }

    private static async Task<AuthenticationHeaderValue?> AuthorizationSent(
        CircuitServicesAccessor circuit,
        IHttpContextAccessor request)
    {
        HttpRequestMessage? sent = null;
        using var handler = new ApiTokenHandler(circuit, request)
        {
            InnerHandler = new StubTransport(message =>
            {
                sent = message;
                return new HttpResponseMessage(HttpStatusCode.OK);
            }),
        };
        using var http = new HttpClient(handler);

        using var _ = await http.GetAsync("http://api.example.test/api/persons");

        return sent?.Headers.Authorization;
    }

    private static ClaimsPrincipal Person(string token) =>
        new(new ClaimsIdentity([new Claim(AccountClient.ApiTokenClaim, token)], "test"));

    private static HttpContextAccessor Request(ClaimsPrincipal user) =>
        new() { HttpContext = new DefaultHttpContext { User = user } };

    private sealed class Signed(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
