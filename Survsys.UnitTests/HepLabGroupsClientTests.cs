using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the hepatitis lab groups: the
/// request each call sends, what it hands a screen, and the phrase its
/// failures start with. Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </summary>
public class HepLabGroupsClientTests
{
    private const string Root = "http://api.example.test/api/notification-setup/hep-lab-groups";

    private const string Group =
        """{ "id": 2, "name": "Hepatitis B", "conditionIds": [ 12 ], "testTypes": [ { "testTypeId": 5, "testGroupName": "HBsAg", "sortOrder": 1 } ] }""";

    private static readonly HepLabGroupRow Row = new(0, "Hepatitis B", [12], [new HepLabGroupTestTypeRow(5, "HBsAg", 1)]);

    [Fact]
    public async Task ListAsksForTheGroups()
    {
        HttpRequestMessage? sent = null;

        var asked = await Recording(request => sent = request, HttpStatusCode.OK, $"[{Group}]").List();

        Assert.Equal(Root, sent?.RequestUri?.ToString());
        Assert.Equal("HBsAg", Assert.Single(Assert.Single(asked.Value!).TestTypes).TestGroupName);
    }

    [Fact]
    public async Task SavePostsANewGroupAndPutsAnExistingOne()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Group);

        await client.Save(Row);
        await client.Save(Row with { Id = 2 });
        await client.Delete(2);

        Assert.Equal((HttpMethod.Post, Root), (sent[0].Method, sent[0].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Put, $"{Root}/2"), (sent[1].Method, sent[1].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Delete, $"{Root}/2"), (sent[2].Method, sent[2].RequestUri?.ToString()));
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(HepLabGroupsClient.LoadFailurePrefix, (await client.List()).Failure);
        Assert.StartsWith(HepLabGroupsClient.SaveFailurePrefix, (await client.Save(Row)).Failure);
        Assert.StartsWith(HepLabGroupsClient.SaveFailurePrefix, await client.Delete(2));
    }

    private static HepLabGroupsClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
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
