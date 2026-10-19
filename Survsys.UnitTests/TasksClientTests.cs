using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of tasks: the URL each of its four
/// asks makes, what the status filter puts in the query string, the body a
/// record and a status send, the value handed back, and the phrase each of its
/// three failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that a report's tasks are read by its own URL and the whole system's by
/// the collection URL, that a named status reaches the API as a filter and an
/// absent one leaves the query string alone, that the task the form filled in
/// is the one posted, that the whole series the API answered comes back, and
/// that the three failures are told apart.
/// </remarks>
public class TasksClientTests
{
    private const string Json = "application/json";

    private const string Tasks =
        """
        {
          "items": [
            {
              "id": 21,
              "personConditionId": 14,
              "name": "Follow-up call",
              "notes": "Ask about the household contacts",
              "priority": "high",
              "dueDate": "2026-03-16",
              "userId": 11,
              "categoryId": 3,
              "status": "open",
              "repeatingTaskId": null,
              "repeatingInterval": "weekly",
              "untilDate": "2026-04-13",
              "createdAt": "2026-03-13T00:13:17.113-04:00",
              "updatedAt": null
            }
          ],
          "page": 1,
          "pageSize": 50,
          "totalCount": 1
        }
        """;

    private const string Series =
        """
        {
          "tasks": [
            {
              "id": 21,
              "personConditionId": 14,
              "name": "Follow-up call",
              "notes": null,
              "priority": null,
              "dueDate": "2026-03-16",
              "userId": null,
              "categoryId": null,
              "status": "open",
              "repeatingTaskId": null,
              "repeatingInterval": "weekly",
              "untilDate": "2026-03-30",
              "createdAt": "2026-03-13T00:13:17.113-04:00",
              "updatedAt": null
            },
            {
              "id": 22,
              "personConditionId": 14,
              "name": "Follow-up call",
              "notes": null,
              "priority": null,
              "dueDate": "2026-03-23",
              "userId": null,
              "categoryId": null,
              "status": "open",
              "repeatingTaskId": 21,
              "repeatingInterval": "weekly",
              "untilDate": "2026-03-30",
              "createdAt": "2026-03-13T00:13:17.113-04:00",
              "updatedAt": null
            },
            {
              "id": 23,
              "personConditionId": 14,
              "name": "Follow-up call",
              "notes": null,
              "priority": null,
              "dueDate": "2026-03-30",
              "userId": null,
              "categoryId": null,
              "status": "open",
              "repeatingTaskId": 21,
              "repeatingInterval": "weekly",
              "untilDate": "2026-03-30",
              "createdAt": "2026-03-13T00:13:17.113-04:00",
              "updatedAt": null
            }
          ]
        }
        """;

    private const string Completed =
        """
        {
          "id": 21,
          "personConditionId": 14,
          "name": "Follow-up call",
          "notes": null,
          "priority": null,
          "dueDate": "2026-03-16",
          "userId": null,
          "categoryId": null,
          "status": "complete",
          "repeatingTaskId": null,
          "repeatingInterval": null,
          "untilDate": null,
          "createdAt": "2026-03-13T00:13:17.113-04:00",
          "updatedAt": "2026-03-16T14:02:00-04:00"
        }
        """;

