using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader of the ad-hoc reports: what it asks the API
/// tier for, what it hands the screen back, and the phrase its failures start
/// with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the grouping is always in the query string and a date only when one
/// is given, that the counts come back with a null group kept as null, and that
/// a failure begins with this client's prefix and names the status rather than
/// the body.
/// </remarks>
public class ReportsClientTests
{
    private const string Json = "application/json";

    private const string Counts =
        """
        [
          { "group": "Malaria", "count": 3 },
          { "group": null, "count": 1 }
        ]
        """;

    [Fact]
    public async Task CaseCountsAsksForTheGroupingAloneWhenNoDatesAreGiven()
    {
        HttpRequestMessage? sent = null;

        var client = new ReportsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Counts);
        }));

        var counts = await client.CaseCounts(ReportGroupings.Condition, null, null);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/reports/case-counts?groupBy=condition",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(counts.Succeeded);
        Assert.Equal(new CaseCountResponse("Malaria", 3), counts.Value?[0]);
        Assert.Equal(new CaseCountResponse(null, 1), counts.Value?[1]);
    }

    [Fact]
    public async Task CaseCountsCarriesTheDatesGiven()
    {
        HttpRequestMessage? sent = null;

        var client = new ReportsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, "[]");
        }));

        await client.CaseCounts(ReportGroupings.Agency, new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));
        Assert.Equal(
            "http://api.example.test/api/reports/case-counts?groupBy=agency&from=2026-03-01&to=2026-03-31",
            sent?.RequestUri?.ToString());

        await client.CaseCounts(ReportGroupings.Agency, null, new DateOnly(2026, 3, 31));
        Assert.Equal(
            "http://api.example.test/api/reports/case-counts?groupBy=agency&to=2026-03-31",
            sent?.RequestUri?.ToString());
    }

    [Fact]
    public async Task AReportThatCouldNotBeRunCarriesThePrefix()
    {
        var client = new ReportsClient(Transport(_ => Answer(
            HttpStatusCode.Forbidden,
            """{ "status": 403, "detail": "run_reports is required." }""")));

        var counts = await client.CaseCounts(ReportGroupings.Condition, null, null);

        Assert.False(counts.Succeeded);
        Assert.Null(counts.Value);
        Assert.StartsWith(ReportsClient.FailurePrefix, counts.Failure);
        Assert.Contains("403", counts.Failure);
        Assert.DoesNotContain("run_reports is required", counts.Failure, StringComparison.Ordinal);
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(Json) },
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
