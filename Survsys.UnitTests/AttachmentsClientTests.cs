using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of a report's attachments: the URL
/// each of its two asks makes, the multipart body an upload sends, the value
/// handed back, and the phrase each of its two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that a list is read by its report's own URL with its page, that a list
/// longer than one page is walked to the end, that an upload posts the file
/// and the category as the parts the endpoint binds, and that the two failures
/// are told apart.
/// </remarks>
public class AttachmentsClientTests
{
    private const string Json = "application/json";

    private const string Attachments =
        """
        {
          "items": [
            {
              "id": 7,
              "personConditionId": 14,
              "filename": "culture-result.pdf",
              "contentType": "application/pdf",
              "size": 184320,
              "category": "lab",
              "createdAt": "2026-03-13T09:41:02-06:00"
            },
            {
              "id": 6,
              "personConditionId": 14,
              "filename": "exposure-letter.docx",
              "contentType": "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
              "size": 20480,
              "category": "letter",
              "createdAt": "2026-03-12T14:02:55-06:00"
            }
          ],
          "page": 1,
          "pageSize": 25,
          "totalCount": 2
        }
        """;

    private const string Attachment =
        """
        {
          "id": 8,
          "personConditionId": 14,
          "filename": "chest.png",
          "contentType": "image/png",
          "size": 4096,
          "category": "x-ray",
          "createdAt": "2026-03-13T10:14:19-06:00"
        }
        """;

