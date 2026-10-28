using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's notes: the URL each
/// of its three asks makes, the body a record and a strikethrough send, the
/// value handed back, and the phrase each of its three failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that a list is read by its report's own URL with its page, that the note
/// the form filled in is the one posted, that a struck-through note is still
/// in the list the client hands back, that the strikethrough is a PUT to the
/// note's own URL carrying the one flag, and that the three failures are told
/// apart.
/// </remarks>
public class NotesClientTests
{
    private const string Json = "application/json";

    private const string Notes =
        """
        {
          "items": [
            {
              "id": 11,
              "personConditionId": 14,
              "exposureId": null,
              "facilityId": null,
              "userId": 11,
              "noteType": "administrative",
              "text": "Event Created",
              "struckthrough": false,
              "createdAt": "2026-03-13T00:13:17.113-04:00"
            },
            {
              "id": 12,
              "personConditionId": 14,
              "exposureId": 31,
              "facilityId": 5,
              "userId": 11,
              "noteType": "clinical",
              "text": "Withdrawn in error",
              "struckthrough": true,
              "createdAt": "2026-03-13T00:16:37.402-04:00"
            }
          ],
          "page": 1,
          "pageSize": 25,
          "totalCount": 2
        }
        """;

    private const string Note =
        """
        {
          "id": 13,
          "personConditionId": 14,
          "exposureId": 31,
          "facilityId": null,
          "userId": 11,
          "noteType": "clinical",
          "text": "Spoke to the patient",
          "struckthrough": false,
          "createdAt": "2026-03-13T00:18:06.332-04:00"
        }
        """;

