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
/// What is left here is what is still this client's own: the URL it asks, the
/// phrase its messages start with, and the page it hands a screen. Every way
/// an ask can fail is <see cref="ApiAskTests"/>'s, because the handling of
/// those moved into <c>ApiAsk</c> when there were four clients sharing it —
/// a method takes its tests where it goes.
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

        var page = Assert.IsType<PagedResult<ConditionResponse>>(asked.Value);
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
    /// A failure is still a message and still this client's own message: the
    /// move into <c>ApiAsk</c> left the sentence a user reads word for word
    /// where it was, which is what the conditions walkthrough asserts on
    /// screen.
    /// </summary>
    [Fact]
    public async Task AFailureCarriesThisClientsPrefixAndNotTheApisBody()
    {
        var client = Answering(
            HttpStatusCode.InternalServerError,
            """{ "status": 500, "correlationId": "00-not-for-a-screen-00" }""");

        var asked = await client.Page(1, 50);

        Assert.False(asked.Succeeded);
        Assert.Null(asked.Value);
        Assert.StartsWith(ConditionsClient.FailurePrefix, asked.Failure);
        Assert.Contains("500", asked.Failure);
        Assert.DoesNotContain("correlation", asked.Failure, StringComparison.OrdinalIgnoreCase);
    }

    private const string HepatitisA =
        """
        {
          "id": 74, "name": "Hepatitis A", "cdcCode": "10110", "active": true,
          "contactLeadIn": "Contacts.", "placeLeadIn": null, "treatmentLeadIn": null
        }
        """;

    private static readonly ConditionDetailResponse HepatitisADetail =
        new(74, "Hepatitis A", "10110", true, "Contacts.", null, null);

    [Fact]
    public async Task FindAsksForTheConditionAndReturnsIt()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, HepatitisA);

        var asked = await client.Find(74);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/conditions/74", sent?.RequestUri?.ToString());
        Assert.Equal(HepatitisADetail, asked.Value);
    }

    [Fact]
    public async Task CreatePostsToTheCollectionAndReturnsTheCreatedCondition()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, HepatitisA);

        var asked = await client.Create(new SaveConditionRequest("Hepatitis A", true, "10110", "Contacts."));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/conditions", sent?.RequestUri?.ToString());
        Assert.Equal(HepatitisADetail, asked.Value);
    }

    [Fact]
    public async Task UpdatePutsToTheConditionAndReturnsItAsSaved()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, HepatitisA);

        var asked = await client.Update(74, new SaveConditionRequest("Hepatitis A", true));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/conditions/74", sent?.RequestUri?.ToString());
        Assert.Equal(HepatitisADetail, asked.Value);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = Answering(HttpStatusCode.BadRequest, "{}");

        var asked = await client.Update(74, new SaveConditionRequest("", true));

        Assert.False(asked.Succeeded);
        Assert.StartsWith(ConditionsClient.SaveFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AFindThatFailedCarriesTheFindPrefix()
    {
        var client = Answering(HttpStatusCode.NotFound, "{}");

        var asked = await client.Find(99);

        Assert.StartsWith(ConditionsClient.FindFailurePrefix, asked.Failure);
    }

    private static ConditionsClient Recording(
        Action<HttpRequestMessage> record,
        HttpStatusCode status,
        string body) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return Answer(status, body);
        }))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

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
    /// to answer with.
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
