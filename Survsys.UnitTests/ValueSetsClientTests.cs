using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the PHIN value sets and units
/// of measure: the request each call sends, what it hands a screen, and the
/// phrase its failures start with. Every way an ask can fail is
/// <see cref="ApiAskTests"/>'s.
/// </summary>
public class ValueSetsClientTests
{
    private const string Json = "application/json";

    private const string Concept =
        """{ "id": 4, "valueSetName": "PHVS_YesNoUnknown_CDC", "conceptCode": "Y", "preferredConceptName": "Yes", "hl7Code": "Y", "codeSystemOid": null, "cdcName": null }""";

    [Fact]
    public async Task ListAsksForTheTableAndReturnsItsRows()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Concept}]");

        var asked = await client.List<PhinValueSetRow>(ValueSetsClient.PhinValueSets);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/notification-setup/phin-value-sets", sent?.RequestUri?.ToString());
        Assert.Equal("Y", Assert.Single(asked.Value!).ConceptCode);
    }

    [Fact]
    public async Task CreatePostsAndUpdatePutsToTheTable()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, Concept);
        var row = new PhinValueSetRow(0, "PHVS_YesNoUnknown_CDC", "Y", "Yes", "Y", null, null);

        await client.Create(ValueSetsClient.PhinValueSets, row);
        var updated = await client.Update(ValueSetsClient.PhinValueSets, 4, row);

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal("http://api.example.test/api/notification-setup/phin-value-sets", sent[0].RequestUri?.ToString());
        Assert.Equal(HttpMethod.Put, sent[1].Method);
        Assert.Equal("http://api.example.test/api/notification-setup/phin-value-sets/4", sent[1].RequestUri?.ToString());
        Assert.Equal(4, updated.Value!.Id);
    }

    [Fact]
    public async Task DeleteAsksForTheEntry()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.NoContent, "");

        Assert.Null(await client.Delete(ValueSetsClient.UnitsOfMeasure, 7));
        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/notification-setup/units-of-measure/7", sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ImportPostsTheCsvForAPreviewOrAConfirmation()
    {
        var sent = new List<HttpRequestMessage>();
        var client = Recording(sent.Add, HttpStatusCode.OK, """{ "applied": false, "added": [ { "line": 2, "key": "PHVS Y" } ], "changed": [], "refused": [] }""");

        var preview = await client.Import(ValueSetsClient.PhinValueSets, "value_set_name\r\n", confirm: false);
        await client.Import(ValueSetsClient.PhinValueSets, "value_set_name\r\n", confirm: true);

        Assert.Equal("http://api.example.test/api/notification-setup/phin-value-sets/csv", sent[0].RequestUri?.ToString());
        Assert.Equal("text/csv", sent[0].Content?.Headers.ContentType?.MediaType);
        Assert.Equal(
            "http://api.example.test/api/notification-setup/phin-value-sets/csv?confirm=true", sent[1].RequestUri?.ToString());
        Assert.Equal("PHVS Y", Assert.Single(preview.Value!.Added).Key);
    }

    [Fact]
    public async Task ExportReturnsTheCsvText()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "value_set_name,concept_code\r\n");

        var asked = await client.Export(ValueSetsClient.UnitsOfMeasure);

        Assert.Equal("http://api.example.test/api/notification-setup/units-of-measure/csv", sent?.RequestUri?.ToString());
        Assert.Equal("value_set_name,concept_code\r\n", asked.Value);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");
        var row = new PhinUnitOfMeasureRow(0, "PHVS_AgeUnit_UCUM", "a", "year", null, null);

        Assert.StartsWith(ValueSetsClient.LoadFailurePrefix,
            (await client.List<PhinUnitOfMeasureRow>(ValueSetsClient.UnitsOfMeasure)).Failure);
        Assert.StartsWith(ValueSetsClient.SaveFailurePrefix, (await client.Create(ValueSetsClient.UnitsOfMeasure, row)).Failure);
        Assert.StartsWith(ValueSetsClient.SaveFailurePrefix, (await client.Update(ValueSetsClient.UnitsOfMeasure, 1, row)).Failure);
        Assert.StartsWith(ValueSetsClient.SaveFailurePrefix, await client.Delete(ValueSetsClient.UnitsOfMeasure, 1));
        Assert.StartsWith(ValueSetsClient.ImportFailurePrefix,
            (await client.Import(ValueSetsClient.UnitsOfMeasure, "x", confirm: false)).Failure);
        Assert.StartsWith(ValueSetsClient.ExportFailurePrefix, (await client.Export(ValueSetsClient.UnitsOfMeasure)).Failure);
    }

    private static ValueSetsClient Recording(Action<HttpRequestMessage> record, HttpStatusCode status, string body) =>
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
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
