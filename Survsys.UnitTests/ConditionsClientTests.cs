using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of <c>GET /api/conditions</c>, against a
/// stubbed transport.
/// </summary>
/// <remarks>
/// No host and no socket: the thing worth proving here is the translation of
/// every way the ask can fail into a message a screen can show, and that
/// nothing it is handed escapes as an exception. The API tier's own behaviour
/// is proved by the integration suite, which drives the real endpoint.
/// </remarks>
public class ConditionsClientTests
{
    private const string Json = "application/json";

    [Fact]
    public async Task APageTheApiReturnedIsThePageTheClientReturns()
    {
        var client = Answering(
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

        var asked = await client.Page(1, 50);

        Assert.True(asked.Succeeded);
        Assert.Null(asked.Failure);

        var page = Assert.IsType<PagedResult<ConditionResponse>>(asked.Result);
        Assert.Equal(7, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(
            new[] { "Giardiasis", "Malaria" },
            page.Items.Select(condition => condition.Name));
        Assert.Equal(new ConditionResponse(9, "Malaria", null, false), page.Items[1]);
    }

    [Fact]
    public async Task TheRequestNamesThePageAndSizeAskedFor()
    {
        Uri? asked = null;

        var client = new ConditionsClient(new HttpClient(new StubTransport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, EmptyPage);
        }))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        await client.Page(3, 25);

        Assert.NotNull(asked);
        Assert.Equal("http://api.example.test/api/conditions?page=3&pageSize=25", asked.ToString());
    }

    /// <summary>
    /// The failure the solution plan's contract is actually about: the API
    /// answered, and answered badly. A 500 from this tier carries Problem
    /// Details with a correlation id; what reaches the screen is the status
    /// and not that body.
    /// </summary>
    [Fact]
    public async Task AFailedStatusBecomesAMessageAndNotAnException()
    {
        var client = Answering(
            HttpStatusCode.InternalServerError,
            """
            {
              "title": "An error occurred while processing your request.",
              "status": 500,
              "correlationId": "00-not-for-a-screen-00"
            }
            """);

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.Null(asked.Result);
        Assert.NotNull(asked.Failure);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
        Assert.Contains("500", asked.Failure);
        Assert.DoesNotContain("correlation", asked.Failure, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ABadRequestBecomesAMessageNamingTheStatus()
    {
        var client = Answering(HttpStatusCode.BadRequest, """{ "status": 400 }""");

        var asked = await client.Page(0, 50);

        Assert.False(asked.Succeeded);
        Assert.Contains("400", asked.Failure);
    }

    [Fact]
    public async Task AnApiTierThatDoesNotAnswerBecomesAMessage()
    {
        var client = new ConditionsClient(new HttpClient(new StubTransport(
            _ => throw new HttpRequestException("No connection could be made.")))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
        Assert.DoesNotContain("No connection could be made", asked.Failure);
    }

    [Fact]
    public async Task AnAnswerThatIsNotAPageBecomesAMessage()
    {
        var client = Answering(HttpStatusCode.OK, "<html>a proxy's error page</html>", "text/html");

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AnEmptyBodyWhereAPageWasExpectedBecomesAMessage()
    {
        var client = Answering(HttpStatusCode.OK, "null");

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
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

        var client = new ConditionsClient(new HttpClient(new StubTransport(
            _ => throw new TaskCanceledException()))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Page(1, 50, cancelled.Token));
    }

    [Fact]
    public async Task AnApiTierThatAnswersTooSlowlyBecomesAMessage()
    {
        var client = new ConditionsClient(new HttpClient(new StubTransport(
            _ => throw new TaskCanceledException("The request timed out.")))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
    }

    private const string EmptyPage =
        """{ "items": [], "page": 1, "pageSize": 50, "totalCount": 0 }""";

    private static ConditionsClient Answering(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        new(new HttpClient(new StubTransport(_ => Answer(status, body, mediaType)))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

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
    /// The transport under the client: it answers with whatever the test said
    /// to answer with, or throws what the test said to throw.
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
