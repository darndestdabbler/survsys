using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the PHIN variable catalogue:
/// the request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class PhinVariablesClientTests
{
    private const string Root = "http://api.example.test/api/notification-setup/phin-variables";

    private const string Variable =
        """{ "phinVar": "INV163", "description": "Case Status", "identifier": null, "priority": null, "hl7DataType": "CWE", "ruleSetId": 4, "ruleSetConfigId": 12, "status": null, "note": null, "codeSystem": "SCT" }""";

    private static readonly PhinVariableRow Row = new("INV163", "Case Status", null, null, "CWE", 4, 12, null, null, "SCT");

    [Fact]
    public async Task ListAsksForTheCatalogue()
    {
        HttpRequestMessage? sent = null;

        var asked = await Recording(request => sent = request, HttpStatusCode.OK, $"[{Variable}]").List();

        Assert.Equal(Root, sent?.RequestUri?.ToString());
        Assert.Equal(12, Assert.Single(asked.Value!).RuleSetConfigId);
    }

    [Fact]
    public async Task CreatePostsAndUpdateAndDeleteNameTheVariable()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Variable);

        await client.Create(Row);
        await client.Update(Row with { PhinVar = "TB 1" });
        await client.Delete("INV163");

        Assert.Equal((HttpMethod.Post, Root), (sent[0].Method, sent[0].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Put, $"{Root}/TB%201"), (sent[1].Method, sent[1].RequestUri?.AbsoluteUri));
        Assert.Equal((HttpMethod.Delete, $"{Root}/INV163"), (sent[2].Method, sent[2].RequestUri?.ToString()));
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(PhinVariablesClient.LoadFailurePrefix, (await client.List()).Failure);
        Assert.StartsWith(PhinVariablesClient.SaveFailurePrefix, (await client.Create(Row)).Failure);
        Assert.StartsWith(PhinVariablesClient.SaveFailurePrefix, (await client.Update(Row)).Failure);
        Assert.StartsWith(PhinVariablesClient.SaveFailurePrefix, await client.Delete("INV163"));
    }

    private static PhinVariablesClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
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
