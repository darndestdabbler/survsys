using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the rule set types and rule
/// set configurations: the request each call sends, what it hands a screen,
/// and the phrase its failures start with. Every way an ask can fail is
/// <see cref="ApiAskTests"/>'s.
/// </summary>
public class RuleSetsClientTests
{
    private const string Root = "http://api.example.test/api/notification-setup";

    private const string Config =
        """
        { "id": 12, "formatType": "string", "valueSetName": null, "repeating": false, "fields": [ "record_number" ],
          "data": [ { "name": "dataSet", "value": "Lab" } ], "valueMaps": [],
          "expressions": [ { "value": "Y", "otherSpecified": null, "executionOrder": 1,
            "conditions": [ { "field": "age", "operator": ">=", "value": "18", "andOr": null, "valueIsField": false } ] } ] }
        """;

    private static readonly RuleSetConfigRow Row = new(0, "string", null, false, ["record_number"], [], [], []);

    [Fact]
    public async Task TypesAsksForTheSevenTypes()
    {
        HttpRequestMessage? sent = null;

        var asked = await Recording(request => sent = request, HttpStatusCode.OK, """[ { "id": 1, "label": "staticValue" } ]""").Types();

        Assert.Equal($"{Root}/rule-set-types", sent?.RequestUri?.ToString());
        Assert.Equal("staticValue", Assert.Single(asked.Value!).Label);
    }

    [Fact]
    public async Task GetReadsAConfigurationWhole()
    {
        HttpRequestMessage? sent = null;

        var asked = await Recording(request => sent = request, HttpStatusCode.OK, Config).Get(12);

        Assert.Equal($"{Root}/rule-sets/12", sent?.RequestUri?.ToString());
        Assert.Equal("Lab", Assert.Single(asked.Value!.Data).Value);
        Assert.Equal(">=", Assert.Single(Assert.Single(asked.Value.Expressions).Conditions).Operator);
    }

    [Fact]
    public async Task CreatePostsAndSavePutsTheConfiguration()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Config);

        await client.Create(Row);
        var saved = await client.Save(12, Row);

        Assert.Equal((HttpMethod.Post, $"{Root}/rule-sets"), (sent[0].Method, sent[0].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Put, $"{Root}/rule-sets/12"), (sent[1].Method, sent[1].RequestUri?.ToString()));
        Assert.Equal(12, saved.Value!.Id);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(RuleSetsClient.LoadFailurePrefix, (await client.Types()).Failure);
        Assert.StartsWith(RuleSetsClient.LoadFailurePrefix, (await client.Get(1)).Failure);
        Assert.StartsWith(RuleSetsClient.SaveFailurePrefix, (await client.Create(Row)).Failure);
        Assert.StartsWith(RuleSetsClient.SaveFailurePrefix, (await client.Save(1, Row)).Failure);
    }

    private static RuleSetsClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
        new(new HttpClient(new StubTransport(request =>
        {
            record(request);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/json") },
                },
            };
        }))
        {
            BaseAddress = new Uri("http://api.example.test/"),
        });

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
