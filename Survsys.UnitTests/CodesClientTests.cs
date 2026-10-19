using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/codes</c>: the request
/// each call sends, what it hands a screen, and the phrase its failures start
/// with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class CodesClientTests
{
    private const string Json = "application/json";

    private const string Blood =
        """
        {
          "id": 12, "list": "specimen_source", "code": "BD", "description": "Blood",
          "sortOrder": 15, "active": true, "conditionIds": []
        }
        """;

    [Fact]
    public async Task ListAsksForTheListAndReturnsItsCodes()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Blood}]");

        var asked = await client.List(CodeLists.SpecimenSource);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/codes/specimen_source", sent?.RequestUri?.ToString());
        var code = Assert.Single(asked.Value!);
        Assert.Equal((12, "specimen_source", "BD", "Blood", 15, true), (code.Id, code.List, code.Code, code.Description, code.SortOrder, code.Active));
        Assert.Empty(code.ConditionIds);
    }

    [Fact]
    public async Task ListAsksForAConditionsTestTypesAndTheRetiredCodes()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "[]");

        await client.List(CodeLists.TestType, conditionId: 7, inactive: true);

        Assert.Equal(
            "http://api.example.test/api/codes/test_type?conditionId=7&inactive=true",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task CreatePostsToTheList()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, Blood);

        var asked = await client.Create(CodeLists.SpecimenSource, new SaveCodeRequest("BD", "Blood", 15));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/codes/specimen_source", sent?.RequestUri?.ToString());
        Assert.Equal("BD", asked.Value!.Code);
    }

    [Fact]
    public async Task UpdatePutsToTheCode()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Blood);

        var asked = await client.Update(12, new SaveCodeRequest("BD", "Blood", 15));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/codes/12", sent?.RequestUri?.ToString());
        Assert.Equal(12, asked.Value!.Id);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(CodesClient.ListFailurePrefix, (await client.List(CodeLists.Race)).Failure);
        Assert.StartsWith(CodesClient.SaveFailurePrefix,
            (await client.Create(CodeLists.Race, new SaveCodeRequest("W"))).Failure);
        Assert.StartsWith(CodesClient.SaveFailurePrefix,
            (await client.Update(12, new SaveCodeRequest("W"))).Failure);
    }

    private static CodesClient Recording(
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
