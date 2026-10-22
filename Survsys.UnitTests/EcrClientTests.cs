using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the eCR documents: what it asks the API
/// tier for, what it hands a screen back, and the phrase each of its failures
/// starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the feed is carried in the query string only when one is chosen,
/// that one document comes back with its rendering, conditions and findings,
/// and that a queue failure and a document failure begin with their own
/// prefixes.
/// </remarks>
public class EcrClientTests
{
    private const string Json = "application/json";

    private const string QueuePage =
        """
        {
          "items": [
            {
              "id": 5,
              "kind": "RR",
              "feed": "test",
              "documentId": "18218889-5d7d-47d4-b329-31bb1c28d3b3",
              "versionNumber": 1,
              "source": "rr.xml",
              "receivedAt": "2026-10-22T12:01:00Z",
              "errorCount": 0,
              "warningCount": 18,
              "reviewCount": 2
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 1
        }
        """;

    private const string OneDocument =
        """
        {
          "document": {
            "id": 4,
            "kind": "eICR",
            "feed": "test",
            "documentId": "ca316e79-aaf2-4e8c-aa5f-848093bb8bb1",
            "versionNumber": null,
            "source": "eicr.xml",
            "receivedAt": "2026-10-22T12:00:00Z",
            "errorCount": 1,
            "warningCount": 0,
            "reviewCount": 0
          },
          "eicrHtml": "<html><body>Initial Public Health Case Report</body></html>",
          "conditions": [
            {
              "rrDocumentId": "18218889-5d7d-47d4-b329-31bb1c28d3b3",
              "conditionCode": "3928002",
              "conditionName": "Zika virus disease (disorder)",
              "determinationCode": "RRVS1",
              "determinationName": "Reportable",
              "locationCode": "RRVS7",
              "locationName": "Both patient home address and provider facility address",
              "responsibleAgencyId": "2.16.840.1.113883.4.6^12341234",
              "responsibleAgencyName": "State Department of Health",
              "reportingTimeframe": "24 h",
              "mappedConditionId": 12,
              "mappedConditionName": "Zika virus",
              "routing": "reportable"
            }
          ],
          "findings": [
            {
              "documentKind": "eICR",
              "documentId": "ca316e79-aaf2-4e8c-aa5f-848093bb8bb1",
              "source": "schema",
              "severity": "error",
              "message": "The element 'ClinicalDocument' has incomplete content.",
              "location": "line 3, position 2"
            }
          ]
        }
        """;

    [Fact]
    public async Task EveryFeedIsReadForThePageAsked()
    {
        HttpRequestMessage? sent = null;
        var client = new EcrClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        var queue = await client.List(feed: null, review: false, page: 1, pageSize: 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/ecr-documents?page=1&pageSize=50", sent?.RequestUri?.ToString());
        Assert.True(queue.Succeeded);
        var row = Assert.Single(queue.Value!.Items);
        Assert.Equal("RR", row.Kind);
        Assert.Equal(18, row.WarningCount);
        Assert.Equal(2, row.ReviewCount);
    }

    [Fact]
    public async Task TheFeedAndPageAreCarriedWhenAFeedIsChosen()
    {
        HttpRequestMessage? sent = null;
        var client = new EcrClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        await client.List(feed: "production", review: false, page: 2, pageSize: 50);

        Assert.Equal(
            "http://api.example.test/api/ecr-documents?page=2&pageSize=50&feed=production",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task TheReviewFlagIsCarriedWhenAsked()
    {
        HttpRequestMessage? sent = null;
        var client = new EcrClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        await client.List(feed: "test", review: true, page: 1, pageSize: 50);

        Assert.Equal(
            "http://api.example.test/api/ecr-documents?page=1&pageSize=50&feed=test&review=true",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task OneDocumentIsReadWithItsRenderingConditionsAndFindings()
    {
        HttpRequestMessage? sent = null;
        var client = new EcrClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneDocument);
        }));

        var detail = await client.Get(4);

        Assert.Equal("http://api.example.test/api/ecr-documents/4", sent?.RequestUri?.ToString());
        Assert.True(detail.Succeeded);
        Assert.Equal(4, detail.Value!.Document.Id);
        Assert.Contains("Initial Public Health Case Report", detail.Value.EicrHtml);
        var condition = Assert.Single(detail.Value.Conditions);
        Assert.Equal("RRVS1", condition.DeterminationCode);
        Assert.Equal("Zika virus", condition.MappedConditionName);
        Assert.Equal("reportable", condition.Routing);
        Assert.Equal("error", Assert.Single(detail.Value.Findings).Severity);
    }

    [Fact]
    public async Task AQueueFailureAndADocumentFailureSayWhichFailed()
    {
        var client = new EcrClient(Transport(_ =>
            Answer(HttpStatusCode.InternalServerError, "{}")));

        var queue = await client.List(null, false, 1, 50);
        var document = await client.Get(4);

        Assert.StartsWith(EcrClient.QueueFailurePrefix, queue.Failure);
        Assert.StartsWith(EcrClient.DocumentFailurePrefix, document.Failure);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(Json) },
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
