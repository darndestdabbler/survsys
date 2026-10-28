using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/ecr-condition-mappings</c>:
/// the URLs and methods it asks with, the mapping it hands the screen, and the
/// phrases its failures start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </remarks>
public class EcrConditionMappingsClientTests
{
    private const string Json = "application/json";

    private const string OneMapping =
        """
        { "id": 3, "codeSystem": "2.16.840.1.113883.6.96", "code": "3928002",
          "displayName": "Zika virus disease (disorder)", "conditionId": 12, "conditionName": "Zika virus" }
        """;

    [Fact]
    public async Task ListAsksForTheMapping()
    {
        Uri? asked = null;
        var client = new EcrConditionMappingsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, $"[{OneMapping}]");
        }));

        var mappings = await client.List();

        Assert.Equal("http://api.example.test/api/ecr-condition-mappings", asked?.ToString());
        var mapping = Assert.Single(mappings.Value!);
        Assert.Equal("3928002", mapping.Code);
        Assert.Equal("Zika virus", mapping.ConditionName);
    }

    [Fact]
    public async Task AddPostsAndSavePutsTheMapping()
    {
        var sent = new List<(HttpMethod Method, string? Uri, SaveEcrConditionMappingRequest? Body)>();
        var client = new EcrConditionMappingsClient(Transport(request =>
        {
            sent.Add((
                request.Method,
                request.RequestUri?.ToString(),
                request.Content!.ReadFromJsonAsync<SaveEcrConditionMappingRequest>().Result));
            return Answer(HttpStatusCode.OK, OneMapping);
        }));
        var request = new SaveEcrConditionMappingRequest("2.16.840.1.113883.6.96", "3928002", null, 12);

        var added = await client.Add(request);
        await client.Save(3, request);

        Assert.Equal(12, added.Value!.ConditionId);
        Assert.Equal(
            [
                (HttpMethod.Post, "http://api.example.test/api/ecr-condition-mappings", request),
                (HttpMethod.Put, "http://api.example.test/api/ecr-condition-mappings/3", request),
            ],
            sent);
    }

    [Fact]
    public async Task DeleteDeletesTheMapping()
    {
        HttpRequestMessage? sent = null;
        var client = new EcrConditionMappingsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.NoContent, "");
        }));

        var why = await client.Delete(3);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/ecr-condition-mappings/3", sent?.RequestUri?.ToString());
        Assert.Null(why);
    }

    [Fact]
    public async Task ExportAsksForTheMappingAsCsv()
    {
        Uri? asked = null;
        var client = new EcrConditionMappingsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, "code_system,code,display_name,condition\r\n", "text/csv");
        }));

        var exported = await client.Export();

        Assert.Equal("http://api.example.test/api/ecr-condition-mappings/csv", asked?.ToString());
        Assert.StartsWith("code_system,code", exported.Value);
    }

    [Theory]
    [InlineData(false, "http://api.example.test/api/ecr-condition-mappings/csv")]
    [InlineData(true, "http://api.example.test/api/ecr-condition-mappings/csv?confirm=true")]
    public async Task ImportPostsTheFileAndConfirmsOnlyWhenAsked(bool confirm, string expected)
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = new EcrConditionMappingsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(
                HttpStatusCode.OK,
                """{ "applied": false, "added": [{ "line": 2, "key": "1.2|3" }], "changed": [], "refused": [] }""");
        }));

        var imported = await client.Import("code_system,code\r\n1.2,3\r\n", confirm);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(expected, sent?.RequestUri?.ToString());
        Assert.Equal("code_system,code\r\n1.2,3\r\n", body);
        Assert.Equal("1.2|3", Assert.Single(imported.Value!.Added).Key);
    }

    [Fact]
    public async Task EachFailureCarriesItsPrefix()
    {
        var client = new EcrConditionMappingsClient(Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        Assert.StartsWith(EcrConditionMappingsClient.ListFailurePrefix, (await client.List()).Failure);
        Assert.StartsWith(
            EcrConditionMappingsClient.SaveFailurePrefix,
            (await client.Add(new SaveEcrConditionMappingRequest("1.2", "3"))).Failure);
        Assert.StartsWith(EcrConditionMappingsClient.SaveFailurePrefix, await client.Delete(3));
        Assert.StartsWith(EcrConditionMappingsClient.ExportFailurePrefix, (await client.Export()).Failure);
        Assert.StartsWith(
            EcrConditionMappingsClient.ImportFailurePrefix, (await client.Import("x", confirm: false)).Failure);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(HttpStatusCode status, string body, string mediaType = Json) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(mediaType) },
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
