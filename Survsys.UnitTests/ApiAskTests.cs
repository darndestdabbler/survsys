using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// How this tier asks the API tier for something, and what it says when the
/// asking fails — against a stubbed transport, with no host and no socket.
/// </summary>
/// <remarks>
/// These cases were <c>ConditionsClientTests</c>'s while the failure handling
/// was <c>ConditionsClient</c>'s; they moved here with it, because a method
/// takes its tests where it goes. What is proved is the thing the tier's
/// contract is actually about: every way an ask can fail becomes a message a
/// screen can show, nothing escapes as an exception, and no answer's body
/// reaches the message.
///
/// The API tier's own behaviour is proved by the integration suite, which
/// drives the real endpoints.
/// </remarks>
public class ApiAskTests
{
    private const string Json = "application/json";

    private const string Prefix = "The conditions could not be loaded";

    private static readonly ApiAsk Ask = new(Prefix, "a page of conditions");

    private const string EmptyPage =
        """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""";

    [Fact]
    public async Task WhatTheApiReturnedIsWhatComesBack()
    {
        var asked = await Get(
            HttpStatusCode.OK,
            """
            {
              "items": [
                { "id": 4, "name": "Giardiasis", "cdcCode": "11570", "active": true },
                { "id": 9, "name": "Malaria", "cdcCode": null, "active": false }
              ],
              "page": 1,
              "pageSize": 50,
              "totalCount": 7
            }
            """);

        Assert.True(asked.Succeeded);
        Assert.Null(asked.Failure);

        var page = Assert.IsType<PagedResult<ConditionResponse>>(asked.Value);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(new ConditionResponse(9, "Malaria", null, false), page.Items[1]);
    }

    [Fact]
    public async Task TheUrlAskedForIsTheUrlGiven()
    {
        Uri? asked = null;

        await Ask.Get<PagedResult<ConditionResponse>>(
            Transport(request =>
            {
                asked = request.RequestUri;
                return Answer(HttpStatusCode.OK, EmptyPage);
            }),
            "api/conditions?page=3&pageSize=25",
            CancellationToken.None);

        Assert.NotNull(asked);
        Assert.Equal("http://api.example.test/api/conditions?page=3&pageSize=25", asked.ToString());
    }

