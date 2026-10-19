using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the staged-message queue: what it asks the
/// API tier for, what it hands a screen back, and the phrase each of its
/// failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the queue is read for the page the screen asked for, that the review
/// state is carried in the query string only when one is chosen, that one
/// message comes back whole with the raw text, and that a queue failure and a
/// message failure begin with their own prefixes.
/// </remarks>
public class MessageQueueClientTests
{
    private const string Json = "application/json";

    private const string QueuePage =
        """
        {
          "items": [
            {
              "id": 11,
              "source": "oru-r01-chlamydia.hl7",
              "receivedAt": "2026-10-17T03:30:00Z",
              "reviewStatus": "entry",
              "qaFlagged": false,
              "exceptionDetail": null
            },
            {
              "id": 12,
              "source": "broken.hl7",
              "receivedAt": "2026-10-17T03:31:00Z",
              "reviewStatus": "exception",
              "qaFlagged": true,
              "exceptionDetail": "MSH segment missing."
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 2
        }
        """;

    private const string OneMessage =
        """
        {
          "message": {
            "id": 11,
            "source": "oru-r01-chlamydia.hl7",
            "receivedAt": "2026-10-17T03:30:00Z",
            "reviewStatus": "entry",
            "qaFlagged": false,
            "exceptionDetail": null
          },
          "rawMessage": "MSH|^~\\&|LAB|..."
        }
        """;

