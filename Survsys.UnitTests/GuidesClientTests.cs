using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the notification guides and
/// the header variable defaults: the request each call sends, what it hands a
/// screen, and the phrase its failures start with. Every way an ask can fail
/// is <see cref="ApiAskTests"/>'s.
/// </summary>
public class GuidesClientTests
{
    private const string Root = "http://api.example.test/api/notification-setup";

    private const string Detail =
        """
        { "id": 3, "name": "Arbo", "version": "Arbo_Case_Map_v1.0", "phinVars": [ "INV163" ], "linkedGuideIds": [ 1 ],
          "variables": [ { "guideVariableDefaultId": 2, "name": "sending_app", "value": null, "defaultValue": "A^1^ISO" } ] }
        """;

    [Fact]
    public async Task ListAndGetAskForTheGuides()
    {
        var sent = new List<HttpRequestMessage>();
        var list = await Recording(sent.Add, HttpStatusCode.OK, """[ { "id": 3, "name": "Arbo", "version": null } ]""").List();
        var detail = await Recording(sent.Add, HttpStatusCode.OK, Detail).Get(3);

        Assert.Equal($"{Root}/guides", sent[0].RequestUri?.ToString());
        Assert.Equal("Arbo", Assert.Single(list.Value!).Name);
        Assert.Equal($"{Root}/guides/3", sent[1].RequestUri?.ToString());
        Assert.Equal(["INV163"], detail.Value!.PhinVars);
        Assert.Equal("A^1^ISO", Assert.Single(detail.Value.Variables).DefaultValue);
    }

    [Fact]
    public async Task CreateUpdateAndDeleteAskForTheGuide()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Detail);
        var row = new GuideRow(0, "Arbo", "Arbo_Case_Map_v1.0");

        await client.Create(row);
        await client.Update(3, row);
        await client.Delete(3);

        Assert.Equal((HttpMethod.Post, $"{Root}/guides"), (sent[0].Method, sent[0].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Put, $"{Root}/guides/3"), (sent[1].Method, sent[1].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Delete, $"{Root}/guides/3"), (sent[2].Method, sent[2].RequestUri?.ToString()));
    }

    [Fact]
    public async Task EachPartOfAGuideIsPutWhole()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Detail);

        await client.SavePhinVariables(3, ["INV163"]);
        await client.SaveLinks(3, [1]);
        await client.SaveVariables(3, [new GuideVariableRow(2, null, "B^2^ISO", null)]);

        Assert.All(sent, request => Assert.Equal(HttpMethod.Put, request.Method));
        Assert.Equal(
            [$"{Root}/guides/3/phin-variables", $"{Root}/guides/3/linked-guides", $"{Root}/guides/3/variables"],
            sent.Select(request => request.RequestUri?.ToString()));
    }

    [Fact]
    public async Task ExportGetsAndImportPostsTheGuidesJson()
    {
        var sent = new List<HttpRequestMessage>();
        var exported = await Recording(sent.Add, HttpStatusCode.OK, """{ "name": "Arbo", "version": null, "phinVars": [] }""")
            .Export(3);
        var imported = await Recording(sent.Add, HttpStatusCode.OK, Detail).Import(exported.Value!);

        Assert.Equal($"{Root}/guides/3/json", sent[0].RequestUri?.ToString());
        Assert.Equal((HttpMethod.Post, $"{Root}/guides/json"), (sent[1].Method, sent[1].RequestUri?.ToString()));
        Assert.Equal(3, imported.Value!.Id);
    }

    [Fact]
    public async Task ADefaultIsCreatedWithoutAnIdAndSavedOverWithOne()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, """{ "id": 2, "name": "sending_app", "defaultValue": "A^1^ISO" }""");

        await client.SaveDefault(new GuideVariableDefaultRow(0, "sending_app", "A^1^ISO"));
        await client.SaveDefault(new GuideVariableDefaultRow(2, "sending_app", "A^1^ISO"));
        await client.DeleteDefault(2);

        Assert.Equal((HttpMethod.Post, $"{Root}/guide-variable-defaults"), (sent[0].Method, sent[0].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Put, $"{Root}/guide-variable-defaults/2"), (sent[1].Method, sent[1].RequestUri?.ToString()));
        Assert.Equal((HttpMethod.Delete, $"{Root}/guide-variable-defaults/2"), (sent[2].Method, sent[2].RequestUri?.ToString()));
    }

    [Fact]
    public async Task TheDefaultsAreImportedAndExportedAsCsv()
    {
        var sent = new List<HttpRequestMessage>();
        await Recording(sent.Add, HttpStatusCode.OK, """{ "applied": true, "added": [], "changed": [], "refused": [] }""")
            .ImportDefaults("name,default_value\r\n", confirm: true);
        var exported = await Recording(sent.Add, HttpStatusCode.OK, "name,default_value\r\n").ExportDefaults();

        Assert.Equal($"{Root}/guide-variable-defaults/csv?confirm=true", sent[0].RequestUri?.ToString());
        Assert.Equal("text/csv", sent[0].Content?.Headers.ContentType?.MediaType);
        Assert.Equal($"{Root}/guide-variable-defaults/csv", sent[1].RequestUri?.ToString());
        Assert.Equal("name,default_value\r\n", exported.Value);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");

        Assert.StartsWith(GuidesClient.LoadFailurePrefix, (await client.List()).Failure);
        Assert.StartsWith(GuidesClient.LoadFailurePrefix, (await client.Defaults()).Failure);
        Assert.StartsWith(GuidesClient.SaveFailurePrefix, (await client.Create(new GuideRow(0, "x", null))).Failure);
        Assert.StartsWith(GuidesClient.SaveFailurePrefix, await client.Delete(1));
        Assert.StartsWith(GuidesClient.ImportFailurePrefix, (await client.Import(new GuideExport("x", null, []))).Failure);
        Assert.StartsWith(GuidesClient.ImportFailurePrefix, (await client.ImportDefaults("x", confirm: false)).Failure);
    }

    private static GuidesClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
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