    /// <summary>
    /// The failure the solution plan's contract is actually about: the API
    /// answered, and answered badly. A 500 from that tier carries Problem
    /// Details with a correlation id; what reaches the screen is the status
    /// and not that body.
    /// </summary>
    [Fact]
    public async Task AFailedStatusBecomesAMessageAndNotAnException()
    {
        var asked = await Get(
            HttpStatusCode.InternalServerError,
            """
            {
              "title": "An error occurred while processing your request.",
              "status": 500,
              "correlationId": "00-not-for-a-screen-00"
            }
            """);

        Assert.False(asked.Succeeded);
        Assert.Null(asked.Value);
        Assert.NotNull(asked.Failure);
        Assert.StartsWith(Prefix, asked.Failure);
        Assert.Contains("500", asked.Failure);
        Assert.DoesNotContain("correlation", asked.Failure, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not-for-a-screen", asked.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// A 400 is a message naming the status as well, and not the body's own
    /// account of what was wrong: a screen validates what it sends, so a 400
    /// reaching here is a bug in this tier rather than something a user can
    /// put right by reading it.
    /// </summary>
    [Fact]
    public async Task ABadRequestBecomesAMessageNamingTheStatus()
    {
        var asked = await Get(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "lastName is required: and so on." }""");

        Assert.False(asked.Succeeded);
        Assert.Contains("400", asked.Failure);
        Assert.DoesNotContain("lastName", asked.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnApiTierThatDoesNotAnswerBecomesAMessage()
    {
        var asked = await Ask.Get<PagedResult<ConditionResponse>>(
            Transport(_ => throw new HttpRequestException("No connection could be made.")),
            "api/conditions",
            CancellationToken.None);

        Assert.False(asked.Succeeded);
        Assert.StartsWith(Prefix, asked.Failure);
        Assert.DoesNotContain("No connection could be made", asked.Failure);
    }

    [Fact]
    public async Task AnAnswerThatIsNotTheValueBecomesAMessage()
    {
        var asked = await Get(HttpStatusCode.OK, "<html>a proxy's error page</html>", "text/html");

        Assert.False(asked.Succeeded);
        Assert.StartsWith(Prefix, asked.Failure);
        Assert.Contains("was not a page of conditions", asked.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEmptyBodyWhereAValueWasExpectedBecomesAMessage()
    {
        var asked = await Get(HttpStatusCode.OK, "null");

        Assert.False(asked.Succeeded);
        Assert.StartsWith(Prefix, asked.Failure);
        Assert.Contains("an empty body where a page of conditions was expected",
            asked.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// A circuit that goes away mid-ask is not a failure to report: there is
    /// no screen left to report it on, so the cancellation stands rather than
    /// being turned into a message.
    /// </summary>
    [Fact]
    public async Task ACancelledAskIsNotTurnedIntoAMessage()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Ask.Get<PagedResult<ConditionResponse>>(
                Transport(_ => throw new TaskCanceledException()),
                "api/conditions",
                cancelled.Token));
    }

    [Fact]
    public async Task AnApiTierThatAnswersTooSlowlyBecomesAMessage()
    {
        var asked = await Ask.Get<PagedResult<ConditionResponse>>(
            Transport(_ => throw new TaskCanceledException("The request timed out.")),
            "api/conditions",
            CancellationToken.None);

        Assert.False(asked.Succeeded);
        Assert.Contains("did not answer in time", asked.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every message starts with what could not be loaded, which is the
    /// client's own phrase: two clients failing for the same reason say
    /// different things, because a user is told which screen's data is
    /// missing.
    /// </summary>
    [Fact]
    public async Task EveryMessageCarriesTheAsksOwnPrefix()
    {
        var people = new ApiAsk("The people could not be loaded", "a page of people");

        var asked = await people.Get<PagedResult<PersonResponse>>(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")),
            "api/persons",
            CancellationToken.None);

        Assert.StartsWith("The people could not be loaded", asked.Failure);
    }

    /// <summary>
    /// A post carries the body as JSON and answers with what was created.
    /// </summary>
    [Fact]
    public async Task APostSendsTheBodyAndReturnsWhatWasCreated()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var created = await Ask.Post<CreatePersonRequest, PersonResponse>(
            Transport(request =>
            {
                sent = request;
                body = request.Content!.ReadAsStringAsync().Result;

                return Answer(
                    HttpStatusCode.Created,
                    """
                    {
                      "id": 42, "lastName": "Thorsen", "firstName": "Inge",
                      "middleName": null, "birthDate": "1971-03-09",
                      "birthGender": "F", "currentGender": null
                    }
                    """);
            }),
            "api/persons",
            new CreatePersonRequest("Thorsen", "Inge"),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/persons", sent?.RequestUri?.ToString());
        Assert.Contains("\"lastName\":\"Thorsen\"", body, StringComparison.Ordinal);

        Assert.True(created.Succeeded);
        Assert.Equal(42, created.Value?.Id);
        Assert.Equal("Thorsen", created.Value?.LastName);
    }

    [Fact]
    public async Task APostThatFailedBecomesAMessageLikeAnAskThatFailed()
    {
        var created = await Ask.Post<CreatePersonRequest, PersonResponse>(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")),
            "api/persons",
            new CreatePersonRequest("Thorsen"),
            CancellationToken.None);

        Assert.False(created.Succeeded);
        Assert.Null(created.Value);
        Assert.StartsWith(Prefix, created.Failure);
        Assert.Contains("500", created.Failure);
    }

    private static Task<Asked<PagedResult<ConditionResponse>>> Get(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        Ask.Get<PagedResult<ConditionResponse>>(
            Transport(_ => Answer(status, body, mediaType)),
            "api/conditions",
            CancellationToken.None);

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

    /// <summary>
    /// The transport under the ask: it answers with whatever the test said to
    /// answer with, or throws what the test said to throw.
    /// </summary>
    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
