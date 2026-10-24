using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the audits: what it asks the API tier for
/// and the phrase each of its failures starts with.
/// </summary>
public class AuditClientTests
{
    private const string Empty = """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""";

    [Fact]
    public async Task ChangesAreReadForTheTablePageAndKeyAsked()
    {
        HttpRequestMessage? sent = null;
        var client = new AuditClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Empty);
        }));

        var changes = await client.Changes("person_conditions", "42", page: 2, pageSize: 50);

        Assert.True(changes.Succeeded);
        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/audit?table=person_conditions&page=2&pageSize=50&key=42",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ChangesWithoutAKeyAskForTheWholeTable()
    {
        HttpRequestMessage? sent = null;
        var client = new AuditClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Empty);
        }));

        await client.Changes("treatments", " ", page: 1, pageSize: 50);

        Assert.Equal(
            "http://api.example.test/api/audit?table=treatments&page=1&pageSize=50",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task TheAccessAuditIsReadForThePageAsked()
    {
        HttpRequestMessage? sent = null;
        var client = new AuditClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Empty);
        }));

        var access = await client.Access(page: 3, pageSize: 50);

        Assert.True(access.Succeeded);
        Assert.Equal(
            "http://api.example.test/api/access-audit?page=3&pageSize=50",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task EachAuditThatCouldNotBeLoadedCarriesItsOwnPrefix()
    {
        var client = new AuditClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest, """{ "status": 400, "detail": "no table" }""")));

        var changes = await client.Changes("x", null, 1, 50);
        var access = await client.Access(1, 50);

        Assert.StartsWith(AuditClient.ChangesFailurePrefix, changes.Failure);
        Assert.Contains("400", changes.Failure);
        Assert.StartsWith(AuditClient.AccessFailurePrefix, access.Failure);
        Assert.DoesNotContain("no table", changes.Failure, StringComparison.Ordinal);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
            },
        };

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
