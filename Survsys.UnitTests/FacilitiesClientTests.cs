using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/facilities</c>: the
/// request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class FacilitiesClientTests
{
    private const string Json = "application/json";

    private const string Arup =
        """
        {
          "id": 11, "name": "ARUP Laboratories", "shortName": "ARUP", "agencyId": 5,
          "street": "500 Chipeta Way", "city": "Salt Lake City", "state": "UT",
          "zip": "84108", "phone": "801-583-2787"
        }
        """;

    private static readonly FacilityResponse ArupResponse = new(
        11, "ARUP Laboratories", "ARUP", 5, "500 Chipeta Way", "Salt Lake City", "UT", "84108", "801-583-2787");

    [Fact]
    public async Task PageAsksForThePageAndSizeAndReturnsThePage()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK,
            $$"""{ "items": [{{Arup}}], "page": 2, "pageSize": 25, "totalCount": 26 }""");

        var asked = await client.Page(2, 25);

        Assert.Equal("http://api.example.test/api/facilities?page=2&pageSize=25", sent?.RequestUri?.ToString());
        Assert.Equal(26, asked.Value!.TotalCount);
        Assert.Equal(ArupResponse, Assert.Single(asked.Value.Items));
    }

    [Fact]
    public async Task FindAsksForTheFacility()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Arup);

        var asked = await client.Find(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/facilities/11", sent?.RequestUri?.ToString());
        Assert.Equal(ArupResponse, asked.Value);
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, Arup);

        var asked = await client.Create(new SaveFacilityRequest("ARUP Laboratories", 5));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/facilities", sent?.RequestUri?.ToString());
        Assert.Equal(ArupResponse, asked.Value);
    }

    [Fact]
    public async Task UpdatePutsToTheFacility()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Arup);

        var asked = await client.Update(11, new SaveFacilityRequest("ARUP Laboratories", 5));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/facilities/11", sent?.RequestUri?.ToString());
        Assert.Equal(ArupResponse, asked.Value);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(FacilitiesClient.PageFailurePrefix, (await client.Page(1, 50)).Failure);
        Assert.StartsWith(FacilitiesClient.FindFailurePrefix, (await client.Find(11)).Failure);
        Assert.StartsWith(FacilitiesClient.SaveFailurePrefix,
            (await client.Create(new SaveFacilityRequest("ARUP", 5))).Failure);
        Assert.StartsWith(FacilitiesClient.SaveFailurePrefix,
            (await client.Update(11, new SaveFacilityRequest("ARUP", 5))).Failure);
    }

    private static FacilitiesClient Recording(
        Action<HttpRequestMessage> record,
        HttpStatusCode status,
        string body) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue(Json) },
                },
            };
        }))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
