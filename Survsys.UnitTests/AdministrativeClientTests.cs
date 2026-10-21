using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's administrative
/// data: the URL each ask makes, the body a save sends, the value handed back,
/// and the phrase each of its two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the administrative data is read by its report's own URL, that the
/// record the tab filled in is the one sent, that the API tier's answer is
/// what comes back, and that a read that failed and a save that failed are
/// told apart. The values are the session-012 fill's: the three keys as
/// <c>11</c> and outbreak associated as the capitalised <c>Unknown</c>.
/// </remarks>
public class AdministrativeClientTests
{
    private const string Json = "application/json";

    private const string Administrative =
        """
        {
          "reportId": 12,
          "lhdCaseStatusId": 11,
          "stateCaseStatusId": 11,
          "outbreakAssociated": "Unknown",
          "lhdInvestigationStartDate": "2026-03-13",
          "lhdInvestigationCompletedDate": null,
          "investigationOutcomeDispositionId": 11,
          "eventName": null,
          "resultsReportedToClinicianDate": null,
          "cdcFirstVerballyNotifiedDate": null
        }
        """;

    [Fact]
    public async Task FindAsksTheReportsOwnAdministrativeUrlAndHandsTheDataBack()
    {
        HttpRequestMessage? sent = null;

        var client = new AdministrativeClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Administrative);
        }));

        var found = await client.Find(12);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/12/administrative",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(found.Succeeded);
        Assert.Equal(12, found.Value?.ReportId);
        Assert.Equal(11, found.Value?.LhdCaseStatusId);
        Assert.Equal(11, found.Value?.StateCaseStatusId);
        Assert.Equal("Unknown", found.Value?.OutbreakAssociated);
        Assert.Equal(new DateOnly(2026, 3, 13), found.Value?.LhdInvestigationStartDate);
        Assert.Null(found.Value?.LhdInvestigationCompletedDate);
        Assert.Equal(11, found.Value?.InvestigationOutcomeDispositionId);
        Assert.Null(found.Value?.EventName);
        Assert.Null(found.Value?.CdcFirstVerballyNotifiedDate);
    }

    [Fact]
    public async Task SaveSendsPutWithTheRequestAsItsBodyAndHandsTheAnswerBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new AdministrativeClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, Administrative);
        }));

        var saved = await client.Save(12, new UpdateAdministrativeRequest(
            LhdCaseStatusId: 11,
            StateCaseStatusId: 11,
            OutbreakAssociated: "Unknown",
            LhdInvestigationStartDate: new DateOnly(2026, 3, 13),
            LhdInvestigationCompletedDate: null,
            InvestigationOutcomeDispositionId: 11,
            EventName: "March cluster",
            ResultsReportedToClinicianDate: null,
            CdcFirstVerballyNotifiedDate: null));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/12/administrative",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"lhdCaseStatusId\":11", body, StringComparison.Ordinal);
        Assert.Contains("\"stateCaseStatusId\":11", body, StringComparison.Ordinal);
        Assert.Contains("\"outbreakAssociated\":\"Unknown\"", body, StringComparison.Ordinal);
        Assert.Contains("\"lhdInvestigationStartDate\":\"2026-03-13\"", body, StringComparison.Ordinal);
        Assert.Contains("\"investigationOutcomeDispositionId\":11", body, StringComparison.Ordinal);
        Assert.Contains("\"eventName\":\"March cluster\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(12, saved.Value?.ReportId);
    }

    [Fact]
    public async Task AReadThatFailedCarriesTheLoadPrefixAndNotTheApisBody()
    {
        var client = new AdministrativeClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(AdministrativeClient.LoadFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
        Assert.DoesNotContain("987654", found.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefixAndNotTheLoadOne()
    {
        var client = new AdministrativeClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "outbreakAssociated 'Maybe' is not an answer this endpoint takes." }""")));

        var saved = await client.Save(12, new UpdateAdministrativeRequest(
            null, null, OutbreakAssociated: "Maybe", null, null, null, null, null, null));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(AdministrativeClient.SaveFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("Maybe", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(AdministrativeClient.LoadFailurePrefix, AdministrativeClient.SaveFailurePrefix);
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