    [Fact]
    public async Task PageAsksTheReportsTasksUrlWithItsPageAndHandsTheListBack()
    {
        HttpRequestMessage? sent = null;

        var client = new TasksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Tasks);
        }));

        var page = await client.Page(14, "open", 1, 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/tasks?status=open&page=1&pageSize=50",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(page.Succeeded);
        Assert.Equal(1, page.Value?.TotalCount);
        var first = Assert.Single(page.Value!.Items);
        Assert.Equal(21, first.Id);
        Assert.Equal("Follow-up call", first.Name);
        Assert.Equal(new DateOnly(2026, 3, 16), first.DueDate);
        Assert.Equal("weekly", first.RepeatingInterval);
        Assert.Equal(new DateOnly(2026, 4, 13), first.UntilDate);
        Assert.Null(first.UpdatedAt);
    }

    /// <summary>
    /// The task list screen reads the collection URL, which is every report's
    /// tasks rather than one report's.
    /// </summary>
    [Fact]
    public async Task AllAsksTheTasksCollectionUrlAcrossEveryReport()
    {
        HttpRequestMessage? sent = null;

        var client = new TasksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Tasks);
        }));

        var page = await client.All("complete", 2, 50);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/tasks?status=complete&page=2&pageSize=50",
            sent?.RequestUri?.ToString());

        Assert.True(page.Succeeded);
        Assert.Equal(14, Assert.Single(page.Value!.Items).PersonConditionId);
    }

    /// <summary>
    /// "All tasks" is the absence of a filter, so no status reaches the query
    /// string at all: an empty one would ask for the tasks whose status is the
    /// empty string, which is not what the filter means.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoStatusIsNoFilterInTheQueryString(string? status)
    {
        var asked = new List<string>();

        var client = new TasksClient(Transport(request =>
        {
            asked.Add(request.RequestUri!.PathAndQuery);
            return Answer(HttpStatusCode.OK, Tasks);
        }));

        await client.All(status, 1, 50);
        await client.Page(14, status, 1, 50);

        Assert.Equal(
            [
                "/api/tasks?page=1&pageSize=50",
                "/api/person-conditions/14/tasks?page=1&pageSize=50",
            ],
            asked);
    }

    [Fact]
    public async Task RecordPostsTheTaskAndHandsTheWholeSeriesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new TasksClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.Created, Series);
        }));

        var recorded = await client.Record(14, new CreateTaskRequest(
            Name: "Follow-up call",
            DueDate: new DateOnly(2026, 3, 16),
            Status: "open",
            RepeatingInterval: "weekly",
            UntilDate: new DateOnly(2026, 3, 30)));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/tasks",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"name\":\"Follow-up call\"", body, StringComparison.Ordinal);
        Assert.Contains("\"dueDate\":\"2026-03-16\"", body, StringComparison.Ordinal);
        Assert.Contains("\"repeatingInterval\":\"weekly\"", body, StringComparison.Ordinal);
        Assert.Contains("\"untilDate\":\"2026-03-30\"", body, StringComparison.Ordinal);

        // The report, the series link and the moments are the server's to set,
        // so the posted task names none of them.
        using var posted = JsonDocument.Parse(body!);
        Assert.False(posted.RootElement.TryGetProperty("personConditionId", out _));
        Assert.False(posted.RootElement.TryGetProperty("repeatingTaskId", out _));
        Assert.False(posted.RootElement.TryGetProperty("createdAt", out _));

        // A weekly task until the 30th is three dated tasks, each later one
        // pointing at the first.
        Assert.True(recorded.Succeeded);
        Assert.Equal(3, recorded.Value?.Tasks.Count);
        Assert.Equal(
            [new DateOnly(2026, 3, 16), new DateOnly(2026, 3, 23), new DateOnly(2026, 3, 30)],
            recorded.Value!.Tasks.Select(task => task.DueDate));
        Assert.Null(recorded.Value.Tasks[0].RepeatingTaskId);
        Assert.Equal([21, 21], recorded.Value.Tasks.Skip(1).Select(task => task.RepeatingTaskId));
    }

    /// <summary>
    /// The one mutation a task takes is a PUT to the task's own URL carrying
    /// the status, and what comes back is the task as it now stands — with the
    /// moment the API tier recorded the change.
    /// </summary>
    [Fact]
    public async Task SetStatusPutsTheStatusToTheTasksOwnUrlAndHandsTheTaskBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new TasksClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, Completed);
        }));

        var done = await client.SetStatus(21, "complete");

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/tasks/21/status", sent?.RequestUri?.ToString());
        Assert.Contains("\"status\":\"complete\"", body, StringComparison.Ordinal);

        Assert.True(done.Succeeded);
        Assert.Equal("complete", done.Value?.Status);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 16, 14, 2, 0, TimeSpan.FromHours(-4)),
            done.Value?.UpdatedAt);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefixAndNotTheApisBody()
    {
        var listFailed = await new TasksClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")))
            .Page(987654, null, 1, 50);

        Assert.False(listFailed.Succeeded);
        Assert.StartsWith(TasksClient.ListFailurePrefix, listFailed.Failure);
        Assert.Contains("404", listFailed.Failure);
        Assert.DoesNotContain("987654", listFailed.Failure, StringComparison.Ordinal);

        // A report's tasks and every report's are the same list under two
        // scopes, so they fail in the same words.
        var allFailed = await new TasksClient(Transport(_ => Answer(
                HttpStatusCode.InternalServerError, """{ "status": 500 }""")))
            .All(null, 1, 50);

        Assert.StartsWith(TasksClient.ListFailurePrefix, allFailed.Failure);

        var recordFailed = await new TasksClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "untilDate 2026-03-01 is before dueDate 2026-03-16." }""")))
            .Record(14, new CreateTaskRequest(Name: "Follow-up call"));

        Assert.False(recordFailed.Succeeded);
        Assert.StartsWith(TasksClient.RecordFailurePrefix, recordFailed.Failure);
        Assert.Contains("400", recordFailed.Failure);

        var statusFailed = await new TasksClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No task has id 987654." }""")))
            .SetStatus(987654, "complete");

        Assert.False(statusFailed.Succeeded);
        Assert.StartsWith(TasksClient.StatusFailurePrefix, statusFailed.Failure);
        Assert.Contains("404", statusFailed.Failure);

        // The three sentences are three sentences.
        Assert.NotEqual(TasksClient.ListFailurePrefix, TasksClient.RecordFailurePrefix);
        Assert.NotEqual(TasksClient.RecordFailurePrefix, TasksClient.StatusFailurePrefix);
        Assert.NotEqual(TasksClient.ListFailurePrefix, TasksClient.StatusFailurePrefix);
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
