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

    /// <summary>
    /// A report with more exposures than one page holds is read whole: a
    /// first page of a full 200 and a second page of one more are both asked
    /// for, in order, and all 201 come back — the exposure after the first
    /// page is not silently left off.
    /// </summary>
    [Fact]
    public async Task AllWalksEveryPageUntilTheTotalHasArrived()
    {
        var asked = new List<string>();

        var client = new ExposuresClient(Transport(request =>
        {
            asked.Add(request.RequestUri!.PathAndQuery);

            return request.RequestUri.Query.Contains("page=1", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, PageOf(Enumerable.Range(1, 200), page: 1, total: 201))
                : Answer(HttpStatusCode.OK, PageOf([201], page: 2, total: 201));
        }));

        var all = await client.All(11);

        Assert.Equal(
            [
                "/api/person-conditions/11/exposures?page=1&pageSize=200",
                "/api/person-conditions/11/exposures?page=2&pageSize=200",
            ],
            asked);

        Assert.True(all.Succeeded);
        Assert.Equal(201, all.Value!.Count);
        Assert.Equal(Enumerable.Range(1, 201), all.Value.Select(exposure => exposure.Id));
    }

    /// <summary>
    /// A report whose exposures fit in one page is read with one ask: the
    /// total says there is no second page, so none is asked for.
    /// </summary>
    [Fact]
    public async Task AllStopsAfterOnePageWhenTheTotalFitsInIt()
    {
        var asks = 0;

        var client = new ExposuresClient(Transport(_ =>
        {
            asks++;
            return Answer(HttpStatusCode.OK, PageOf([31, 32], page: 1, total: 2));
        }));

        var all = await client.All(11);

        Assert.Equal(1, asks);
        Assert.True(all.Succeeded);
        Assert.Equal([31, 32], all.Value!.Select(exposure => exposure.Id));
    }

    /// <summary>
    /// A page that fails fails the whole, in the list's own words: a list
    /// with a hole in it is not the list.
    /// </summary>
    [Fact]
    public async Task AllFailsAsTheListWhenAnyPageFails()
    {
        var client = new ExposuresClient(Transport(request =>
            request.RequestUri!.Query.Contains("page=1", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, PageOf(Enumerable.Range(1, 200), page: 1, total: 201))
                : Answer(HttpStatusCode.InternalServerError, """{ "status": 500 }""")));

        var all = await client.All(11);

        Assert.False(all.Succeeded);
        Assert.StartsWith(ExposuresClient.ListFailurePrefix, all.Failure);
        Assert.Contains("500", all.Failure);
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

    [Fact]
    public async Task UpdateExposurePutsTheExposuresUrlWithTheBodyAndHandsItBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":31,"name":"Parish hall potluck","description":"Corrected","exposureStart":"2026-03-08","exposureEnd":"2026-03-10","exposurePlaceId":41}""");
        }));

        var saved = await client.UpdateExposure(31, new UpdateExposureRequest(
            Name: "Parish hall potluck",
            Description: "Corrected",
            ExposureStart: new DateOnly(2026, 3, 8),
            ExposureEnd: new DateOnly(2026, 3, 10)));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposures/31", sent?.RequestUri?.ToString());
        Assert.Contains("\"name\":\"Parish hall potluck\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("Parish hall potluck", saved.Value?.Name);
    }

    [Fact]
    public async Task UpdatePlacePutsThePlacesOwnUrlWithTheBody()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":41,"exposurePlaceTypeId":3,"name":"St Mark's parish hall","street":"12 Chapel St","city":"Hartford","zip":null,"state":"CT","latitude":null,"longitude":null,"phone":null}""");
        }));

        var saved = await client.UpdatePlace(41, new ExposurePlaceRequest(
            ExposurePlaceTypeId: 3, Name: "St Mark's parish hall", Street: "12 Chapel St",
            City: "Hartford", State: "CT"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposure-places/41", sent?.RequestUri?.ToString());
        Assert.Contains("\"street\":\"12 Chapel St\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("12 Chapel St", saved.Value?.Street);
    }

    [Fact]
    public async Task UpdateContactPutsTheContactsOwnUrlWithItsDisposition()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":51,"exposureId":31,"personId":15,"exposureStart":null,"exposureEnd":null,"dispositionId":4,"contactType":"household","dispositionDate":"2026-03-12"}""");
        }));

        var saved = await client.UpdateContact(51, new ExposureContactRequest(
            PersonId: 15, DispositionId: 4, ContactType: "household",
            DispositionDate: new DateOnly(2026, 3, 12)));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposure-contacts/51", sent?.RequestUri?.ToString());
        Assert.Contains("\"dispositionId\":4", body, StringComparison.Ordinal);
        Assert.Contains("\"dispositionDate\":\"2026-03-12\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(4, saved.Value?.DispositionId);
    }

    [Fact]
    public async Task UpdateSourcePutsTheSourcesOwnUrlWithTheBody()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"id":61,"exposureId":31,"personId":null,"personConditionId":null,"sourceName":"Well water","sourceType":"environmental"}""");
        }));

        var saved = await client.UpdateSource(61, new ExposureSourceRequest(
            SourceName: "Well water", SourceType: "environmental"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposure-sources/61", sent?.RequestUri?.ToString());
        Assert.Contains("\"sourceName\":\"Well water\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("Well water", saved.Value?.SourceName);
    }

    [Fact]
    public async Task DeleteEachRowDeletesItsOwnUrlAndAnswersNullWhenRemoved()
    {
        var urls = new List<string?>();

        var client = new ExposuresClient(Transport(request =>
        {
            urls.Add(request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        Assert.Null(await client.DeleteExposure(31));
        Assert.Null(await client.DeletePlace(41));
        Assert.Null(await client.DeleteContact(51));
        Assert.Null(await client.DeleteSource(61));

        Assert.Equal(
            [
                "http://api.example.test/api/exposures/31",
                "http://api.example.test/api/exposure-places/41",
                "http://api.example.test/api/exposure-contacts/51",
                "http://api.example.test/api/exposure-sources/61",
            ],
            urls);
    }

    [Fact]
    public async Task ADependentDeleteRefusedCarriesTheRemovePrefixAndTheStatusNotTheBody()
    {
        var failure = await new ExposuresClient(Transport(_ => Answer(
                HttpStatusCode.Conflict,
                """{ "status": 409, "detail": "exposure 31 still has 1 contact recorded against it and cannot be removed." }""")))
            .DeleteExposure(31);

        Assert.NotNull(failure);
        Assert.StartsWith(ExposuresClient.RemoveFailurePrefix, failure);
        Assert.Contains("409", failure);
        Assert.DoesNotContain("still has", failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveRefusedCarriesTheSavePrefixAndTheStatusNotTheBody()
    {
        var saved = await new ExposuresClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "personId 987654 is not a person on file." }""")))
            .UpdateContact(51, new ExposureContactRequest(PersonId: 987654));

        Assert.False(saved.Succeeded);
        Assert.StartsWith(ExposuresClient.SaveFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("987654", saved.Failure!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddContactEventPostsTheExposuresContactEventsUrlWithTheNewPerson()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.Created,
                """{"id":51,"exposureId":31,"personId":15,"exposureStart":null,"exposureEnd":null,"dispositionId":null,"contactType":"household","dispositionDate":null,"personConditionId":62}""");
        }));

        var added = await client.AddContactEvent(31, new CreateContactEventRequest(
            NewPerson: new CreatePersonRequest("Ortiz", "Ana"), ContactType: "household"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/exposures/31/contact-events", sent?.RequestUri?.ToString());
        Assert.Contains("\"lastName\":\"Ortiz\"", body, StringComparison.Ordinal);
        Assert.Contains("\"contactType\":\"household\"", body, StringComparison.Ordinal);

        Assert.True(added.Succeeded);
        Assert.Equal(62, added.Value?.PersonConditionId);
    }

    [Fact]
    public async Task AddContactEventThatWasRefusedCarriesTheAddContactPrefix()
    {
        var client = new ExposuresClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest, """{"status":400,"detail":"Name the contact exactly one way."}""")));

        var added = await client.AddContactEvent(31, new CreateContactEventRequest());

        Assert.False(added.Succeeded);
        Assert.StartsWith(ExposuresClient.AddContactFailurePrefix, added.Failure);
        Assert.Contains("400", added.Failure);
    }

    /// <summary>
    /// The import: a POST of the CSV and the exposure to the report's import
    /// URL, and the saved and refused rows are both what the screen gets.
    /// </summary>
    [Fact]
    public async Task ImportContactsPostsTheBatchAndHandsBackTheSavedAndRefusedRows()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new ExposuresClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK,
                """{"accepted":[{"line":2,"firstName":null,"middleName":null,"lastName":"Ortiz","birthDate":"1990-04-02","sex":null,"street":null,"unit":null,"city":null,"state":null,"zip":null,"county":null,"phone":null,"email":null,"personConditionId":62}],"rejected":[{"line":3,"reason":"the last name is missing"}]}""");
        }));

        var imported = await client.ImportContacts(11,
            new ImportContactsRequest("last_name,birth_date\nOrtiz,1990-04-02\n,1990-04-02", 31));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/person-conditions/11/contacts/import",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"exposureId\":31", body, StringComparison.Ordinal);
        Assert.Contains("\"csv\":", body, StringComparison.Ordinal);

        Assert.True(imported.Succeeded);
        Assert.Equal(62, Assert.Single(imported.Value!.Accepted).PersonConditionId);
        Assert.Equal("the last name is missing", Assert.Single(imported.Value.Rejected).Reason);
    }

    [Fact]
    public async Task ImportContactsThatWasRefusedCarriesTheImportPrefix()
    {
        var client = new ExposuresClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest, """{"status":400,"detail":"exposureId is required."}""")));

        var imported = await client.ImportContacts(11, new ImportContactsRequest("last_name,birth_date"));

        Assert.False(imported.Succeeded);
        Assert.StartsWith(ExposuresClient.ImportFailurePrefix, imported.Failure);
        Assert.Contains("400", imported.Failure);
    }

    /// <summary>
    /// One page of bare exposures with the given ids, as the API tier writes
    /// it, carrying the page number and the total the client reads to decide
    /// whether to ask for another.
    /// </summary>
    private static string PageOf(IEnumerable<int> ids, int page, int total)
    {
        var items = string.Join(",", ids.Select(id =>
            $$"""{"id":{{id}},"name":null,"description":null,"exposureStart":null,"exposureEnd":null,"exposurePlaceId":null}"""));

        return $$"""{"items":[{{items}}],"page":{{page}},"pageSize":200,"totalCount":{{total}}}""";
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
