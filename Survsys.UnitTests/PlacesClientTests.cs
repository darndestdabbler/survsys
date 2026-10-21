using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/ct-towns</c> and
/// <c>/api/zip-codes</c>: the request each call sends, what it hands a
/// screen, and the phrase its failures start with. Every way an ask can fail
/// is <see cref="ApiAskTests"/>'s.
/// </summary>
public class PlacesClientTests
{
    private const string Json = "application/json";

    private const string Andover = """{ "town": "Andover", "county": "09013", "planningRegion": "09110" }""";

    [Fact]
    public void TheTablesAreTheirPathSegments()
    {
        Assert.Equal(("ct-towns", "zip-codes"), (PlacesClient.Towns, PlacesClient.ZipCodes));
    }

    [Fact]
    public async Task ListAsksForTheTableAndReturnsItsRows()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Andover}]");

        var asked = await client.List<CtTownRow>(PlacesClient.Towns);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/ct-towns", sent?.RequestUri?.ToString());
        Assert.Equal(new CtTownRow("Andover", "09013", "09110"), Assert.Single(asked.Value!));
    }

    [Fact]
    public async Task CreatePostsToTheTable()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, Andover);

        var asked = await client.Create(PlacesClient.Towns, new CtTownRow("Andover", "09013", "09110"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/ct-towns", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task UpdatePutsToTheRowByItsEscapedKey()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, Andover);

        await client.Update(PlacesClient.Towns, "Windsor Locks", new CtTownRow("Windsor Locks", "09003", "09110"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/ct-towns/Windsor%20Locks", sent?.RequestUri?.AbsoluteUri);
    }

    [Fact]
    public async Task ExportAsksForTheTablesCsv()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "zip_code,county\r\n");

        var asked = await client.Export(PlacesClient.ZipCodes);

        Assert.Equal("http://api.example.test/api/zip-codes/csv", sent?.RequestUri?.ToString());
        Assert.Equal("zip_code,county\r\n", asked.Value);
    }

    [Fact]
    public async Task ImportPostsTheCsvForAPreviewOrAConfirmation()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(
            sent.Add,
            HttpStatusCode.OK,
            """{ "applied": false, "added": [], "changed": [ { "line": 2, "key": "Andover" } ], "refused": [] }""");

        var preview = await client.Import(PlacesClient.Towns, "town,county,planning_region\r\n", confirm: false);
        await client.Import(PlacesClient.Towns, "town,county,planning_region\r\n", confirm: true);

        Assert.Equal(
            ["http://api.example.test/api/ct-towns/csv", "http://api.example.test/api/ct-towns/csv?confirm=true"],
            sent.Select(request => request.RequestUri?.ToString()));
        Assert.Equal("text/csv", sent[0].Content?.Headers.ContentType?.MediaType);
        Assert.Equal("Andover", Assert.Single(preview.Value!.Changed).Key);
    }

    [Fact]
    public async Task EachFailureStartsWithItsOwnPhrase()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "");
        var row = new ZipCodeRow("06101", null);

        Assert.StartsWith(PlacesClient.LoadFailurePrefix, (await client.List<ZipCodeRow>(PlacesClient.ZipCodes)).Failure);
        Assert.StartsWith(PlacesClient.SaveFailurePrefix, (await client.Create(PlacesClient.ZipCodes, row)).Failure);
        Assert.StartsWith(PlacesClient.SaveFailurePrefix, (await client.Update(PlacesClient.ZipCodes, "06101", row)).Failure);
        Assert.StartsWith(PlacesClient.ExportFailurePrefix, (await client.Export(PlacesClient.ZipCodes)).Failure);
        Assert.StartsWith(
            PlacesClient.ImportFailurePrefix, (await client.Import(PlacesClient.ZipCodes, "x", confirm: false)).Failure);
    }

    private static PlacesClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
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

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
