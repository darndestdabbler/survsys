using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's sign-in exchange with the API tier: the requests it
/// sends, what it hands back, what a refusal says, and the principal the
/// cookie carries.
/// </summary>
public class AccountClientTests
{
    private const string Json = "application/json";

    private const string Ada =
        """{ "id": 7, "userName": "ada", "displayName": "Ada Lovelace", "privileges": ["view_cases", "manage_users"] }""";

    [Fact]
    public async Task SignInPostsTheCredentialsAndAnswersWithTheToken()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
        }, HttpStatusCode.OK, """{ "tokenType": "Bearer", "accessToken": "t0ken", "expiresIn": 28800 }""");

        var asked = await client.SignIn(new SignInRequest("ada", "pw"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/sign-in", sent?.RequestUri?.ToString());
        Assert.Contains("\"userName\":\"ada\"", body, StringComparison.Ordinal);
        Assert.Equal("t0ken", asked.Value);
    }

    [Fact]
    public async Task SignInRefusedSaysSoInWordsAPersonCanActOn()
    {
        var client = Recording(_ => { }, HttpStatusCode.Unauthorized, "{}");

        Assert.Equal(AccountClient.RefusedFailure, (await client.SignIn(new SignInRequest("ada", "wrong"))).Failure);
    }

    [Fact]
    public async Task SignInThatFailsOtherwiseCarriesThePrefixAndTheStatus()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        var failure = (await client.SignIn(new SignInRequest("ada", "pw"))).Failure;

        Assert.StartsWith(AccountClient.FailurePrefix, failure);
        Assert.Contains("500", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SignInWithTheApiTierDownSaysItDidNotAnswer()
    {
        var client = new AccountClient(new HttpClient(new StubTransport(_ => throw new HttpRequestException()))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        Assert.Contains("did not answer", (await client.SignIn(new SignInRequest("ada", "pw"))).Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MeSendsTheTokenAndAnswersWithWhoItSaysThePersonIs()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Ada);

        var asked = await client.Me("t0ken");

        Assert.Equal("http://api.example.test/api/me", sent?.RequestUri?.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "t0ken"), sent?.Headers.Authorization);
        Assert.Equal(7, asked.Value?.Id);
        Assert.Equal([Privileges.ViewCases, Privileges.ManageUsers], asked.Value?.Privileges);
    }

    [Fact]
    public void PrincipalForCarriesTheNamesThePrivilegesAndTheToken()
    {
        var principal = AccountClient.PrincipalFor(
            new CurrentUserResponse(7, "ada", "Ada Lovelace", [Privileges.ViewCases, Privileges.ManageUsers]),
            "t0ken");

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("ada", principal.Identity?.Name);
        Assert.Equal("Ada Lovelace", principal.FindFirstValue(AccountClient.DisplayNameClaim));
        Assert.Equal("t0ken", principal.FindFirstValue(AccountClient.ApiTokenClaim));
        Assert.Equal(
            [Privileges.ViewCases, Privileges.ManageUsers],
            principal.FindAll(AccountClient.PrivilegeClaim).Select(claim => claim.Value));
    }

    private static AccountClient Recording(
        Action<HttpRequestMessage> record,
        HttpStatusCode status,
        string body) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(Json) },
                },
            };
        }))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
