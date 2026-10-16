using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the work queue and sharing:
/// what it sends, what it hands a screen back, and the phrase each of its
/// failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the queue is read for the agency and page the screen asked for, that
/// a share is posted to the report's own shares URL with the agency the screen
/// chose, and that the agencies a report is shared with come back as a list.
/// </remarks>
public class WorkQueueClientTests
{
    private const string Json = "application/json";

    private const string QueuePage =
        """
        {
          "items": [
            {
              "id": 11,
              "recordNumber": "202600001",
              "personId": 13,
              "conditionId": 74,
              "agencyId": 1,
              "workflowState": "accepted",
              "caseStatus": "C",
              "eventOnsetDate": "2026-03-12",
              "shared": false
            },
            {
              "id": 12,
              "recordNumber": "202600002",
              "personId": 14,
              "conditionId": 75,
              "agencyId": 2,
              "workflowState": "assigned_to_lhd",
              "caseStatus": null,
              "eventOnsetDate": null,
              "shared": true
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 2
        }
        """;

    private const string SharedWith =
        """
        {
          "items": [
            { "id": 3, "name": "Davis County" },
            { "id": 9, "name": "Summit County" }
          ],
          "page": 1,
          "pageSize": 2,
          "totalCount": 2
        }
        """;

    private const string SharedAgency = """{ "id": 3, "name": "Davis County" }""";

    /// <summary>
    /// Reading a queue: the agency and the page the screen asked for are carried
    /// in the query string, and the reports come back with the shared-in flag
    /// each row carries.
    /// </summary>
    [Fact]
    public async Task TheQueueIsReadForTheAgencyAndPageAsked()
    {
        HttpRequestMessage? sent = null;

        var client = new WorkQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        var queue = await client.Queue(agencyId: 1, page: 1, pageSize: 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/work-queue?agencyId=1&page=1&pageSize=50",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(queue.Succeeded);
        Assert.Equal(2, queue.Value?.TotalCount);
        Assert.Equal("202600001", queue.Value?.Items[0].RecordNumber);
        Assert.False(queue.Value?.Items[0].Shared);
        Assert.True(queue.Value?.Items[1].Shared);
        Assert.Equal("accepted", queue.Value?.Items[0].WorkflowState);
    }

    /// <summary>
    /// The page the panel asks for is the page the client fetches: a queue
    /// longer than one page is reached by asking for the next, so the client
    /// carries the page and the size it is given rather than a fixed first page.
    /// This is the boundary the paged work-queue screens stand on.
    /// </summary>
    [Fact]
    public async Task TheQueueCarriesWhicheverPageIsAsked()
    {
        HttpRequestMessage? sent = null;

        var client = new WorkQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        await client.Queue(agencyId: 5, page: 3, pageSize: 50);

        Assert.Equal(
            "http://api.example.test/api/work-queue?agencyId=5&page=3&pageSize=50",
            sent?.RequestUri?.ToString());
    }

    /// <summary>
    /// Sharing: the agency the screen chose is the body posted to the report's
    /// own shares URL, and the agency it was shared with comes back.
    /// </summary>
    [Fact]
    public async Task TheShareIsPostedAndTheSharedAgencyComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new WorkQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, SharedAgency);
        }));

        var shared = await client.Share(11, new ShareReportRequest(3));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/shares",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"agencyId\":3", body, StringComparison.Ordinal);

        Assert.True(shared.Succeeded);
        Assert.Equal(3, shared.Value?.Id);
        Assert.Equal("Davis County", shared.Value?.Name);
    }

    /// <summary>
    /// Listing shares: the report's own shares URL, and the agencies it is
    /// shared with back.
    /// </summary>
    [Fact]
    public async Task TheAgenciesSharedWithComeBack()
    {
        HttpRequestMessage? sent = null;

        var client = new WorkQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, SharedWith);
        }));

        var shares = await client.Shares(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/shares",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(shares.Succeeded);
        Assert.Equal(2, shares.Value?.TotalCount);
        Assert.Equal("Davis County", shares.Value?.Items[0].Name);
        Assert.Equal("Summit County", shares.Value?.Items[1].Name);
    }

    /// <summary>
    /// A share the API tier refused: the message starts with this client's
    /// share prefix — not its queue or its shares-list one — and names the
    /// status the API gave rather than its body.
    /// </summary>
    [Fact]
    public async Task AShareThatCouldNotBeMadeCarriesTheSharePrefix()
    {
        var client = new WorkQueueClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "This report is already shared with agencyId 3." }""")));

        var shared = await client.Share(11, new ShareReportRequest(3));

        Assert.False(shared.Succeeded);
        Assert.Null(shared.Value);
        Assert.StartsWith(WorkQueueClient.ShareFailurePrefix, shared.Failure);
        Assert.Contains("400", shared.Failure);
        Assert.DoesNotContain("already shared", shared.Failure, StringComparison.Ordinal);
        Assert.NotEqual(WorkQueueClient.QueueFailurePrefix, WorkQueueClient.ShareFailurePrefix);
        Assert.NotEqual(WorkQueueClient.SharesFailurePrefix, WorkQueueClient.ShareFailurePrefix);
    }

    /// <summary>
    /// A queue the API tier could not answer: the message starts with this
    /// client's queue prefix and names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task AQueueThatCouldNotBeLoadedCarriesTheQueuePrefix()
    {
        var client = new WorkQueueClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "agencyId 99 is not an agency this tier knows." }""")));

        var queue = await client.Queue(agencyId: 99, page: 1, pageSize: 50);

        Assert.False(queue.Succeeded);
        Assert.Null(queue.Value);
        Assert.StartsWith(WorkQueueClient.QueueFailurePrefix, queue.Failure);
        Assert.Contains("400", queue.Failure);
        Assert.DoesNotContain("99", queue.Failure, StringComparison.Ordinal);
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
