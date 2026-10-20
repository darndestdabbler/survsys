using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/vocabulary</c> and a
/// condition's rules: the request each call sends, what it hands a screen, and
/// the phrase its failures start with. Every way an ask can fail is
/// <see cref="ApiAskTests"/>'s.
/// </summary>
public class VocabularyClientTests
{
    private const string Json = "application/json";

    private const string MasterLoinc =
        """{ "id": 4, "loinc": "11585-7", "conceptName": null, "conditionId": 42, "organismId": null, "organismFromResult": false }""";

    private const string Rule =
        """
        {
          "id": 9, "kind": "case_management", "executionOrder": 1, "allowNewCase": true,
          "allowUpdateCase": false, "isSurveillance": false,
          "conditions": [ { "field": "TestResult", "operator": "=", "value": "Positive", "andOr": null } ]
        }
        """;

    [Fact]
    public async Task ListAsksForTheTableAndReturnsItsRows()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{MasterLoinc}]");

        var asked = await client.List<VocabMasterLoincRow>(VocabularyClient.MasterLoincs);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/vocabulary/master-loincs", sent?.RequestUri?.ToString());
        var row = Assert.Single(asked.Value!);
        Assert.Equal(("11585-7", 42), (row.Loinc, row.ConditionId));
    }

    [Fact]
    public async Task CreatePostsToTheTable()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.Created, MasterLoinc);

        var asked = await client.Create(
            VocabularyClient.MasterLoincs, new VocabMasterLoincRow(0, "11585-7", null, 42, null, false));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/vocabulary/master-loincs", sent?.RequestUri?.ToString());
        Assert.Equal(4, asked.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheEntry()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, MasterLoinc);

        var asked = await client.Update(
            VocabularyClient.MasterLoincs, 4, new VocabMasterLoincRow(4, "11585-7", null, 42, null, false));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/vocabulary/master-loincs/4", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task DeleteDeletesTheEntry()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.NoContent, "");

        var failure = await client.Delete(VocabularyClient.ChildValues, 7);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/vocabulary/child-values/7", sent?.RequestUri?.ToString());
        Assert.Null(failure);
    }

    [Fact]
    public async Task ImportPostsTheCsvAsTextForAPreview()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = Recording(
            request =>
            {
                sent = request;
                body = request.Content!.ReadAsStringAsync().Result;
            },
            HttpStatusCode.OK,
            """
            { "applied": false, "added": [ { "line": 2, "key": "27268008" } ],
              "changed": [ { "line": 3, "key": "3092008" } ], "refused": [ { "line": 4, "reason": "no" } ] }
            """);

        var asked = await client.Import(
            VocabularyClient.Organisms, "snomed,snomed_alt,organism,condition_id\r\n", confirm: false);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/vocabulary/organisms/csv", sent?.RequestUri?.ToString());
        Assert.Equal("text/csv", sent?.Content?.Headers.ContentType?.MediaType);
        Assert.StartsWith("snomed,", body);
        Assert.False(asked.Value!.Applied);
        Assert.Equal("27268008", Assert.Single(asked.Value.Added).Key);
        Assert.Equal(3, Assert.Single(asked.Value.Changed).Line);
        Assert.Equal(4, Assert.Single(asked.Value.Refused).Line);
    }

    [Fact]
    public async Task ImportAsksForTheConfirmation()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(
            request => sent = request,
            HttpStatusCode.OK,
            """{ "applied": true, "added": [], "changed": [], "refused": [] }""");

        var asked = await client.Import(VocabularyClient.Organisms, "snomed\r\n", confirm: true);

        Assert.Equal(
            "http://api.example.test/api/vocabulary/organisms/csv?confirm=true", sent?.RequestUri?.ToString());
        Assert.True(asked.Value!.Applied);
    }

    [Fact]
    public async Task ExportReturnsTheCsvText()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, "category,concept,app_value\r\n");

        var asked = await client.Export(VocabularyClient.MasterValues);

        Assert.Equal("http://api.example.test/api/vocabulary/master-values/csv", sent?.RequestUri?.ToString());
        Assert.Equal("category,concept,app_value\r\n", asked.Value);
    }

    [Fact]
    public async Task RulesAsksForTheConditionsRules()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Rule}]");

        var asked = await client.Rules(42);

        Assert.Equal("http://api.example.test/api/conditions/42/rules", sent?.RequestUri?.ToString());
        var rule = Assert.Single(asked.Value!);
        Assert.Equal("case_management", rule.Kind);
        Assert.Equal("Positive", Assert.Single(rule.Conditions).Value);
    }

    [Fact]
    public async Task SaveRulesPutsTheWholeSet()
    {
        HttpRequestMessage? sent = null;
        var client = Recording(request => sent = request, HttpStatusCode.OK, $"[{Rule}]");

        var asked = await client.SaveRules(42, [new VocabRuleRow(0, "message_filter", 1, false, false, false, [])]);

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/conditions/42/rules", sent?.RequestUri?.ToString());
        Assert.Equal(9, Assert.Single(asked.Value!).Id);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefix()
    {
        var client = Recording(_ => { }, HttpStatusCode.InternalServerError, "{}");
        var row = new VocabMasterValueRow(0, "specimen", "Blood", null);

        Assert.StartsWith(VocabularyClient.LoadFailurePrefix,
            (await client.List<VocabMasterValueRow>(VocabularyClient.MasterValues)).Failure);
        Assert.StartsWith(VocabularyClient.LoadFailurePrefix, (await client.Rules(1)).Failure);
        Assert.StartsWith(VocabularyClient.SaveFailurePrefix,
            (await client.Create(VocabularyClient.MasterValues, row)).Failure);
        Assert.StartsWith(VocabularyClient.SaveFailurePrefix,
            (await client.Update(VocabularyClient.MasterValues, 1, row)).Failure);
        Assert.StartsWith(VocabularyClient.SaveFailurePrefix, await client.Delete(VocabularyClient.MasterValues, 1));
        Assert.StartsWith(VocabularyClient.SaveFailurePrefix, (await client.SaveRules(1, [])).Failure);
        Assert.StartsWith(VocabularyClient.ImportFailurePrefix,
            (await client.Import(VocabularyClient.MasterValues, "x", confirm: false)).Failure);
        Assert.StartsWith(VocabularyClient.ExportFailurePrefix,
            (await client.Export(VocabularyClient.MasterValues)).Failure);
    }

    private static VocabularyClient Recording(
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
