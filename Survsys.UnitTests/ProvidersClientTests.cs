using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/providers</c>: the
/// request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class ProvidersClientTests
{
    private const string Json = "application/json";

    private const string Ada =
        """
        {
          "id": 7, "providerTypeId": 1175, "firstName": "Ada", "middleName": null,
          "lastName": "Lovelace", "phone": "860-555-0123", "email": "ada@example.org", "active": true
        }
        """;

    private static readonly ProviderResponse AdaResponse = new(
        7, 1175, "Ada", null, "Lovelace", "860-555-0123", "ada@example.org", true);

    [Fact]
    public async Task PageAsksForThePageAndSizeAndReturnsThePage()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            $$"""{ "items": [{{Ada}}], "page": 2, "pageSize": 25, "totalCount": 26 }""");

        var asked = await client.Page(2, 25);

        Assert.Equal("http://api.example.test/api/providers?page=2&pageSize=25", sent?.RequestUri?.ToString());
        Assert.Equal(26, asked.Value!.TotalCount);
        Assert.Equal(AdaResponse, Assert.Single(asked.Value.Items));
    }

    [Fact]
    public async Task PageAsksForTheLastNameWhereGiven()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""");

        await client.Page(1, 50, " O'Brien ");

        Assert.Equal(
            "http://api.example.test/api/providers?page=1&pageSize=50&lastName=O%27Brien",
            sent?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task FindAsksForTheProvider()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Ada);

        var asked = await client.Find(7);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/providers/7", sent?.RequestUri?.ToString());
        Assert.Equal(AdaResponse, asked.Value);
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, Ada);

        var asked = await client.Create(new SaveProviderRequest("Lovelace"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/providers", sent?.RequestUri?.ToString());
        Assert.Equal(AdaResponse, asked.Value);
    }

    [Fact]
    public async Task UpdatePutsToTheProvider()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Ada);

        var asked = await client.Update(7, new SaveProviderRequest("Lovelace"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/providers/7", sent?.RequestUri?.ToString());
        Assert.Equal(AdaResponse, asked.Value);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(ProvidersClient.PageFailurePrefix, (await client.Page(1, 50)).Failure);
        Assert.StartsWith(ProvidersClient.FindFailurePrefix, (await client.Find(7)).Failure);
        Assert.StartsWith(ProvidersClient.SaveFailurePrefix,
            (await client.Create(new SaveProviderRequest("Lovelace"))).Failure);
        Assert.StartsWith(ProvidersClient.SaveFailurePrefix,
            (await client.Update(7, new SaveProviderRequest("Lovelace"))).Failure);
        Assert.StartsWith(ProvidersClient.ExportFailurePrefix, (await client.Export()).Failure);
        Assert.StartsWith(ProvidersClient.ImportFailurePrefix, (await client.Import("id\r\n", false)).Failure);
    }

    [Fact]
    public async Task ExportAsksForTheProvidersAsCsv()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "id,last_name\r\n7,Lovelace\r\n");

        var exported = await client.Export();

        Assert.Equal("http://api.example.test/api/providers/csv", sent?.RequestUri?.ToString());
        Assert.Equal("id,last_name\r\n7,Lovelace\r\n", exported.Value);
    }

    [Theory]
    [InlineData(false, "http://api.example.test/api/providers/csv")]
    [InlineData(true, "http://api.example.test/api/providers/csv?confirm=true")]
    public async Task ImportPostsTheFileAndConfirmsOnlyWhenAsked(bool confirm, string expected)
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(
            request =>
            {
                sent = request;
                body = request.Content!.ReadAsStringAsync().Result;
            },
            HttpStatusCode.OK,
            """{ "applied": false, "added": [{ "line": 2, "key": "Lovelace" }], "changed": [], "refused": [] }""");

        var imported = await client.Import("last_name\r\nLovelace\r\n", confirm);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(expected, sent?.RequestUri?.ToString());
        Assert.Equal("last_name\r\nLovelace\r\n", body);
        Assert.Equal("Lovelace", Assert.Single(imported.Value!.Added).Key);
    }

    private static ProvidersClient Recording(
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
