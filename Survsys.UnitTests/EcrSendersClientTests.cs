using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/ecr-senders</c>: the
/// URLs and methods it asks with, the senders it hands the screen, and the
/// phrases its failures start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s.
/// </remarks>
public class EcrSendersClientTests
{
    private const string Json = "application/json";

    private const string OneSender =
        """
        { "id": 3, "senderId": "2.16.840.1.113883.4.6^88888888", "name": "Salem Medical Center",
          "stage": "validation", "takesLabResults": true }
        """;

    [Fact]
    public async Task ListAsksForTheSenders()
    {
        Uri? asked = null;
        var client = new EcrSendersClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, $"[{OneSender}]");
        }));

        var senders = await client.List();

        Assert.Equal("http://api.example.test/api/ecr-senders", asked?.ToString());
        var sender = Assert.Single(senders.Value!);
        Assert.Equal("Salem Medical Center", sender.Name);
        Assert.Equal("validation", sender.Stage);
        Assert.True(sender.TakesLabResults);
    }

    [Fact]
    public async Task AddPostsSavePutsAndDeleteDeletesTheSender()
    {
        var sent = new List<(HttpMethod Method, string? Uri, SaveEcrSenderRequest? Body)>();
        var client = new EcrSendersClient(Transport(request =>
        {
            sent.Add((
                request.Method,
                request.RequestUri?.ToString(),
                request.Content?.ReadFromJsonAsync<SaveEcrSenderRequest>().Result));
            return request.Method == HttpMethod.Delete
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : Answer(HttpStatusCode.OK, OneSender);
        }));
        var request = new SaveEcrSenderRequest("2.16.840.1.113883.4.6^88888888", "Salem Medical Center", "validation", true);

        await client.Add(request);
        await client.Save(3, request);
        Assert.Null(await client.Delete(3));

        Assert.Equal(HttpMethod.Post, sent[0].Method);
        Assert.Equal("http://api.example.test/api/ecr-senders", sent[0].Uri);
        Assert.Equal(request, sent[0].Body);
        Assert.Equal(HttpMethod.Put, sent[1].Method);
        Assert.Equal("http://api.example.test/api/ecr-senders/3", sent[1].Uri);
        Assert.Equal(request, sent[1].Body);
        Assert.Equal(HttpMethod.Delete, sent[2].Method);
        Assert.Equal("http://api.example.test/api/ecr-senders/3", sent[2].Uri);
    }

    [Fact]
    public async Task EachFailureCarriesItsPrefix()
    {
        var client = new EcrSendersClient(Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        Assert.StartsWith(EcrSendersClient.ListFailurePrefix, (await client.List()).Failure);
        Assert.StartsWith(
            EcrSendersClient.SaveFailurePrefix,
            (await client.Add(new SaveEcrSenderRequest("1.2", "A", "test"))).Failure);
        Assert.StartsWith(
            EcrSendersClient.SaveFailurePrefix,
            (await client.Save(3, new SaveEcrSenderRequest("1.2", "A", "test"))).Failure);
        Assert.StartsWith(EcrSendersClient.SaveFailurePrefix, await client.Delete(3));
    }

    private static HttpClient Transport(Func<HttpRequestMessage, HttpResponseMessage> answer) =>
        new(new StubTransport(answer)) { BaseAddress = new Uri("http://api.example.test/") };

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(Json) },
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
