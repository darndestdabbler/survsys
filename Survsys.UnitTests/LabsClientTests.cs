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

    [Fact]
    public async Task UpdateSpecimenPutsTheSpecimensUrlWithTheBodyAndHandsItBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":5,"personId":13,"personConditionId":11,"labFacilityId":2,"providerId":null,"diagnosticFacilityId":null,"reportingFacilityId":null,"specimenSourceId":7,"collectionDate":"2026-03-02","accessionNo":"ACC-2","specimenSentToState":true}""");
        }));

        var saved = await client.UpdateSpecimen(5, new UpdateLabRequest(
            LabFacilityId: 2, SpecimenSourceId: 7, AccessionNo: "ACC-2", SpecimenSentToState: true));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/labs/5", sent?.RequestUri?.ToString());
        Assert.Contains("\"accessionNo\":\"ACC-2\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("ACC-2", saved.Value?.AccessionNo);
    }

    [Fact]
    public async Task UpdateTestPutsTheTestsOwnUrlWithTheBody()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":9,"labId":5,"testTypeId":4,"testStatusId":2,"loincCode":"1988-5","referenceRange":null,"labTestDate":null,"results":[]}""");
        }));

        var saved = await client.UpdateTest(9, new UpdateLabTestRequest(TestTypeId: 4, TestStatusId: 2, LoincCode: "1988-5"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/lab-tests/9", sent?.RequestUri?.ToString());
        Assert.Contains("\"loincCode\":\"1988-5\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(4, saved.Value?.TestTypeId);
    }

    [Fact]
    public async Task UpdateResultPutsTheResultsOwnUrlWithTheBody()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":14,"labTestId":9,"organismId":5,"resultValue":"1200","testResult":"POS","comment":null,"units":"U/L","primaryLabResult":true}""");
        }));

        var saved = await client.UpdateResult(14, new CreateLabTestResultRequest(
            OrganismId: 5, ResultValue: "1200", TestResult: "POS", Units: "U/L", PrimaryLabResult: true));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/lab-test-results/14", sent?.RequestUri?.ToString());
        Assert.Contains("\"resultValue\":\"1200\"", body, StringComparison.Ordinal);
        Assert.Contains("\"primaryLabResult\":true", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("1200", saved.Value?.ResultValue);
    }

    [Fact]
    public async Task UpdateResistPutsTheSusceptibilitysOwnUrlWithTheBody()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":21,"labId":5,"organismId":5,"resistTestAgentId":8,"resistTestResultId":2,"testDate":"2026-03-11"}""");
        }));

        var saved = await client.UpdateResist(21, new CreateLabResistTestRequest(
            OrganismId: 5, ResistTestAgentId: 8, ResistTestResultId: 2, TestDate: new DateOnly(2026, 3, 11)));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/lab-resist-tests/21", sent?.RequestUri?.ToString());
        Assert.Contains("\"resistTestAgentId\":8", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(8, saved.Value?.ResistTestAgentId);
    }

    [Fact]
    public async Task DeleteSpecimenDeletesItsOwnUrlAndAnswersNullWhenRemoved()
    {
        HttpRequestMessage? sent = null;

        var client = new LabsClient(Transport(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        var failure = await client.DeleteSpecimen(5);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/labs/5", sent?.RequestUri?.ToString());
        Assert.Null(failure);
    }

    [Fact]
    public async Task DeleteTestAndResultAndResistEachDeleteTheirOwnUrl()
    {
        var urls = new List<string?>();

        var client = new LabsClient(Transport(request =>
        {
            urls.Add(request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        await client.DeleteTest(9);
        await client.DeleteResult(14);
        await client.DeleteResist(21);

        Assert.Equal(
            [
                "http://api.example.test/api/lab-tests/9",
                "http://api.example.test/api/lab-test-results/14",
                "http://api.example.test/api/lab-resist-tests/21",
            ],
            urls);
    }

    [Fact]
    public async Task ADependentDeleteRefusedCarriesTheRemovePrefixAndTheStatusNotTheBody()
    {
        var failure = await new LabsClient(Transport(_ => Answer(
                HttpStatusCode.Conflict,
                """{ "status": 409, "detail": "laboratory specimen 5 still has 1 test recorded against it and cannot be removed." }""")))
            .DeleteSpecimen(5);

        Assert.NotNull(failure);
        Assert.StartsWith(LabsClient.RemoveFailurePrefix, failure);
        Assert.Contains("409", failure);
        Assert.DoesNotContain("still has", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveRefusedCarriesTheSavePrefixAndTheStatusNotTheBody()
    {
        var saved = await new LabsClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "another result on this test is already the primary one." }""")))
            .UpdateResult(14, new CreateLabTestResultRequest(PrimaryLabResult: true));

        Assert.False(saved.Succeeded);
        Assert.StartsWith(LabsClient.SaveFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("primary", saved.Failure!, StringComparison.Ordinal);
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
