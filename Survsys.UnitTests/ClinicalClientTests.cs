using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's clinical data: the
/// URL each ask makes, the body a save sends, the value handed back, and the
/// phrase each of its two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the clinical data is read by its report's own URL, that the record
/// the tab filled in is the one sent, that the API tier's answer is what comes
/// back, and that a read that failed and a save that failed are told apart.
/// </remarks>
public class ClinicalClientTests
{
    private const string Json = "application/json";

    private const string Clinical =
        """
        {
          "reportId": 11,
          "diseaseOnsetDate": "2026-03-01",
          "dateDiagnosed": "2026-03-05",
          "treatmentGiven": "Yes",
          "deathStatus": "No",
          "dateOfDeath": null
        }
        """;

    [Fact]
    public async Task FindAsksTheReportsOwnClinicalUrlAndHandsTheDataBack()
    {
        HttpRequestMessage? sent = null;

        var client = new ClinicalClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Clinical);
        }));

        var found = await client.Find(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/clinical",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(found.Succeeded);
        Assert.Equal(11, found.Value?.ReportId);
        Assert.Equal(new DateOnly(2026, 3, 1), found.Value?.DiseaseOnsetDate);
        Assert.Equal(new DateOnly(2026, 3, 5), found.Value?.DateDiagnosed);
        Assert.Equal("Yes", found.Value?.TreatmentGiven);
        Assert.Equal("No", found.Value?.DeathStatus);
        Assert.Null(found.Value?.DateOfDeath);
    }

    [Fact]
    public async Task SaveSendsPutWithTheRequestAsItsBodyAndHandsTheAnswerBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ClinicalClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, Clinical);
        }));

        var saved = await client.Save(11, new UpdateClinicalRequest(
            DiseaseOnsetDate: new DateOnly(2026, 3, 1),
            DateDiagnosed: new DateOnly(2026, 3, 5),
            TreatmentGiven: "Yes",
            DeathStatus: "No",
            DateOfDeath: null));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/clinical",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"diseaseOnsetDate\":\"2026-03-01\"", body, StringComparison.Ordinal);
        Assert.Contains("\"dateDiagnosed\":\"2026-03-05\"", body, StringComparison.Ordinal);
        Assert.Contains("\"treatmentGiven\":\"Yes\"", body, StringComparison.Ordinal);
        Assert.Contains("\"deathStatus\":\"No\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(11, saved.Value?.ReportId);
    }

    [Fact]
    public async Task AReadThatFailedCarriesTheLoadPrefixAndNotTheApisBody()
    {
        var client = new ClinicalClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(ClinicalClient.LoadFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
        Assert.DoesNotContain("987654", found.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefixAndNotTheLoadOne()
    {
        var client = new ClinicalClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "deathStatus 'Maybe' is not an answer this endpoint takes." }""")));

        var saved = await client.Save(11, new UpdateClinicalRequest(
            null, null, null, DeathStatus: "Maybe", null));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(ClinicalClient.SaveFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("Maybe", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(ClinicalClient.LoadFailurePrefix, ClinicalClient.SaveFailurePrefix);
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
