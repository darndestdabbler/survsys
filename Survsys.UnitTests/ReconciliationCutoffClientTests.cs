using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of the reconciliation cutoff: what
/// it asks the API tier for, what it hands a screen back, and the phrase each
/// of its failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is each ask's URL and method, the cutoff in the body of a save, and that
/// each failure begins with its own prefix and names the status rather than
/// the body.
/// </remarks>
public class ReconciliationCutoffClientTests
{
    private const string Json = "application/json";

    /// <summary>Finding the cutoff: a GET of its URL, and the year and week come back.</summary>
    [Fact]
    public async Task FindReadsTheCutoff()
    {
        HttpRequestMessage? sent = null;
        var client = new ReconciliationCutoffClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, """{ "mmwrYear": 2026, "mmwrWeek": 10 }""");
        }));

        var found = await client.Find();

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/reconciliation-cutoff", sent?.RequestUri?.ToString());
        Assert.Equal(new ReconciliationCutoffResponse(2026, 10), found.Value);
    }

    /// <summary>
    /// Saving the cutoff: a PUT of the year and week to its URL; a refused week
    /// carries the save prefix and names the status rather than the body.
    /// </summary>
    [Fact]
    public async Task SetPutsTheYearAndWeek()
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        var client = new ReconciliationCutoffClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, """{ "mmwrYear": 2026, "mmwrWeek": 12 }""");
        }));

        var stored = await client.Set(2026, 12);

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/reconciliation-cutoff", sent?.RequestUri?.ToString());
        Assert.Contains("\"mmwrWeek\":12", body, StringComparison.Ordinal);
        Assert.Equal(12, stored.Value?.MmwrWeek);

        var failing = new ReconciliationCutoffClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest, """{ "status": 400, "detail": "Week 54 is not an MMWR week." }""")));

        var refused = await failing.Set(2026, 54);

        Assert.StartsWith(ReconciliationCutoffClient.SaveFailurePrefix, refused.Failure);
        Assert.Contains("400", refused.Failure);
        Assert.DoesNotContain("Week 54", refused.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reading the totals: a GET of the totals URL, and the rows come back; a
    /// failure carries the totals prefix, and a missing cutoff the find prefix.
    /// </summary>
    [Fact]
    public async Task TotalsReadsTheVerificationTotals()
    {
        HttpRequestMessage? sent = null;
        var client = new ReconciliationCutoffClient(Transport(request =>
        {
            sent = request;
            return Answer(
                HttpStatusCode.OK,
                """[{ "condition": "Malaria", "cdcCode": "10130", "mmwrYear": 2026, "count": 2 }]""");
        }));

        var totals = await client.Totals();

        Assert.Equal("http://api.example.test/api/reconciliation-cutoff/totals", sent?.RequestUri?.ToString());
        Assert.Equal(new ReconciliationTotalResponse("Malaria", "10130", 2026, 2), Assert.Single(totals.Value!));

        var failing = new ReconciliationCutoffClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));
        Assert.StartsWith(ReconciliationCutoffClient.TotalsFailurePrefix, (await failing.Totals()).Failure);
        Assert.StartsWith(ReconciliationCutoffClient.FindFailurePrefix, (await failing.Find()).Failure);
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
