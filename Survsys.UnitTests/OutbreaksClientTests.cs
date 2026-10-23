using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/outbreaks</c>: the URLs
/// it asks, the list and records it hands a screen, and the phrases its
/// failures start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What is this
/// client's own is small, which is the point of it being this small.
/// </remarks>
public class OutbreaksClientTests
{
    private const string Json = "application/json";

    private const string TwoOutbreaks =
        """
        {
          "items": [
            { "id": 2, "number": "2026-002", "name": "Summer fair", "outbreakDate": null,
              "description": null, "conditionId": null, "agencyId": null, "status": 0,
              "createdAt": "2026-06-01T09:00:00+00:00" },
            { "id": 1, "number": "2026-001", "name": "Spring picnic", "outbreakDate": "2026-03-01",
              "description": "A gathering", "conditionId": 4, "agencyId": 5, "status": 1,
              "createdAt": "2026-03-01T09:00:00+00:00" }
          ],
          "page": 1,
          "pageSize": 200,
          "totalCount": 2
        }
        """;

    private const string OneOutbreak =
        """
        {
          "id": 1, "number": "2026-001", "name": "Spring picnic", "outbreakDate": "2026-03-01",
          "description": "A gathering", "conditionId": 4, "conditionName": "Malaria",
          "agencyId": 5, "agencyName": "Salt Lake County", "status": 1,
          "createdAt": "2026-03-01T09:00:00+00:00", "access": []
        }
        """;

