using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's writer of <c>POST /api/person-conditions</c>: what
/// it sends, what it hands the screen back, and the phrase its failures start
/// with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the report the screen filled in is the report that is sent, and
/// that the record number the API assigned is what comes back — because the
/// record number is the one thing the creation screen shows the user
/// afterwards.
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
