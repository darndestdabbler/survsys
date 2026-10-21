using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a case's encounters: the URLs it
/// asks, the encounters it hands a screen, and the phrases its failures start
/// with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </remarks>
public class EncountersClientTests
{
    private const string Json = "application/json";

    private const string TwoEncounters =
        """
        [
          { "id": 1, "encounterDate": "2026-03-05", "encounterType": "OP", "facilityId": null,
            "facilityName": null, "description": "Follow-up" },
          { "id": 2, "encounterDate": "2026-03-02", "encounterType": "ED", "facilityId": 12,
            "facilityName": "General Hospital", "description": null }
        ]
        """;

    private const string OneEncounter =
        """
        { "id": 2, "encounterDate": "2026-03-02", "encounterType": "ED", "facilityId": 12,
          "facilityName": "General Hospital", "description": null }
        """;

    [Fact]
    public async Task RecordsAsksForTheCasesEncounters()
    {
        Uri? asked = null;
        var client = new EncountersClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoEncounters);
        }));

        var records = await client.Records(7);

        Assert.Equal("http://api.example.test/api/cases/7/encounters", asked?.ToString());
        Assert.Equal([1, 2], records.Value!.Select(encounter => encounter.Id));
        Assert.Equal("General Hospital", records.Value[1].FacilityName);
    }

    [Fact]
    public async Task RecordsFailureCarriesItsPrefix()
    {
        var client = new EncountersClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var records = await client.Records(7);

        Assert.False(records.Succeeded);
        Assert.StartsWith(EncountersClient.RecordsFailurePrefix, records.Failure);
    }

    [Fact]
    public async Task AddPostsToTheCase()
    {
        HttpRequestMessage? sent = null;
        var client = new EncountersClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneEncounter);
        }));

        var added = await client.Add(7, new SaveEncounterRequest(new DateOnly(2026, 3, 2), "ED", 12));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/7/encounters", sent?.RequestUri?.ToString());
        Assert.Equal(2, added.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheEncounter()
    {
        HttpRequestMessage? sent = null;
        var client = new EncountersClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneEncounter);
        }));

        var saved = await client.Update(2, new SaveEncounterRequest(new DateOnly(2026, 3, 2)));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/encounters/2", sent?.RequestUri?.ToString());
        Assert.True(saved.Succeeded);
    }

    [Fact]
    public async Task DeleteDeletesTheEncounter()
    {
        HttpRequestMessage? sent = null;
        var client = new EncountersClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.NoContent, "");
        }));

        var why = await client.Delete(2);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/encounters/2", sent?.RequestUri?.ToString());
        Assert.Null(why);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new EncountersClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var added = await client.Add(7, new SaveEncounterRequest(null));

        Assert.StartsWith(EncountersClient.SaveFailurePrefix, added.Failure);
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
