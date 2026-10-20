using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of <c>GET /api/agencies</c>: the URL it asks,
/// the list it hands a screen, and the phrase its failures start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What is this
/// client's own is small, which is the point of it being this small.
/// </remarks>
public class AgenciesClientTests
{
    private const string Json = "application/json";

    private const string TwoAgencies =
        """
        {
          "items": [
            { "id": 1, "name": "Bear River" },
            { "id": 5, "name": "Salt Lake County" }
          ],
          "page": 1,
          "pageSize": 2,
          "totalCount": 2
        }
        """;

    [Fact]
    public async Task TheRequestAsksForTheAgenciesAndNoPage()
    {
        Uri? asked = null;

        var client = new AgenciesClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoAgencies);
        }));

        await client.All();

        Assert.Equal("http://api.example.test/api/agencies", asked?.ToString());
    }

    [Fact]
    public async Task TheAgenciesTheApiReturnedAreTheOnesTheClientReturns()
    {
        var client = new AgenciesClient(Transport(_ => Answer(HttpStatusCode.OK, TwoAgencies)));

        var asked = await client.All();

        Assert.True(asked.Succeeded);
        Assert.Equal(
            [new AgencyResponse(1, "Bear River"), new AgencyResponse(5, "Salt Lake County")],
            asked.Value!.Items);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefix()
    {
        var client = new AgenciesClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var asked = await client.All();

        Assert.False(asked.Succeeded);
        Assert.StartsWith(AgenciesClient.FailurePrefix, asked.Failure);
        Assert.NotEqual(ConditionsClient.FailurePrefix, AgenciesClient.FailurePrefix);
    }

    private const string SaltLake =
        """{ "id": 5, "name": "Salt Lake County", "shortName": "SLCo", "zipCodes": ["84101", "84108"], "notificationEmails": "epi@example.org" }""";

    private static readonly AgencyDetailResponse SaltLakeDetail =
        new(5, "Salt Lake County", "SLCo", ["84101", "84108"], "epi@example.org");

    [Fact]
    public async Task FindAsksForTheAgencyAndReturnsItWithItsZipCodes()
    {
        HttpRequestMessage? sent = null;
        var client = new AgenciesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, SaltLake);
        }));

        var asked = await client.Find(5);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/agencies/5", sent?.RequestUri?.ToString());
        Assert.Equal(SaltLakeDetail.ZipCodes, asked.Value!.ZipCodes);
        Assert.Equal(SaltLakeDetail.Name, asked.Value.Name);
        Assert.Equal(SaltLakeDetail.NotificationEmails, asked.Value.NotificationEmails);
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = new AgenciesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, SaltLake);
        }));

        var asked = await client.Create(
            new SaveAgencyRequest("Salt Lake County", "SLCo", ["84101", "84108"], "epi@example.org"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Contains("\"notificationEmails\":\"epi@example.org\"", await sent!.Content!.ReadAsStringAsync());
        Assert.Equal("http://api.example.test/api/agencies", sent?.RequestUri?.ToString());
        Assert.Equal(5, asked.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheAgency()
    {
        HttpRequestMessage? sent = null;
        var client = new AgenciesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, SaltLake);
        }));

        var asked = await client.Update(5, new SaveAgencyRequest("Salt Lake County"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/agencies/5", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new AgenciesClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.Create(new SaveAgencyRequest(""));

        Assert.StartsWith(AgenciesClient.SaveFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AFindThatFailedCarriesTheFindPrefix()
    {
        var client = new AgenciesClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.Find(99);

        Assert.StartsWith(AgenciesClient.FindFailurePrefix, asked.Failure);
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