    [Fact]
    public async Task TheRequestAsksForTheOutbreaksInOneWidePage()
    {
        Uri? asked = null;

        var client = new OutbreaksClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoOutbreaks);
        }));

        await client.All();

        Assert.Equal("http://api.example.test/api/outbreaks?pageSize=200", asked?.ToString());
    }

    [Fact]
    public async Task TheOutbreaksTheApiReturnedAreTheOnesTheClientReturns()
    {
        var client = new OutbreaksClient(Transport(_ => Answer(HttpStatusCode.OK, TwoOutbreaks)));

        var asked = await client.All();

        Assert.True(asked.Succeeded);
        Assert.Equal([2, 1], asked.Value!.Items.Select(outbreak => outbreak.Id));
        Assert.Equal("Summer fair", asked.Value.Items[0].Name);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefix()
    {
        var client = new OutbreaksClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var asked = await client.All();

        Assert.False(asked.Succeeded);
        Assert.StartsWith(OutbreaksClient.FailurePrefix, asked.Failure);
        Assert.NotEqual(AgenciesClient.FailurePrefix, OutbreaksClient.FailurePrefix);
    }

    [Fact]
    public async Task FindAsksForTheOutbreakAndReturnsItWithItsReferencesNamed()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneOutbreak);
        }));

        var asked = await client.Find(1);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1", sent?.RequestUri?.ToString());
        Assert.Equal("Malaria", asked.Value!.ConditionName);
        Assert.Equal("Salt Lake County", asked.Value.AgencyName);
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneOutbreak);
        }));

        var asked = await client.Create(new SaveOutbreakRequest("Spring picnic"));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks", sent?.RequestUri?.ToString());
        Assert.Equal(1, asked.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheOutbreak()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneOutbreak);
        }));

        var asked = await client.Update(1, new SaveOutbreakRequest("Spring picnic"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new OutbreaksClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.Create(new SaveOutbreakRequest(""));

        Assert.StartsWith(OutbreaksClient.SaveFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AFindThatFailedCarriesTheFindPrefix()
    {
        var client = new OutbreaksClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.Find(99);

        Assert.StartsWith(OutbreaksClient.FindFailurePrefix, asked.Failure);
    }

    private const string TwoAccess =
        """[ { "agencyId": 3, "agencyName": "Davis County" }, { "agencyId": 5, "agencyName": "Salt Lake County" } ]""";

    private const string Sections =
        """[ { "id": 1, "description": "Context/Background" }, { "id": 2, "description": "Initiation of Investigation" } ]""";

    private const string Report =
        """[ { "reportSectionId": 1, "section": "Context/Background", "reportText": "Written." }, { "reportSectionId": 2, "section": "Initiation of Investigation", "reportText": null } ]""";

    private const string Activity =
        """[ { "id": 2, "createdAt": "2026-03-02T10:00:00+00:00", "userId": 1, "description": "Linked cases.", "type": 0 } ]""";

    private const string OneEntry =
        """{ "id": 3, "createdAt": "2026-03-03T10:00:00+00:00", "userId": 1, "description": "Noted.", "type": 0 }""";

    private const string Cases =
        """[ { "reportId": 7, "recordNumber": "202600004", "personName": "Rodriguez, Maria", "condition": "Malaria" } ]""";

    [Fact]
    public async Task AccessAsksForTheOutbreaksAccessAgencies()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, TwoAccess);
        }));

        var asked = await client.Access(1);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1/access", sent?.RequestUri?.ToString());
        Assert.Equal([3, 5], asked.Value!.Select(access => access.AgencyId));
    }

    [Fact]
    public async Task SetAccessPutsTheAgencies()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, TwoAccess);
        }));

        var asked = await client.SetAccess(1, new SetOutbreakAccessRequest([3, 5]));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1/access", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task ReportSectionsAsksForTheSections()
    {
        Uri? asked = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Sections);
        }));

        var sections = await client.ReportSections();

        Assert.Equal("http://api.example.test/api/report-sections", asked?.ToString());
        Assert.Equal(2, sections.Value!.Count);
    }

    [Fact]
    public async Task ReportAsksForTheOutbreaksReport()
    {
        Uri? asked = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Report);
        }));

        var report = await client.Report(1);

        Assert.Equal("http://api.example.test/api/outbreaks/1/report", asked?.ToString());
        Assert.Equal("Written.", report.Value![0].ReportText);
        Assert.Null(report.Value[1].ReportText);
    }

    [Fact]
    public async Task SaveReportSectionPutsToTheSection()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Report);
        }));

        var saved = await client.SaveReportSection(1, 2, new SaveOutbreakReportSectionRequest(4, "Text."));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1/report/2", sent?.RequestUri?.ToString());
        Assert.True(saved.Succeeded);
    }

    [Fact]
    public async Task ActivityAsksForTheLog()
    {
        Uri? asked = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Activity);
        }));

        var log = await client.Activity(1);

        Assert.Equal("http://api.example.test/api/outbreaks/1/activity", asked?.ToString());
        Assert.Equal("Linked cases.", Assert.Single(log.Value!).Description);
    }

    [Fact]
    public async Task AddActivityPostsAnEntry()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneEntry);
        }));

        var added = await client.AddActivity(1, new AddOutbreakUpdateRequest(1, "Noted.", 0));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1/activity", sent?.RequestUri?.ToString());
        Assert.Equal(3, added.Value!.Id);
    }

    [Fact]
    public async Task CasesAsksForTheLinkedCases()
    {
        Uri? asked = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Cases);
        }));

        var linked = await client.Cases(1);

        Assert.Equal("http://api.example.test/api/outbreaks/1/cases", asked?.ToString());
        Assert.Equal("Rodriguez, Maria", Assert.Single(linked.Value!).PersonName);
    }

    [Fact]
    public async Task DeleteSendsADeleteToTheOutbreak()
    {
        HttpRequestMessage? sent = null;
        var client = new OutbreaksClient(Transport(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        var why = await client.Delete(1);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/1", sent?.RequestUri?.ToString());
        Assert.Null(why);
    }

    [Fact]
    public async Task ADeleteThatFailedCarriesTheSavePrefix()
    {
        var client = new OutbreaksClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var why = await client.Delete(99);

        Assert.StartsWith(OutbreaksClient.SaveFailurePrefix, why);
    }

    [Fact]
    public async Task AnAccessLoadThatFailedCarriesTheAccessPrefix()
    {
        var client = new OutbreaksClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.Access(99);

        Assert.StartsWith(OutbreaksClient.AccessFailurePrefix, asked.Failure);
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
