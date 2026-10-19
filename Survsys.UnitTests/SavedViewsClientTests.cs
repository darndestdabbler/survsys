using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/saved-views</c>: the
/// request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way a JSON ask can fail is
/// <see cref="ApiAskTests"/>'s.
/// </summary>
public class SavedViewsClientTests
{
    private const string Json = "application/json";

    private const string OpenSalmonella = """
        { "id": 4, "name": "Open salmonella", "columns": ["record_number", "condition"],
          "conditionId": 3, "agencyId": null, "workflowState": "accepted", "caseStatus": null,
          "onsetFrom": "2026-01-01", "onsetTo": null }
        """;

    private static readonly SaveSavedViewRequest Request =
        new("Open salmonella", [ExportColumns.RecordNumber, ExportColumns.Condition], ConditionId: 3);

    [Fact]
    public async Task AllAsksForTheSavedViews()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            $$"""{ "items": [{{OpenSalmonella}}], "page": 1, "pageSize": 1, "totalCount": 1 }""");

        var asked = await client.All();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/saved-views", sent?.RequestUri?.ToString());
        var view = Assert.Single(asked.Value!.Items);
        Assert.Equal("Open salmonella", view.Name);
        Assert.Equal([ExportColumns.RecordNumber, ExportColumns.Condition], view.Columns);
        Assert.Equal(new DateOnly(2026, 1, 1), view.OnsetFrom);
    }

    [Fact]
    public async Task CreatePostsTheView()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
        }, HttpStatusCode.Created, OpenSalmonella);

        var asked = await client.Create(Request);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/saved-views", sent?.RequestUri?.ToString());
        Assert.Contains("\"record_number\"", body, StringComparison.Ordinal);
        Assert.Equal(4, asked.Value?.Id);
    }

    [Fact]
    public async Task UpdatePutsTheViewToItsAddress()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, OpenSalmonella);

        var asked = await client.Update(4, Request);

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/saved-views/4", sent?.RequestUri?.ToString());
        Assert.Equal(4, asked.Value?.Id);
    }

    [Fact]
    public async Task DeleteDeletesTheViewAtItsAddress()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.NoContent, "");

        var failure = await client.Delete(4);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/saved-views/4", sent?.RequestUri?.ToString());
        Assert.Null(failure);
    }

    [Fact]
    public async Task ExportAsksForTheViewsCsvAndHandsItOnAsText()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "record_number\r\n2026-0001\r\n", "text/csv");

        var asked = await client.Export(4);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/saved-views/4/export", sent?.RequestUri?.ToString());
        Assert.Equal("record_number\r\n2026-0001\r\n", asked.Value);
    }

    [Fact]
    public async Task ExportOfAViewThatIsNotTheCallersIsAFailure()
    {
        var client = Recording(_ => { }, HttpStatusCode.NotFound, "{}");

        var asked = await client.Export(4);

        Assert.Null(asked.Value);
        Assert.StartsWith(SavedViewsClient.ExportFailurePrefix, asked.Failure);
        Assert.Contains("404", asked.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExportWhenTheApiTierDoesNotAnswerIsAFailure()
    {
        var client = new SavedViewsClient(new HttpClient(new StubTransport(_ => throw new HttpRequestException()))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        Assert.StartsWith(SavedViewsClient.ExportFailurePrefix, (await client.Export(4)).Failure);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(SavedViewsClient.LoadFailurePrefix, (await client.All()).Failure);
        Assert.StartsWith(SavedViewsClient.SaveFailurePrefix, (await client.Create(Request)).Failure);
        Assert.StartsWith(SavedViewsClient.SaveFailurePrefix, (await client.Update(4, Request)).Failure);
        Assert.StartsWith(SavedViewsClient.SaveFailurePrefix, await client.Delete(4));
        Assert.StartsWith(SavedViewsClient.ExportFailurePrefix, (await client.Export(4)).Failure);
    }

    private static SavedViewsClient Recording(
        Action<HttpRequestMessage> record,
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(mediaType) },
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
