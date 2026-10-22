using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's investigation data:
/// the URL each ask makes, the body a save sends, the value handed back, and
/// the phrase each of its two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the investigation data is read by its report's own URL, that the
/// record the tab filled in is the one sent, that the API tier's answer is
/// what comes back, and that a read that failed and a save that failed are
/// told apart. The values are the session-012 fill's: the lowercase
/// <c>unknown</c>, imported from <c>11</c> and transmission mode <c>AB</c>.
/// </remarks>
public class InvestigationClientTests
{
    private const string Json = "application/json";

    private const string Investigation =
        """
        {
          "reportId": 12,
          "foodHandler": "unknown",
          "healthcareWorker": "unknown",
          "groupLiving": "unknown",
          "dayCareAssociation": "unknown",
          "importedFromId": 11,
          "transmissionMode": "AB",
          "riskFactors": null,
          "riskFactorsNotes": null,
          "otherData1": null,
          "otherData2": null
        }
        """;

    [Fact]
    public async Task FindAsksTheReportsOwnInvestigationUrlAndHandsTheDataBack()
    {
        HttpRequestMessage? sent = null;

        var client = new InvestigationClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Investigation);
        }));

        var found = await client.Find(12);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/12/investigation",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(found.Succeeded);
        Assert.Equal(12, found.Value?.ReportId);
        Assert.Equal("unknown", found.Value?.FoodHandler);
        Assert.Equal("unknown", found.Value?.HealthcareWorker);
        Assert.Equal("unknown", found.Value?.GroupLiving);
        Assert.Equal("unknown", found.Value?.DayCareAssociation);
        Assert.Equal(11, found.Value?.ImportedFromId);
        Assert.Equal("AB", found.Value?.TransmissionMode);
        Assert.Null(found.Value?.RiskFactors);
        Assert.Null(found.Value?.OtherData2);
    }

    [Fact]
    public async Task SaveSendsPutWithTheRequestAsItsBodyAndHandsTheAnswerBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new InvestigationClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, Investigation);
        }));

        var saved = await client.Save(12, new UpdateInvestigationRequest(
            FoodHandler: "unknown",
            HealthcareWorker: "unknown",
            GroupLiving: "unknown",
            DayCareAssociation: "unknown",
            ImportedFromId: 11,
            TransmissionMode: "AB",
            RiskFactors: "Travel",
            RiskFactorsNotes: null,
            OtherData1: null,
            OtherData2: null));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/12/investigation",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"foodHandler\":\"unknown\"", body, StringComparison.Ordinal);
        Assert.Contains("\"dayCareAssociation\":\"unknown\"", body, StringComparison.Ordinal);
        Assert.Contains("\"importedFromId\":11", body, StringComparison.Ordinal);
        Assert.Contains("\"transmissionMode\":\"AB\"", body, StringComparison.Ordinal);
        Assert.Contains("\"riskFactors\":\"Travel\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(12, saved.Value?.ReportId);
    }

    [Fact]
    public async Task AReadThatFailedCarriesTheLoadPrefixAndNotTheApisBody()
    {
        var client = new InvestigationClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(InvestigationClient.LoadFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
        Assert.DoesNotContain("987654", found.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefixAndNotTheLoadOne()
    {
        var client = new InvestigationClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "foodHandler 'Maybe' is not an answer this endpoint takes." }""")));

        var saved = await client.Save(12, new UpdateInvestigationRequest(
            FoodHandler: "Maybe", null, null, null, null, null, null, null, null, null));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(InvestigationClient.SaveFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("Maybe", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(InvestigationClient.LoadFailurePrefix, InvestigationClient.SaveFailurePrefix);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(
        HttpStatusCode status,
        string body,
        string mediaType = Json) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(mediaType) },
            },
        };

    private sealed class StubTransport(Func<HttpRequestMessage, HttpResponseMessage> answer)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }
}
