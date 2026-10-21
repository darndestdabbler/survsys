using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the notification queue: what it asks the
/// API tier for, what it hands a screen back, and the phrase each of its
/// failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the queue is read for the page the screen asked for, that a hold is
/// patched to the case's own URL with the bit in the body, and that a queue
/// failure and a hold failure begin with their own prefixes and name the status
/// rather than the body.
/// </remarks>
public class NotificationQueueClientTests
{
    private const string Json = "application/json";

    private const string QueuePage =
        """
        {
          "items": [
            {
              "personConditionId": 42,
              "recordNumber": "202600042",
              "patientName": "Rodriguez, Maria",
              "condition": "Malaria",
              "queuedAt": "2026-10-17T03:30:00Z",
              "hold": false
            },
            {
              "personConditionId": 43,
              "recordNumber": "202600043",
              "patientName": "Chen, Li",
              "condition": "Measles",
              "queuedAt": "2026-10-17T03:31:00Z",
              "hold": true
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 2
        }
        """;

    private const string HeldEntry =
        """
        {
          "personConditionId": 42,
          "recordNumber": "202600042",
          "patientName": "Rodriguez, Maria",
          "condition": "Malaria",
          "queuedAt": "2026-10-17T03:30:00Z",
          "hold": true
        }
        """;

    private const string QueuedEntry =
        """
        {
          "personConditionId": 42,
          "recordNumber": "202600042",
          "patientName": "Rodriguez, Maria",
          "condition": "Malaria",
          "queuedAt": "2026-10-17T03:30:00Z",
          "hold": false
        }
        """;

