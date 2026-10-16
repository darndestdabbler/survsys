using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's exposure events:
/// the URL each of its three asks makes, the body a record sends, the value
/// handed back, and the phrase each of its three failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that a list is read by its report's own URL with its page, that one
/// exposure is read by the exposure's own URL, that the exposure the form
/// filled in is the one posted, that the whole exposure the API answered is
/// what comes back, and that the three failures are told apart.
/// </remarks>
public class ExposuresClientTests
{
    private const string Json = "application/json";

    private const string Exposures =
        """
        {
          "items": [
            {
              "id": 31,
              "name": "Church supper",
              "description": "Potluck in the parish hall",
              "exposureStart": "2026-03-08",
              "exposureEnd": "2026-03-10",
              "exposurePlaceId": 41
            }
          ],
          "page": 1,
          "pageSize": 25,
          "totalCount": 1
        }
        """;

    private const string Detail =
        """
        {
          "id": 31,
          "personConditionId": 11,
          "conditionId": 7,
          "agencyId": 2,
          "name": "Church supper",
          "description": "Potluck in the parish hall",
          "exposureStart": "2026-03-08",
          "exposureEnd": "2026-03-10",
          "place": {
            "id": 41,
            "exposurePlaceTypeId": 3,
            "name": "St Mark's parish hall",
            "street": "12 Chapel St",
            "city": "Hartford",
            "zip": "06103",
            "state": "CT",
            "latitude": 41.763710,
            "longitude": -72.685097,
            "phone": null
          },
          "contacts": [
            {
              "id": 51,
              "exposureId": 31,
              "personId": 15,
              "exposureStart": "2026-03-08",
              "exposureEnd": null,
              "dispositionId": 4,
              "contactType": "household",
              "dispositionDate": "2026-03-12"
            }
          ],
          "sources": [
            {
              "id": 61,
              "exposureId": 31,
              "personId": null,
              "personConditionId": null,
              "sourceName": "Well water",
              "sourceType": "environmental"
            }
          ]
        }
        """;

    [Fact]
    public async Task PageAsksTheReportsExposuresUrlWithItsPageAndHandsTheListBack()
    {
        HttpRequestMessage? sent = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Exposures);
        }));

        var page = await client.Page(11, 1, 25);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/exposures?page=1&pageSize=25",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(page.Succeeded);
        Assert.Equal(1, page.Value?.TotalCount);
        var first = Assert.Single(page.Value!.Items);
        Assert.Equal(31, first.Id);
        Assert.Equal("Church supper", first.Name);
        Assert.Equal(new DateOnly(2026, 3, 8), first.ExposureStart);
        Assert.Equal(41, first.ExposurePlaceId);
    }

    [Fact]
    public async Task DetailAsksTheExposuresOwnUrlAndHandsItWhole()
    {
        HttpRequestMessage? sent = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Detail);
        }));

        var detail = await client.Detail(31);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposures/31", sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(detail.Succeeded);
        Assert.Equal(31, detail.Value?.Id);
        Assert.Equal(11, detail.Value?.PersonConditionId);
        Assert.Equal(7, detail.Value?.ConditionId);
        Assert.Equal("St Mark's parish hall", detail.Value?.Place?.Name);
        Assert.Equal(41.763710m, detail.Value?.Place?.Latitude);
        var contact = Assert.Single(detail.Value!.Contacts);
        Assert.Equal(15, contact.PersonId);
        Assert.Equal("household", contact.ContactType);
        Assert.Equal(4, contact.DispositionId);
        var source = Assert.Single(detail.Value!.Sources);
        Assert.Equal("Well water", source.SourceName);
        Assert.Null(source.PersonId);
    }

    [Fact]
    public async Task RecordPostsTheExposureAndHandsTheReturnedDetailBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.Created, Detail);
        }));

        var recorded = await client.Record(11, new CreateExposureRequest(
            Name: "Church supper",
            ExposureStart: new DateOnly(2026, 3, 8),
            ExposureEnd: new DateOnly(2026, 3, 10),
            Place: new ExposurePlaceRequest(Name: "St Mark's parish hall", State: "CT"),
            Contacts:
            [
                new ExposureContactRequest(PersonId: 15, ContactType: "household", DispositionId: 4),
            ],
            Sources: [new ExposureSourceRequest(SourceName: "Well water", SourceType: "environmental")]));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/exposures",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"name\":\"Church supper\"", body, StringComparison.Ordinal);
        Assert.Contains("\"exposureStart\":\"2026-03-08\"", body, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"CT\"", body, StringComparison.Ordinal);
        Assert.Contains("\"personId\":15", body, StringComparison.Ordinal);
        Assert.Contains("\"contactType\":\"household\"", body, StringComparison.Ordinal);
        Assert.Contains("\"dispositionId\":4", body, StringComparison.Ordinal);
        Assert.Contains("\"sourceName\":\"Well water\"", body, StringComparison.Ordinal);

        // The report, the condition and the agency are the server's to set
        // from the route, so the exposure itself never names them — a source
        // may name a report of its own, which is why the root is read and not
        // the whole body.
        using var posted = JsonDocument.Parse(body!);
        Assert.False(posted.RootElement.TryGetProperty("personConditionId", out _));
        Assert.False(posted.RootElement.TryGetProperty("conditionId", out _));
        Assert.False(posted.RootElement.TryGetProperty("agencyId", out _));

        Assert.True(recorded.Succeeded);
        Assert.Equal(31, recorded.Value?.Id);
        Assert.Single(recorded.Value!.Contacts);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefixAndNotTheApisBody()
    {
        var listFailed = await new ExposuresClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")))
            .Page(987654, 1, 25);

        Assert.False(listFailed.Succeeded);
        Assert.StartsWith(ExposuresClient.ListFailurePrefix, listFailed.Failure);
        Assert.Contains("404", listFailed.Failure);
        Assert.DoesNotContain("987654", listFailed.Failure, StringComparison.Ordinal);

        var findFailed = await new ExposuresClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No exposure has id 987654." }""")))
            .Detail(987654);

        Assert.False(findFailed.Succeeded);
        Assert.StartsWith(ExposuresClient.FindFailurePrefix, findFailed.Failure);
        Assert.Contains("404", findFailed.Failure);

        var recordFailed = await new ExposuresClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "exposureEnd 2026-03-08 is before exposureStart 2026-03-10." }""")))
            .Record(11, new CreateExposureRequest(Name: "Church supper"));

        Assert.False(recordFailed.Succeeded);
        Assert.StartsWith(ExposuresClient.RecordFailurePrefix, recordFailed.Failure);
        Assert.Contains("400", recordFailed.Failure);

        // The three sentences are three sentences.
        Assert.NotEqual(ExposuresClient.ListFailurePrefix, ExposuresClient.FindFailurePrefix);
        Assert.NotEqual(ExposuresClient.FindFailurePrefix, ExposuresClient.RecordFailurePrefix);
        Assert.NotEqual(ExposuresClient.ListFailurePrefix, ExposuresClient.RecordFailurePrefix);
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
