using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the CDC export log: what it asks the API
/// tier for, what it hands a screen back, and the phrase each of its failures
/// starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the log is read for the page the screen asked for, that one export
/// comes back whole with the generated message, and that a log failure and an
/// export failure begin with their own prefixes and name the status rather than
/// the body.
/// </remarks>
public class ExportLogClientTests
{
    private const string Json = "application/json";

    private const string LogPage =
        """
        {
          "items": [
            {
              "id": 7,
              "recordNumber": "202600042",
              "guideId": 3,
              "queuedAt": "2026-10-17T05:00:00Z",
              "sentAt": "2026-10-17T05:05:00Z",
              "outcome": "accepted",
              "detail": "CDC accepted the message.",
              "processingId": "P",
              "queue": "production"
            },
            {
              "id": 6,
              "recordNumber": "202600041",
              "guideId": null,
              "queuedAt": "2026-10-17T04:00:00Z",
              "sentAt": null,
              "outcome": null,
              "detail": null,
              "processingId": null,
              "queue": null
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 2
        }
        """;

    private const string OneExport =
        """
        {
          "entry": {
            "id": 7,
            "recordNumber": "202600042",
            "guideId": 3,
            "queuedAt": "2026-10-17T05:00:00Z",
            "sentAt": "2026-10-17T05:05:00Z",
            "outcome": "accepted",
            "detail": "CDC accepted the message.",
            "processingId": "P",
            "queue": "production"
          },
          "generatedMessage": "MSH|^~\\&|SURVSYS|...",
          "messages": [
            { "phinVar": "TST999", "message": "No rule set formats this variable.", "severity": "Warning" }
          ]
        }
        """;

    /// <summary>
    /// Reading the log: the page the screen asked for is in the query string,
    /// nothing is posted, and the rows come back newest first with the outcome
    /// each carries.
    /// </summary>
    [Fact]
    public async Task TheLogIsReadForThePageAsked()
    {
        HttpRequestMessage? sent = null;

        var client = new ExportLogClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, LogPage);
        }));

        var log = await client.Page(page: 1, pageSize: 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/export-log?page=1&pageSize=50",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(log.Succeeded);
        Assert.Equal(2, log.Value?.TotalCount);
        Assert.Equal("202600042", log.Value?.Items[0].RecordNumber);
        Assert.Equal("accepted", log.Value?.Items[0].Outcome);
        Assert.Equal("CDC accepted the message.", log.Value?.Items[0].Detail);
        Assert.Equal("P", log.Value?.Items[0].ProcessingId);
        Assert.Equal("production", log.Value?.Items[0].Queue);
        Assert.Null(log.Value?.Items[1].Outcome);
        Assert.Null(log.Value?.Items[1].GuideId);
    }

    /// <summary>
    /// Reading one export: its own URL, and the export whole with the generated
    /// message the list leaves out.
    /// </summary>
    [Fact]
    public async Task OneExportIsReadWhole()
    {
        HttpRequestMessage? sent = null;

        var client = new ExportLogClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneExport);
        }));

        var detail = await client.Find(7);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/export-log/7",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(detail.Succeeded);
        Assert.Equal(7, detail.Value?.Entry.Id);
        Assert.Equal("accepted", detail.Value?.Entry.Outcome);
        Assert.StartsWith("MSH|", detail.Value?.GeneratedMessage);
        var message = Assert.Single(detail.Value!.Messages);
        Assert.Equal("TST999", message.PhinVar);
        Assert.Equal("Warning", message.Severity);
    }

    /// <summary>
    /// A log the API tier could not answer: the message starts with this
    /// client's log prefix — not its export one — and names the status the API
    /// gave rather than its body.
    /// </summary>
    [Fact]
    public async Task ALogThatCouldNotBeLoadedCarriesTheLogPrefix()
    {
        var client = new ExportLogClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "page 0 is not a page this endpoint can return." }""")));

        var log = await client.Page(page: 0, pageSize: 50);

        Assert.False(log.Succeeded);
        Assert.Null(log.Value);
        Assert.StartsWith(ExportLogClient.LogFailurePrefix, log.Failure);
        Assert.Contains("400", log.Failure);
        Assert.DoesNotContain("page 0", log.Failure, StringComparison.Ordinal);
        Assert.NotEqual(ExportLogClient.ExportFailurePrefix, ExportLogClient.LogFailurePrefix);
    }

    /// <summary>
    /// An export the API tier has none of: the message starts with this
    /// client's export prefix and names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task AnExportThatIsNotThereCarriesTheExportPrefix()
    {
        var client = new ExportLogClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No export has id 99." }""")));

        var detail = await client.Find(99);

        Assert.False(detail.Succeeded);
        Assert.Null(detail.Value);
        Assert.StartsWith(ExportLogClient.ExportFailurePrefix, detail.Failure);
        Assert.Contains("404", detail.Failure);
        Assert.DoesNotContain("id 99", detail.Failure, StringComparison.Ordinal);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
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
