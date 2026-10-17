using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/core-elements</c>: the
/// request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class CoreElementsClientTests
{
    private const string Json = "application/json";

    private const string FoodHandler =
        """{ "id": 6, "uid": "food_handler", "label": "Food handler", "tab": "Investigation", "hidden": true }""";

    private const string Override =
        """{ "id": 3, "coreElementId": 6, "conditionId": 40, "agencyId": null, "hidden": false }""";

    private static readonly CoreElementResponse FoodHandlerResponse = new(6, "food_handler", "Food handler", "Investigation", true);

    private static readonly CoreElementOverrideResponse OverrideResponse = new(3, 6, 40, null, false);

    [Fact]
    public async Task AllAsksForTheElements()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{FoodHandler}]");

        var asked = await client.All();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/core-elements", sent?.RequestUri?.ToString());
        Assert.Equal(FoodHandlerResponse, Assert.Single(asked.Value!));
    }

    [Fact]
    public async Task SetDefaultPutsTheFlagToTheElement()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
        }, HttpStatusCode.OK, FoodHandler);

        var asked = await client.SetDefault(6, true);

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/core-elements/6", sent?.RequestUri?.ToString());
        Assert.Contains("\"hidden\":true", body, StringComparison.Ordinal);
        Assert.Equal(FoodHandlerResponse, asked.Value);
    }

    [Fact]
    public async Task OverridesAsksForTheOverrides()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Override}]");

        var asked = await client.Overrides();

        Assert.Equal("http://api.example.test/api/core-elements/overrides", sent?.RequestUri?.ToString());
        Assert.Equal(OverrideResponse, Assert.Single(asked.Value!));
    }

    [Fact]
    public async Task SaveOverridePostsToTheOverrides()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Override);

        var asked = await client.SaveOverride(new SaveCoreElementOverrideRequest(6, 40, null, false));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/core-elements/overrides", sent?.RequestUri?.ToString());
        Assert.Equal(OverrideResponse, asked.Value);
    }

    [Fact]
    public async Task DeleteOverrideDeletesTheOverride()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.NoContent, "");

        var failure = await client.DeleteOverride(3);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/core-elements/overrides/3", sent?.RequestUri?.ToString());
        Assert.Null(failure);
    }

    [Fact]
    public async Task HiddenOnAsksForTheReportsHiddenElements()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, """["food_handler","event_name"]""");

        var asked = await client.HiddenOn(42);

        Assert.Equal("http://api.example.test/api/person-conditions/42/hidden-elements", sent?.RequestUri?.ToString());
        Assert.Equal(["food_handler", "event_name"], asked.Value);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(CoreElementsClient.LoadFailurePrefix, (await client.All()).Failure);
        Assert.StartsWith(CoreElementsClient.LoadFailurePrefix, (await client.Overrides()).Failure);
        Assert.StartsWith(CoreElementsClient.SaveFailurePrefix, (await client.SetDefault(6, true)).Failure);
        Assert.StartsWith(CoreElementsClient.SaveFailurePrefix,
            (await client.SaveOverride(new SaveCoreElementOverrideRequest(6, 40, null, true))).Failure);
        Assert.StartsWith(CoreElementsClient.DeleteFailurePrefix, await client.DeleteOverride(3));
        Assert.StartsWith(CoreElementsClient.HiddenFailurePrefix, (await client.HiddenOn(42)).Failure);
    }

    private static CoreElementsClient Recording(
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