    /// <summary>
    /// Reading the whole queue: the page the screen asked for is in the query
    /// string, no status is sent when none is chosen, and the rows come back
    /// with the review state and QA flag each carries.
    /// </summary>
    [Fact]
    public async Task TheWholeQueueIsReadForThePageAsked()
    {
        HttpRequestMessage? sent = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        var queue = await client.Page(status: null, page: 1, pageSize: 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/staged-messages?page=1&pageSize=50",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(queue.Succeeded);
        Assert.Equal(2, queue.Value?.TotalCount);
        Assert.Equal("entry", queue.Value?.Items[0].ReviewStatus);
        Assert.False(queue.Value?.Items[0].QaFlagged);
        Assert.Equal("exception", queue.Value?.Items[1].ReviewStatus);
        Assert.Equal("MSH segment missing.", queue.Value?.Items[1].ExceptionDetail);
    }

    /// <summary>
    /// Filtering by review state: the chosen status and whichever page is asked
    /// for are both carried in the query string, so a queue longer than one
    /// page and narrowed to one state is reached by asking for both.
    /// </summary>
    [Fact]
    public async Task TheReviewStateAndPageAreCarriedWhenAStatusIsChosen()
    {
        HttpRequestMessage? sent = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, QueuePage);
        }));

        await client.Page(status: "entry", page: 2, pageSize: 50);

        Assert.Equal(
            "http://api.example.test/api/staged-messages?status=entry&page=2&pageSize=50",
            sent?.RequestUri?.ToString());
    }

    /// <summary>
    /// Reading one message: its own URL, and the message whole with the raw
    /// text the list leaves out.
    /// </summary>
    [Fact]
    public async Task OneMessageIsReadWhole()
    {
        HttpRequestMessage? sent = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneMessage);
        }));

        var detail = await client.Find(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/staged-messages/11",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(detail.Succeeded);
        Assert.Equal(11, detail.Value?.Message.Id);
        Assert.Equal("entry", detail.Value?.Message.ReviewStatus);
        Assert.StartsWith("MSH|", detail.Value?.RawMessage);
    }

    private const string Promoted =
        """
        {
          "report": {
            "id": 42,
            "personId": 13,
            "conditionId": 74,
            "recordNumber": "202600042",
            "agencyId": 1,
            "firstReportedPhDate": "2026-10-15",
            "eventOnsetDate": "2026-10-17",
            "createdAt": "2026-10-17T04:00:00Z",
            "caseStatus": null,
            "mmwrWeek": 42,
            "mmwrYear": 2026,
            "workflowState": "assigned_to_lhd"
          },
          "notification": "Case 202600042 was created for Chlamydia; a notification has been raised to public health."
        }
        """;

    /// <summary>
    /// Promoting a message: the reviewer's decision is the body posted to the
    /// message's own case URL, and the created report and the notification the
    /// promotion raised come back.
    /// </summary>
    [Fact]
    public async Task TheDecisionIsPostedAndTheCaseAndNotificationComeBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, Promoted);
        }));

        var promoted = await client.Promote(
            11, new PromoteMessageRequest(PersonId: 13, ConditionId: 74, AgencyId: 1));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/staged-messages/11/case",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"personId\":13", body, StringComparison.Ordinal);
        Assert.Contains("\"conditionId\":74", body, StringComparison.Ordinal);

        Assert.True(promoted.Succeeded);
        Assert.Equal("202600042", promoted.Value?.Report.RecordNumber);
        Assert.Equal(42, promoted.Value?.Report.Id);
        Assert.Contains("a notification has been raised", promoted.Value?.Notification);
    }

    /// <summary>
    /// A message the API tier would not promote — one whose state refuses it,
    /// answered 409: the message starts with this client's promote prefix, not
    /// its queue or message one, and names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task APromotionThatWasRefusedCarriesThePromotePrefix()
    {
        var client = new MessageQueueClient(Transport(_ => Answer(
            HttpStatusCode.Conflict,
            """{ "status": 409, "detail": "This message has already been promoted to a case." }""")));

        var promoted = await client.Promote(
            11, new PromoteMessageRequest(PersonId: 13, ConditionId: 74));

        Assert.False(promoted.Succeeded);
        Assert.Null(promoted.Value);
        Assert.StartsWith(MessageQueueClient.PromoteFailurePrefix, promoted.Failure);
        Assert.Contains("409", promoted.Failure);
        Assert.DoesNotContain("already been promoted", promoted.Failure, StringComparison.Ordinal);
        Assert.NotEqual(MessageQueueClient.QueueFailurePrefix, MessageQueueClient.PromoteFailurePrefix);
        Assert.NotEqual(MessageQueueClient.MessageFailurePrefix, MessageQueueClient.PromoteFailurePrefix);
    }

    /// <summary>
    /// A queue the API tier could not answer: the message starts with this
    /// client's queue prefix — not its message one — and names the status the
    /// API gave rather than its body.
    /// </summary>
    [Fact]
    public async Task AQueueThatCouldNotBeLoadedCarriesTheQueuePrefix()
    {
        var client = new MessageQueueClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "page 0 is not a page this endpoint can return." }""")));

        var queue = await client.Page(status: null, page: 0, pageSize: 50);

        Assert.False(queue.Succeeded);
        Assert.Null(queue.Value);
        Assert.StartsWith(MessageQueueClient.QueueFailurePrefix, queue.Failure);
        Assert.Contains("400", queue.Failure);
        Assert.DoesNotContain("page 0", queue.Failure, StringComparison.Ordinal);
        Assert.NotEqual(MessageQueueClient.MessageFailurePrefix, MessageQueueClient.QueueFailurePrefix);
    }

    /// <summary>
    /// A message the API tier has none of: the message starts with this
    /// client's message prefix and names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task AMessageThatIsNotThereCarriesTheMessagePrefix()
    {
        var client = new MessageQueueClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No staged message has id 99." }""")));

        var detail = await client.Find(99);

        Assert.False(detail.Succeeded);
        Assert.Null(detail.Value);
        Assert.StartsWith(MessageQueueClient.MessageFailurePrefix, detail.Failure);
        Assert.Contains("404", detail.Failure);
        Assert.DoesNotContain("99", detail.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// A message's person matches: read from the message's own match route,
    /// handed back with the patient and the candidates best first.
    /// </summary>
    [Fact]
    public async Task PersonMatchesReadsTheMessagesPatientAndCandidates()
    {
        HttpRequestMessage? sent = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;

            return Answer(
                HttpStatusCode.OK,
                """
                {
                  "patient": { "lastName": "Thorsen", "firstName": "Inge", "middleName": null,
                               "birthDate": "1990-01-15", "birthGender": "F" },
                  "candidates": [{
                    "person": { "id": 13, "lastName": "Thorsen", "firstName": "Inge", "middleName": null,
                                "birthDate": "1990-01-15", "birthGender": "F", "currentGender": null },
                    "score": 85, "verdict": "Probable", "reasons": ["Last name matches"]
                  }]
                }
                """);
        }));

        var matches = await client.PersonMatches(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/staged-messages/11/person-matches", sent?.RequestUri?.ToString());
        Assert.True(matches.Succeeded);
        Assert.Equal("Thorsen", matches.Value?.Patient?.LastName);
        Assert.Equal(13, Assert.Single(matches.Value!.Candidates).Person.Id);
    }

    [Fact]
    public async Task PersonMatchesThatFailedCarryTheMatchPrefix()
    {
        var client = new MessageQueueClient(Transport(_ => Answer(
            HttpStatusCode.NotFound, """{ "status": 404, "detail": "No staged message has id 99." }""")));

        var matches = await client.PersonMatches(99);

        Assert.False(matches.Succeeded);
        Assert.StartsWith(MessageQueueClient.MatchFailurePrefix, matches.Failure);
        Assert.Contains("404", matches.Failure);
    }

    /// <summary>
    /// Moving messages: the ids and the target state are posted to the move
    /// URL, and how many moved comes back.
    /// </summary>
    [Fact]
    public async Task MovePostsTheIdsAndTheStateAndTheCountComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, """{ "count": 2 }""");
        }));

        var moved = await client.Move([11, 12], "pending");

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/staged-messages/move", sent?.RequestUri?.ToString());
        Assert.Contains("\"ids\":[11,12]", body, StringComparison.Ordinal);
        Assert.Contains("\"status\":\"pending\"", body, StringComparison.Ordinal);
        Assert.Equal(2, moved.Value?.Count);
    }

    [Fact]
    public async Task DeletePostsTheIdsToTheDeleteUrl()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, """{ "count": 1 }""");
        }));

        var deleted = await client.Delete([11]);

        Assert.Equal("http://api.example.test/api/staged-messages/delete", sent?.RequestUri?.ToString());
        Assert.Contains("\"ids\":[11]", body, StringComparison.Ordinal);
        Assert.Equal(1, deleted.Value?.Count);
    }

    [Fact]
    public async Task RetryPostsTheIdsToTheRetryUrl()
    {
        HttpRequestMessage? sent = null;

        var client = new MessageQueueClient(Transport(request =>
        {
            sent = request;

            return Answer(HttpStatusCode.OK, """{ "count": 3 }""");
        }));

        var retried = await client.Retry([11, 12, 13]);

        Assert.Equal("http://api.example.test/api/staged-messages/retry", sent?.RequestUri?.ToString());
        Assert.Equal(3, retried.Value?.Count);
    }

    /// <summary>
    /// An action the API tier refused: each starts with its own prefix and
    /// names the status, never the body.
    /// </summary>
    [Fact]
    public async Task ARefusedActionCarriesItsOwnPrefix()
    {
        var client = new MessageQueueClient(Transport(_ => Answer(
            HttpStatusCode.Conflict,
            """{ "status": 409, "detail": "Message 11: This message has become a case." }""")));

        var moved = await client.Move([11], "pending");
        var deleted = await client.Delete([11]);
        var retried = await client.Retry([11]);

        Assert.StartsWith(MessageQueueClient.MoveFailurePrefix, moved.Failure);
        Assert.StartsWith(MessageQueueClient.DeleteFailurePrefix, deleted.Failure);
        Assert.StartsWith(MessageQueueClient.RetryFailurePrefix, retried.Failure);
        Assert.Contains("409", deleted.Failure);
        Assert.DoesNotContain("become a case", deleted.Failure, StringComparison.Ordinal);
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