    [Fact]
    public async Task PageAsksTheReportsAttachmentsUrlWithItsPageAndHandsTheListBack()
    {
        HttpRequestMessage? sent = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Attachments);
        }));

        var page = await client.Page(14, 1, 25);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/attachments?page=1&pageSize=25",
            sent?.RequestUri?.ToString());

        Assert.True(page.Succeeded);
        Assert.Equal(2, page.Value?.TotalCount);
        Assert.Equal("culture-result.pdf", page.Value?.Items[0].Filename);
        Assert.Equal("lab", page.Value?.Items[0].Category);
        Assert.Equal(184320, page.Value?.Items[0].Size);
    }

    /// <summary>
    /// The panel lists a report's attachments whole, so a list longer than one
    /// page is read to the end rather than cut off at the first.
    /// </summary>
    [Fact]
    public async Task AllWalksThePagesUntilAsManyHaveArrivedAsTheApiSaidThereWere()
    {
        var asked = new List<string>();

        var client = new AttachmentsClient(Transport(request =>
        {
            asked.Add(request.RequestUri!.Query);

            return Answer(
                HttpStatusCode.OK,
                asked.Count == 1
                    ? PageOf(Enumerable.Range(1, AttachmentsClient.FullPage), 1, 201)
                    : PageOf([201], 2, 201));
        }));

        var all = await client.All(14);

        Assert.True(all.Succeeded);
        Assert.Equal(201, all.Value?.Count);
        Assert.Equal(
            [
                $"?page=1&pageSize={AttachmentsClient.FullPage}",
                $"?page=2&pageSize={AttachmentsClient.FullPage}",
            ],
            asked);
    }

    /// <summary>
    /// A page that fails is the failure of the whole list, in the list's own
    /// words: a list with a hole in it is not the list.
    /// </summary>
    [Fact]
    public async Task AllThatCouldNotReadAPageIsTheFailureOfTheList()
    {
        var client = new AttachmentsClient(Transport(_ =>
            Answer(HttpStatusCode.InternalServerError, "{}")));

        var all = await client.All(14);

        Assert.False(all.Succeeded);
        Assert.StartsWith(AttachmentsClient.ListFailurePrefix, all.Failure);
    }

    [Fact]
    public async Task AttachPostsTheFileAndTheCategoryAsTheEndpointsOwnParts()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, Attachment);
        }));

        using var content = new MemoryStream([137, 80, 78, 71]);

        var stored = await client.Attach(14, "chest.png", "image/png", "x-ray", content);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/14/attachments",
            sent?.RequestUri?.ToString());
        Assert.Equal("multipart/form-data", sent?.Content?.Headers.ContentType?.MediaType);

        // The parts the endpoint binds by name: the file under "file",
        // carrying the name and the declared type the browser gave, and the
        // category beside it.
        Assert.Contains("name=file", body, StringComparison.Ordinal);
        Assert.Contains("chest.png", body, StringComparison.Ordinal);
        Assert.Contains("image/png", body, StringComparison.Ordinal);
        Assert.Contains("name=category", body, StringComparison.Ordinal);
        Assert.Contains("x-ray", body, StringComparison.Ordinal);

        Assert.True(stored.Succeeded);
        Assert.Equal(8, stored.Value?.Id);
        Assert.Equal("chest.png", stored.Value?.Filename);
    }

    [Fact]
    public async Task AttachToOutbreakPostsToTheOutbreaksAttachmentsUrl()
    {
        HttpRequestMessage? sent = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, Attachment);
        }));

        using var content = new MemoryStream([1, 2, 3]);

        var stored = await client.AttachToOutbreak(9, "line-list.csv", "text/csv", "lab", content);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/outbreaks/9/attachments", sent?.RequestUri?.ToString());
        Assert.True(stored.Succeeded);
    }

    [Fact]
    public async Task AllForOutbreakReadsTheOutbreaksAttachments()
    {
        HttpRequestMessage? sent = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, PageOf([4], 1, 1));
        }));

        var all = await client.AllForOutbreak(9);

        Assert.Equal(
            $"http://api.example.test/api/outbreaks/9/attachments?page=1&pageSize={AttachmentsClient.FullPage}",
            sent?.RequestUri?.ToString());
        Assert.Equal(1, all.Value?.Count);
    }

    /// <summary>
    /// A declared type that is not a media type at all is left off the part
    /// rather than guessed at: the upload still goes, and the API tier refuses
    /// it by the rule it refuses any type it does not store.
    /// </summary>
    [Fact]
    public async Task AttachSendsAFileWhoseDeclaredTypeWillNotParse()
    {
        string? body = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, Attachment);
        }));

        using var content = new MemoryStream([1, 2, 3]);

        var stored = await client.Attach(14, "chest.png", "not a media type", "x-ray", content);

        Assert.True(stored.Succeeded);
        Assert.Contains("chest.png", body, StringComparison.Ordinal);
        Assert.DoesNotContain("not a media type", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two failures are two sentences: a list that would not load and a
    /// file that would not go up are different things to be told.
    /// </summary>
    [Fact]
    public async Task EachAskFailsInItsOwnWords()
    {
        var client = new AttachmentsClient(Transport(_ =>
            Answer(HttpStatusCode.BadRequest, """{ "status": 400 }""")));

        var listFailed = await client.Page(14, 1, 25);

        using var content = new MemoryStream([1, 2, 3]);
        var uploadFailed = await client.Attach(14, "chest.png", "image/png", "x-ray", content);

        Assert.StartsWith(AttachmentsClient.ListFailurePrefix, listFailed.Failure);
        Assert.StartsWith(AttachmentsClient.UploadFailurePrefix, uploadFailed.Failure);
        Assert.NotEqual(
            AttachmentsClient.ListFailurePrefix,
            AttachmentsClient.UploadFailurePrefix);
    }

    [Fact]
    public async Task ContentAsksForTheAttachmentsFile()
    {
        HttpRequestMessage? sent = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, "a,b", "text/csv");
        }));

        var file = await client.Content(7);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/attachments/7/content", sent?.RequestUri?.ToString());
        Assert.Equal("a,b"u8.ToArray(), file.Value!.Content);
        Assert.Equal("text/csv", file.Value.ContentType);
    }

    [Fact]
    public async Task DeleteDeletesTheAttachment()
    {
        HttpRequestMessage? sent = null;

        var client = new AttachmentsClient(Transport(request =>
        {
            sent = request;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }));

        var failure = await client.Delete(7);

        Assert.Equal(HttpMethod.Delete, sent?.Method);
        Assert.Equal("http://api.example.test/api/attachments/7", sent?.RequestUri?.ToString());
        Assert.Null(failure);
    }

    [Fact]
    public async Task ADownloadAndADeleteFailInTheirOwnWords()
    {
        var client = new AttachmentsClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var file = await client.Content(7);
        var deleted = await client.Delete(7);

        Assert.StartsWith(AttachmentsClient.ContentFailurePrefix, file.Failure);
        Assert.StartsWith(AttachmentsClient.DeleteFailurePrefix, deleted);
    }

    /// <summary>
    /// One page of bare attachments with the given ids, as the API tier writes
    /// it, carrying the page number and the total the client reads to decide
    /// whether to ask for another.
    /// </summary>
    private static string PageOf(IEnumerable<int> ids, int page, int total)
    {
        var items = string.Join(",", ids.Select(id =>
            $$"""{"id":{{id}},"personConditionId":14,"filename":"a.pdf","contentType":"application/pdf","size":1024,"category":"other","createdAt":"2026-03-13T09:41:02-06:00"}"""));

        return $$"""{"items":[{{items}}],"page":{{page}},"pageSize":{{AttachmentsClient.FullPage}},"totalCount":{{total}}}""";
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
