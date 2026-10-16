using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's writer and reader of <c>/api/person-conditions</c>:
/// what it sends, what it hands the screen back, and the phrase each of its
/// two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the report the screen filled in is the report that is sent, that
/// the record number the API assigned is what comes back — because the record
/// number is what the report shell shows at the top — and that a report read
/// by its identifier is the report the shell gets.
/// </remarks>
public class PersonConditionsClientTests
{
    private const string Json = "application/json";

    private const string Opened =
        """
        {
          "id": 11,
          "personId": 13,
          "conditionId": 74,
          "recordNumber": "202600001",
          "agencyId": 1,
          "firstReportedPhDate": "2026-03-12",
          "eventOnsetDate": "2026-03-12",
          "createdAt": "2026-03-12T22:07:16-06:00"
        }
        """;

    [Fact]
    public async Task TheReportFilledInIsTheReportSent()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, Opened);
        }));

        await client.Open(new CreatePersonConditionRequest(
            PersonId: 13,
            ConditionId: 74,
            AgencyId: 1,
            FirstReportedPhDate: new DateOnly(2026, 3, 12)));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/person-conditions", sent?.RequestUri?.ToString());

        Assert.Contains("\"personId\":13", body, StringComparison.Ordinal);
        Assert.Contains("\"conditionId\":74", body, StringComparison.Ordinal);
        Assert.Contains("\"agencyId\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"firstReportedPhDate\":\"2026-03-12\"", body, StringComparison.Ordinal);

        // The record number is the other tier's to assign, so it is not in
        // what this one sends.
        Assert.DoesNotContain("recordNumber", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReportOpenedComesBackWithItsRecordNumber()
    {
        var client = new PersonConditionsClient(
            Transport(_ => Answer(HttpStatusCode.Created, Opened)));

        var opened = await client.Open(new CreatePersonConditionRequest(13, 74, 1));

        Assert.True(opened.Succeeded);
        Assert.Equal("202600001", opened.Value?.RecordNumber);
        Assert.Equal(11, opened.Value?.Id);
        Assert.Equal(13, opened.Value?.PersonId);
        Assert.Equal(74, opened.Value?.ConditionId);
        Assert.Equal(1, opened.Value?.AgencyId);
        Assert.Equal(new DateOnly(2026, 3, 12), opened.Value?.EventOnsetDate);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefixAndNotTheApisBody()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "conditionId 987654 is not a condition on file." }""")));

        var opened = await client.Open(new CreatePersonConditionRequest(13, 987654));

        Assert.False(opened.Succeeded);
        Assert.Null(opened.Value);
        Assert.StartsWith(PersonConditionsClient.FailurePrefix, opened.Failure);
        Assert.Contains("400", opened.Failure);
        Assert.DoesNotContain("987654", opened.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// The read the report shell makes first: the report's own URL, and the
    /// report back — whose person and condition the shell then reads from
    /// their own resources.
    /// </summary>
    [Fact]
    public async Task TheReportFoundIsTheReportTheClientReturns()
    {
        HttpRequestMessage? sent = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Opened);
        }));

        var found = await client.Find(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/person-conditions/11", sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(found.Succeeded);
        Assert.Equal(11, found.Value?.Id);
        Assert.Equal(13, found.Value?.PersonId);
        Assert.Equal(74, found.Value?.ConditionId);
        Assert.Equal("202600001", found.Value?.RecordNumber);
        Assert.Equal(1, found.Value?.AgencyId);
        Assert.Equal(new DateOnly(2026, 3, 12), found.Value?.FirstReportedPhDate);
        Assert.Equal(new DateOnly(2026, 3, 12), found.Value?.EventOnsetDate);
    }

    /// <summary>
    /// An identifier nobody has: the API tier answers 404, and what the shell
    /// gets is a message about loading — not about creating, which is the
    /// other thing this client does — rather than an exception.
    /// </summary>
    [Fact]
    public async Task AReportNobodyHasBecomesAMessageAboutLoading()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(PersonConditionsClient.FindFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
        Assert.DoesNotContain("987654", found.Failure, StringComparison.Ordinal);
        Assert.NotEqual(PersonConditionsClient.FailurePrefix, PersonConditionsClient.FindFailurePrefix);
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