    /// <summary>
    /// Reading the queue: the page the screen asked for is in the query string,
    /// nothing is posted, and the rows come back with the case joined and the
    /// hold each carries.
    /// </summary>
    [Fact]
    public async Task TheQueueIsReadForThePageAsked()
    {
        HttpRequestMessage? sent = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        var queue = await client.Page(page: 1, pageSize: 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/notification-queue?page=1&pageSize=50",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(queue.Succeeded);
        Assert.Equal(2, queue.Value?.TotalCount);
        Assert.Equal("202600042", queue.Value?.Items[0].RecordNumber);
        Assert.Equal("Rodriguez, Maria", queue.Value?.Items[0].PatientName);
        Assert.Equal("Malaria", queue.Value?.Items[0].Condition);
        Assert.False(queue.Value?.Items[0].Hold);
        Assert.True(queue.Value?.Items[1].Hold);
    }

    /// <summary>
    /// Raising a hold: a PATCH to the case's own hold URL carrying the bit, and
    /// the entry comes back in its new state.
    /// </summary>
    [Fact]
    public async Task AHoldIsPatchedToTheCasesUrlWithTheBit()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, HeldEntry);
        }));

        var held = await client.SetHold(42, hold: true);

        Assert.Equal(HttpMethod.Patch, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/notification-queue/42/hold",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"hold\":true", body, StringComparison.Ordinal);

        Assert.True(held.Succeeded);
        Assert.Equal(42, held.Value?.PersonConditionId);
        Assert.True(held.Value?.Hold);
    }

    /// <summary>
    /// Queueing a case by hand: a POST to the case's own notification-queue
    /// URL, and the queued entry comes back.
    /// </summary>
    [Fact]
    public async Task ACaseIsQueuedByPostingToItsNotificationQueueUrl()
    {
        HttpRequestMessage? sent = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuedEntry);
        }));

        var queued = await client.Queue(42);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/42/notification-queue",
            sent?.RequestUri?.ToString());

        Assert.True(queued.Succeeded);
        Assert.Equal(42, queued.Value?.PersonConditionId);
        Assert.False(queued.Value?.Hold);
    }

    /// <summary>
    /// Processing the queue now: a POST to the queue's process URL, and the
    /// count processed comes back; a failure carries the processing prefix.
    /// </summary>
    [Fact]
    public async Task ProcessNowPostsToTheProcessUrl()
    {
        HttpRequestMessage? sent = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, """{ "processed": 3 }""");
        }));

        var done = await client.ProcessNow();

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/notification-queue/process",
            sent?.RequestUri?.ToString());
        Assert.True(done.Succeeded);
        Assert.Equal(3, done.Value?.Processed);

        var failing = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.Forbidden, """{ "status": 403 }""")));

        var refused = await failing.ProcessNow();

        Assert.False(refused.Succeeded);
        Assert.StartsWith(NotificationQueueClient.ProcessingFailurePrefix, refused.Failure);
        Assert.Contains("403", refused.Failure);
    }

    /// <summary>
    /// A case the API tier has no case for, answered 404: the message starts
    /// with this client's queueing prefix — not its queue or hold one — and
    /// names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task QueueingACaseThatIsNotThereCarriesTheQueueingPrefix()
    {
        var client = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case has id 99." }""")));

        var queued = await client.Queue(99);

        Assert.False(queued.Succeeded);
        Assert.Null(queued.Value);
        Assert.StartsWith(NotificationQueueClient.QueueingFailurePrefix, queued.Failure);
        Assert.Contains("404", queued.Failure);
        Assert.DoesNotContain("id 99", queued.Failure, StringComparison.Ordinal);
        Assert.NotEqual(NotificationQueueClient.QueueFailurePrefix, NotificationQueueClient.QueueingFailurePrefix);
        Assert.NotEqual(NotificationQueueClient.HoldFailurePrefix, NotificationQueueClient.QueueingFailurePrefix);
    }

    /// <summary>
    /// A queue the API tier could not answer: the message starts with this
    /// client's queue prefix — not its hold one — and names the status the API
    /// gave rather than its body.
    /// </summary>
    [Fact]
    public async Task AQueueThatCouldNotBeLoadedCarriesTheQueuePrefix()
    {
        var client = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "page 0 is not a page this endpoint can return." }""")));

        var queue = await client.Page(page: 0, pageSize: 50);

        Assert.False(queue.Succeeded);
        Assert.Null(queue.Value);
        Assert.StartsWith(NotificationQueueClient.QueueFailurePrefix, queue.Failure);
        Assert.Contains("400", queue.Failure);
        Assert.DoesNotContain("page 0", queue.Failure, StringComparison.Ordinal);
        Assert.NotEqual(NotificationQueueClient.HoldFailurePrefix, NotificationQueueClient.QueueFailurePrefix);
    }

    /// <summary>
    /// A case the API tier has no entry for, answered 404: the message starts
    /// with this client's hold prefix and names the status rather than the
    /// body.
    /// </summary>
    [Fact]
    public async Task AHoldOnACaseNotInTheQueueCarriesTheHoldPrefix()
    {
        var client = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case with id 99 is in the notification queue." }""")));

        var held = await client.SetHold(99, hold: true);

        Assert.False(held.Succeeded);
        Assert.Null(held.Value);
        Assert.StartsWith(NotificationQueueClient.HoldFailurePrefix, held.Failure);
        Assert.Contains("404", held.Failure);
        Assert.DoesNotContain("id 99", held.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// Removing a case: a DELETE to the case's queue URL, answered with no
    /// body; a failure carries the removal prefix and names the status.
    /// </summary>
    [Fact]
    public async Task RemoveDeletesTheCasesQueueEntry()
    {
        HttpRequestMessage? sent = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        Assert.Null(await client.Remove(42));
        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/notification-queue/42", sent?.RequestUri?.ToString());

        var failing = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.NotFound, """{ "status": 404, "detail": "No case with id 42 is queued." }""")));

        var refused = await failing.Remove(42);

        Assert.StartsWith(NotificationQueueClient.RemovalFailurePrefix, refused);
        Assert.Contains("404", refused);
    }

    /// <summary>
    /// Exporting records: a POST of the record numbers to the records URL, and
    /// the count queued and the unknown numbers come back.
    /// </summary>
    [Fact]
    public async Task ExportRecordsPostsTheRecordNumbers()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, """{ "queued": 1, "unknown": ["NOPE-1"] }""");
        }));

        var done = await client.ExportRecords(["202600042", "NOPE-1"]);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/notification-queue/records", sent?.RequestUri?.ToString());
        Assert.Contains("\"recordNumbers\":[\"202600042\",\"NOPE-1\"]", body, StringComparison.Ordinal);
        Assert.Equal(1, done.Value?.Queued);
        Assert.Equal(["NOPE-1"], done.Value?.Unknown);
    }

    /// <summary>
    /// Exporting an MMWR range: a POST of the range to the range URL; a failure
    /// carries the export prefix and names the status.
    /// </summary>
    [Fact]
    public async Task ExportMmwrRangePostsTheRange()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new NotificationQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, """{ "queued": 3, "unknown": [] }""");
        }));

        var done = await client.ExportMmwrRange(new ExportMmwrRangeRequest(2026, 9, 12, 7));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/notification-queue/mmwr-range", sent?.RequestUri?.ToString());
        Assert.Contains("\"mmwrYear\":2026", body, StringComparison.Ordinal);
        Assert.Contains("\"conditionId\":7", body, StringComparison.Ordinal);
        Assert.Equal(3, done.Value?.Queued);

        var failing = new NotificationQueueClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest, """{ "status": 400, "detail": "Weeks 12 to 9 are not an MMWR range." }""")));

        var refused = await failing.ExportMmwrRange(new ExportMmwrRangeRequest(2026, 12, 9));

        Assert.StartsWith(NotificationQueueClient.ExportFailurePrefix, refused.Failure);
        Assert.Contains("400", refused.Failure);
        Assert.DoesNotContain("Weeks 12", refused.Failure, StringComparison.Ordinal);
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