    [Fact]
    public async Task PageAsksTheReportsNotesUrlWithItsPageAndHandsTheListBack()
    {
        HttpRequestMessage? sent = null;

        var client = new NotesClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Notes);
        }));

        var page = await client.Page(14, 1, 25);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/notes?page=1&pageSize=25",
            sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(page.Succeeded);
        Assert.Equal(2, page.Value?.TotalCount);
        var first = page.Value!.Items[0];
        Assert.Equal(11, first.Id);
        Assert.Equal("administrative", first.NoteType);
        Assert.Equal("Event Created", first.Text);
        Assert.False(first.Struckthrough);
        Assert.Equal(
            new DateTimeOffset(2026, 3, 13, 0, 13, 17, 113, TimeSpan.FromHours(-4)),
            first.CreatedAt);
    }

    /// <summary>
    /// A struck-through note arrives in the list like any other, marked
    /// struck. Strikethrough is a soft delete, and a client that dropped a
    /// struck note would make it a hard one.
    /// </summary>
    [Fact]
    public async Task AStruckThroughNoteIsStillInTheList()
    {
        var client = new NotesClient(Transport(_ => Answer(HttpStatusCode.OK, Notes)));

        var page = await client.Page(14, 1, 25);

        var struck = page.Value!.Items[1];
        Assert.Equal(12, struck.Id);
        Assert.True(struck.Struckthrough);
        Assert.Equal("Withdrawn in error", struck.Text);
    }

    /// <summary>
    /// A report with more notes than one page holds is read whole: a first
    /// page of a full 200 and a second page of one more are both asked for,
    /// in order, and all 201 come back — the note after the first page is not
    /// silently left off.
    /// </summary>
    [Fact]
    public async Task AllWalksEveryPageUntilTheTotalHasArrived()
    {
        var asked = new List<string>();

        var client = new NotesClient(Transport(request =>
        {
            asked.Add(request.RequestUri!.PathAndQuery);

            return request.RequestUri.Query.Contains("page=1", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, PageOf(Enumerable.Range(1, 200), page: 1, total: 201))
                : Answer(HttpStatusCode.OK, PageOf([201], page: 2, total: 201));
        }));

        var all = await client.All(14);

        Assert.Equal(
            [
                "/api/person-conditions/14/notes?page=1&pageSize=200",
                "/api/person-conditions/14/notes?page=2&pageSize=200",
            ],
            asked);

        Assert.True(all.Succeeded);
        Assert.Equal(201, all.Value!.Count);
        Assert.Equal(Enumerable.Range(1, 201), all.Value.Select(note => note.Id));
    }

    /// <summary>
    /// A report whose notes fit in one page is read with one ask: the total
    /// says there is no second page, so none is asked for.
    /// </summary>
    [Fact]
    public async Task AllStopsAfterOnePageWhenTheTotalFitsInIt()
    {
        var asks = 0;

        var client = new NotesClient(Transport(_ =>
        {
            asks++;
            return Answer(HttpStatusCode.OK, PageOf([11, 12], page: 1, total: 2));
        }));

        var all = await client.All(14);

        Assert.Equal(1, asks);
        Assert.True(all.Succeeded);
        Assert.Equal([11, 12], all.Value!.Select(note => note.Id));
    }

    /// <summary>
    /// A page that fails fails the whole, in the list's own words: a list with
    /// a hole in it is not the list.
    /// </summary>
    [Fact]
    public async Task AllFailsAsTheListWhenAnyPageFails()
    {
        var client = new NotesClient(Transport(request =>
            request.RequestUri!.Query.Contains("page=1", StringComparison.Ordinal)
                ? Answer(HttpStatusCode.OK, PageOf(Enumerable.Range(1, 200), page: 1, total: 201))
                : Answer(HttpStatusCode.InternalServerError, """{ "status": 500 }""")));

        var all = await client.All(14);

        Assert.False(all.Succeeded);
        Assert.StartsWith(NotesClient.ListFailurePrefix, all.Failure);
        Assert.Contains("500", all.Failure);
    }

    [Fact]
    public async Task RecordPostsTheNoteAndHandsTheStoredNoteBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new NotesClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.Created, Note);
        }));

        var recorded = await client.Record(14, new CreateNoteRequest(
            Text: "Spoke to the patient",
            NoteType: "clinical",
            ExposureId: 31));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/notes",
            sent?.RequestUri?.ToString());

        Assert.Contains("\"text\":\"Spoke to the patient\"", body, StringComparison.Ordinal);
        Assert.Contains("\"noteType\":\"clinical\"", body, StringComparison.Ordinal);
        Assert.Contains("\"exposureId\":31", body, StringComparison.Ordinal);

        // The report, the author and the moment are the server's to set, and
        // a note is recorded standing, so the posted note names none of the
        // four.
        using var posted = JsonDocument.Parse(body!);
        Assert.False(posted.RootElement.TryGetProperty("personConditionId", out _));
        Assert.False(posted.RootElement.TryGetProperty("userId", out _));
        Assert.False(posted.RootElement.TryGetProperty("createdAt", out _));
        Assert.False(posted.RootElement.TryGetProperty("struckthrough", out _));

        Assert.True(recorded.Succeeded);
        Assert.Equal(13, recorded.Value?.Id);
        Assert.Equal("Spoke to the patient", recorded.Value?.Text);
        Assert.False(recorded.Value?.Struckthrough);
    }

    /// <summary>
    /// The soft delete is a PUT to the note's own URL carrying the one flag,
    /// and what comes back is the note as it now stands — so the screen shows
    /// what is stored rather than what was clicked.
    /// </summary>
    [Fact]
    public async Task StrikePutsTheFlagToTheNotesOwnUrlAndHandsTheNoteBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new NotesClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(
                HttpStatusCode.OK,
                Note.Replace("\"struckthrough\": false", "\"struckthrough\": true", StringComparison.Ordinal));
        }));

        var struck = await client.Strike(13, true);

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/notes/13/strikethrough",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"struckthrough\":true", body, StringComparison.Ordinal);

        Assert.True(struck.Succeeded);
        Assert.True(struck.Value?.Struckthrough);
    }

    /// <summary>
    /// The same ask restores a note struck through by mistake: the flag is a
    /// value and not a verb, so false is sent as readily as true.
    /// </summary>
    [Fact]
    public async Task StrikeRestoresANoteAsWell()
    {
        string? body = null;

        var client = new NotesClient(Transport(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;
            return Answer(HttpStatusCode.OK, Note);
        }));

        var restored = await client.Strike(13, false);

        Assert.Contains("\"struckthrough\":false", body, StringComparison.Ordinal);
        Assert.True(restored.Succeeded);
        Assert.False(restored.Value?.Struckthrough);
    }

    [Fact]
    public async Task EachFailureCarriesItsOwnPrefixAndNotTheApisBody()
    {
        var listFailed = await new NotesClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")))
            .Page(987654, 1, 25);

        Assert.False(listFailed.Succeeded);
        Assert.StartsWith(NotesClient.ListFailurePrefix, listFailed.Failure);
        Assert.Contains("404", listFailed.Failure);
        Assert.DoesNotContain("987654", listFailed.Failure, StringComparison.Ordinal);

        var recordFailed = await new NotesClient(Transport(_ => Answer(
                HttpStatusCode.BadRequest,
                """{ "status": 400, "detail": "noteType 'investigation' is not a kind of note." }""")))
            .Record(14, new CreateNoteRequest(Text: "Spoke to the patient"));

        Assert.False(recordFailed.Succeeded);
        Assert.StartsWith(NotesClient.RecordFailurePrefix, recordFailed.Failure);
        Assert.Contains("400", recordFailed.Failure);

        var strikeFailed = await new NotesClient(Transport(_ => Answer(
                HttpStatusCode.NotFound,
                """{ "status": 404, "detail": "No note has id 987654." }""")))
            .Strike(987654, true);

        Assert.False(strikeFailed.Succeeded);
        Assert.StartsWith(NotesClient.StrikeFailurePrefix, strikeFailed.Failure);
        Assert.Contains("404", strikeFailed.Failure);

        // The three sentences are three sentences.
        Assert.NotEqual(NotesClient.ListFailurePrefix, NotesClient.RecordFailurePrefix);
        Assert.NotEqual(NotesClient.RecordFailurePrefix, NotesClient.StrikeFailurePrefix);
        Assert.NotEqual(NotesClient.ListFailurePrefix, NotesClient.StrikeFailurePrefix);
    }

    /// <summary>
    /// One page of bare notes with the given ids, as the API tier writes it,
    /// carrying the page number and the total the client reads to decide
    /// whether to ask for another.
    /// </summary>
    private static string PageOf(IEnumerable<int> ids, int page, int total)
    {
        var items = string.Join(",", ids.Select(id =>
            $$"""{"id":{{id}},"personConditionId":14,"exposureId":null,"facilityId":null,"userId":null,"noteType":"clinical","text":"A note","struckthrough":false,"createdAt":"2026-03-13T00:13:17-04:00"}"""));

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
