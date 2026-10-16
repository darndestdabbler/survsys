using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's laboratory
/// specimens: the URL each of its three asks makes, the body a record sends,
/// the value handed back, and the phrase each of its three failures starts
/// with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that a list is read by its report's own URL with its page, that one
/// specimen is read by the specimen's own URL, that the specimen the form
/// filled in is the one posted, that the whole specimen the API answered is
/// what comes back, and that the three failures are told apart.
/// </remarks>
public class LabsClientTests
{
    private const string Json = "application/json";

    private const string Specimens =
        """
        {
          "items": [
            {
              "id": 5,
              "personId": 13,
              "personConditionId": 11,
              "labFacilityId": 2,
              "providerId": null,
              "diagnosticFacilityId": null,
              "reportingFacilityId": null,
              "specimenSourceId": 7,
              "collectionDate": "2026-03-02",
              "accessionNo": "ACC-1",
              "specimenSentToState": true
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
          "specimen": {
            "id": 5,
            "personId": 13,
            "personConditionId": 11,
            "labFacilityId": 2,
            "providerId": null,
            "diagnosticFacilityId": null,
            "reportingFacilityId": null,
            "specimenSourceId": 7,
            "collectionDate": "2026-03-02",
            "accessionNo": "ACC-1",
            "specimenSentToState": true
          },
          "tests": [
            {
              "id": 9,
              "labId": 5,
              "testTypeId": 3,
              "testStatusId": 1,
              "loincCode": "12345-6",
              "referenceRange": "negative",
              "labTestDate": "2026-03-03",
              "results": [
                {
                  "id": 14,
                  "labTestId": 9,
                  "organismId": 42,
                  "resultValue": "positive",
                  "testResult": "POS",
                  "comment": null,
                  "units": null,
                  "primaryLabResult": true
                }
              ]
            }
          ],
          "resistTests": [
            {
              "id": 21,
              "labId": 5,
              "organismId": 42,
              "resistTestAgentId": 8,
              "resistTestResultId": 2,
              "testDate": "2026-03-04"
            }
          ]
        }
        """;

    [Fact]
    public async Task PageAsksTheReportsLabsUrlWithItsPageAndHandsTheListBack()
    {
        HttpRequestMessage? sent = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Specimens);
        }));

        var page = await client.Page(11, 1, 25);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/labs?page=1&pageSize=25",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(page.Succeeded);
        Assert.Equal(1, page.Value?.TotalCount);
        var first = Assert.Single(page.Value!.Items);
        Assert.Equal(5, first.Id);
        Assert.Equal("ACC-1", first.AccessionNo);
        Assert.Equal(7, first.SpecimenSourceId);
    }

    [Fact]
    public async Task DetailAsksTheSpecimensOwnUrlAndHandsItWhole()
    {
        HttpRequestMessage? sent = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Detail);
        }));

        var detail = await client.Detail(5);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/labs/5", sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(detail.Succeeded);
        Assert.Equal(5, detail.Value?.Specimen.Id);
        var test = Assert.Single(detail.Value!.Tests);
        Assert.Equal(9, test.Id);
        var result = Assert.Single(test.Results);
        Assert.True(result.PrimaryLabResult);
        var resist = Assert.Single(detail.Value!.ResistTests);
        Assert.Equal(8, resist.ResistTestAgentId);
    }

    [Fact]
    public async Task RecordPostsTheSpecimenAndHandsTheReturnedDetailBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.Created, Detail);
        }));

        var recorded = await client.Record(11, new CreateLabRequest(
            SpecimenSourceId: 7,
            CollectionDate: new DateOnly(2026, 3, 2),
            AccessionNo: "ACC-1",
            SpecimenSentToState: true,
            Tests:
            [
                new CreateLabTestRequest(
                    TestTypeId: 3,
                    Results: [new CreateLabTestResultRequest(TestResult: "POS", PrimaryLabResult: true)]),
            ],
            ResistTests:
            [
                new CreateLabResistTestRequest(OrganismId: 42, ResistTestAgentId: 8),
            ]));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/labs",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"accessionNo\":\"ACC-1\"", body, StringComparison.Ordinal);
        Assert.Contains("\"specimenSourceId\":7", body, StringComparison.Ordinal);
        Assert.Contains("\"testTypeId\":3", body, StringComparison.Ordinal);
        Assert.Contains("\"primaryLabResult\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"resistTestAgentId\":8", body, StringComparison.Ordinal);

        Assert.True(recorded.Succeeded);
        Assert.Equal(5, recorded.Value?.Specimen.Id);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefixAndNotTheApisBody()
    {
        var listFailed = await new LabsClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")))
            .Page(987654, 1, 25);

        Assert.False(listFailed.Succeeded);
        Assert.StartsWith(LabsClient.ListFailurePrefix, listFailed.Failure);
        Assert.Contains("404", listFailed.Failure);
        Assert.DoesNotContain("987654", listFailed.Failure, StringComparison.Ordinal);

        var findFailed = await new LabsClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No laboratory specimen has id 987654." }""")))
            .Detail(987654);

        Assert.False(findFailed.Succeeded);
        Assert.StartsWith(LabsClient.FindFailurePrefix, findFailed.Failure);
        Assert.Contains("404", findFailed.Failure);

        var recordFailed = await new LabsClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "a test has 2 results marked the primary one." }""")))
            .Record(11, new CreateLabRequest(AccessionNo: "ACC-1"));

        Assert.False(recordFailed.Succeeded);
        Assert.StartsWith(LabsClient.RecordFailurePrefix, recordFailed.Failure);
        Assert.Contains("400", recordFailed.Failure);

        // The three sentences are three sentences.
        Assert.NotEqual(LabsClient.ListFailurePrefix, LabsClient.FindFailurePrefix);
        Assert.NotEqual(LabsClient.FindFailurePrefix, LabsClient.RecordFailurePrefix);
        Assert.NotEqual(LabsClient.ListFailurePrefix, LabsClient.RecordFailurePrefix);
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
