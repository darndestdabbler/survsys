using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/roles</c> and reader of
/// <c>/api/privileges</c>: the request each call sends, what it hands a
/// screen, and the phrase its failures start with. Every way an ask can fail
/// is <see cref="ApiAskTests"/>'s.
/// </summary>
public class RolesClientTests
{
    private const string Json = "application/json";

    private const string Investigator = """{ "id": 2, "name": "Investigator", "privileges": ["view_cases", "edit_cases"] }""";

    [Fact]
    public async Task AllAsksForTheRoles()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            $$"""{ "items": [{{Investigator}}], "page": 1, "pageSize": 1, "totalCount": 1 }""");

        var asked = await client.All();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/roles", sent?.RequestUri?.ToString());
        var role = Assert.Single(asked.Value!.Items);
        Assert.Equal("Investigator", role.Name);
        Assert.Equal([Privileges.ViewCases, Privileges.EditCases], role.Privileges);
    }

    [Fact]
    public async Task PrivilegesAsksForTheCatalogue()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, """["view_cases","manage_users"]""");

        var asked = await client.Privileges();

        Assert.Equal("http://api.example.test/api/privileges", sent?.RequestUri?.ToString());
        Assert.Equal([Privileges.ViewCases, Privileges.ManageUsers], asked.Value);
    }

    [Fact]
    public async Task CreatePostsTheRole()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
        }, HttpStatusCode.Created, Investigator);

        var asked = await client.Create(new SaveRoleRequest("Investigator", [Privileges.ViewCases, Privileges.EditCases]));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/roles", sent?.RequestUri?.ToString());
        Assert.Contains("\"edit_cases\"", body, StringComparison.Ordinal);
        Assert.Equal(2, asked.Value?.Id);
    }

    [Fact]
    public async Task UpdatePutsTheRoleToItsAddress()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Investigator);

        var asked = await client.Update(2, new SaveRoleRequest("Investigator"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/roles/2", sent?.RequestUri?.ToString());
        Assert.Equal(2, asked.Value?.Id);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(RolesClient.LoadFailurePrefix, (await client.All()).Failure);
        Assert.StartsWith(RolesClient.LoadFailurePrefix, (await client.Privileges()).Failure);
        Assert.StartsWith(RolesClient.SaveFailurePrefix, (await client.Create(new SaveRoleRequest("R"))).Failure);
        Assert.StartsWith(RolesClient.SaveFailurePrefix, (await client.Update(2, new SaveRoleRequest("R"))).Failure);
    }

    private static RolesClient Recording(
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
