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
