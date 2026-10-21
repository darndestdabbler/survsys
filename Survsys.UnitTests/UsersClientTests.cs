using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/users</c>: the request
/// each call sends, what it hands a screen, and the phrase its failures start
/// with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class UsersClientTests
{
    private const string Json = "application/json";

    private const string Ada =
        """{ "id": 7, "userName": "ada", "displayName": "Ada Lovelace", "active": true, "roleIds": [1], "agencyIds": [4] }""";

    private static readonly UserResponse AdaResponse = new(7, "ada", "Ada Lovelace", true, [1], [4]);

    [Fact]
    public async Task AllAsksForTheUsers()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            $$"""{ "items": [{{Ada}}], "page": 1, "pageSize": 1, "totalCount": 1 }""");

        var asked = await client.All();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/users", sent?.RequestUri?.ToString());
        var user = Assert.Single(asked.Value!.Items);
        Assert.Equal(AdaResponse.UserName, user.UserName);
        Assert.Equal(AdaResponse.RoleIds, user.RoleIds);
        Assert.Equal(AdaResponse.AgencyIds, user.AgencyIds);
    }

    [Fact]
    public async Task CreatePostsTheUser()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
        }, HttpStatusCode.Created, Ada);

        var asked = await client.Create(new SaveUserRequest("ada", "Ada Lovelace", true, [1], "pw"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/users", sent?.RequestUri?.ToString());
        Assert.Contains("\"password\":\"pw\"", body, StringComparison.Ordinal);
        Assert.Equal(7, asked.Value?.Id);
    }

    [Fact]
    public async Task UpdatePutsTheUserToTheirAddress()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Ada);

        var asked = await client.Update(7, new SaveUserRequest("ada", "Ada Lovelace", true, [1]));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/users/7", sent?.RequestUri?.ToString());
        Assert.Equal(7, asked.Value?.Id);
    }

    [Fact]
    public async Task InvestigatorsAsksForTheActiveUsersAnInvestigationMayBeAssignedTo()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            """[{ "id": 7, "displayName": "Ada Lovelace" }]""");

        var asked = await client.Investigators();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/investigators", sent?.RequestUri?.ToString());
        Assert.Equal(new InvestigatorResponse(7, "Ada Lovelace"), Assert.Single(asked.Value!));
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(UsersClient.LoadFailurePrefix, (await client.All()).Failure);
        Assert.StartsWith(UsersClient.SaveFailurePrefix, (await client.Create(new SaveUserRequest("a", "A"))).Failure);
        Assert.StartsWith(UsersClient.SaveFailurePrefix, (await client.Update(7, new SaveUserRequest("a", "A"))).Failure);
        Assert.StartsWith(UsersClient.InvestigatorsFailurePrefix, (await client.Investigators()).Failure);
    }

    private static UsersClient Recording(
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
