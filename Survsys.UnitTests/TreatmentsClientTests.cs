using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/treatments</c>: the URLs
/// it asks, the list and records it hands a screen, and the phrases its failures
/// start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What is this
/// client's own is small, which is the point of it being this small.
/// </remarks>
public class TreatmentsClientTests
{
    private const string Json = "application/json";

    private const string TwoTreatments =
        """
        {
          "items": [
            { "id": 1, "name": "Amoxicillin", "active": true, "defaultTreatment": false, "conditionCount": 2 },
            { "id": 2, "name": "Rifampin", "active": false, "defaultTreatment": true, "conditionCount": 0 }
          ],
          "page": 1,
          "pageSize": 200,
          "totalCount": 2
        }
        """;

    private const string OneTreatment =
        """
        {
          "id": 1, "name": "Amoxicillin", "active": true, "defaultTreatment": false,
          "conditions": [ { "id": 4, "name": "Lyme disease" }, { "id": 7, "name": "Malaria" } ]
        }
        """;

    private const string Conditions =
        """
        {
          "items": [
            { "id": 4, "name": "Lyme disease", "cdcCode": null, "active": true },
            { "id": 7, "name": "Malaria", "cdcCode": "10130", "active": true }
          ],
          "page": 1,
          "pageSize": 200,
          "totalCount": 2
        }
        """;

    [Fact]
    public async Task TheRequestAsksForTheTreatmentsInOneWidePage()
    {
        Uri? asked = null;

        var client = new TreatmentsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoTreatments);
        }));

        await client.All();

        Assert.Equal("http://api.example.test/api/treatments?pageSize=200", asked?.ToString());
    }

    [Fact]
    public async Task TheTreatmentsTheApiReturnedAreTheOnesTheClientReturns()
    {
        var client = new TreatmentsClient(Transport(_ => Answer(HttpStatusCode.OK, TwoTreatments)));

        var asked = await client.All();

        Assert.True(asked.Succeeded);
        Assert.Equal([1, 2], asked.Value!.Items.Select(treatment => treatment.Id));
        Assert.Equal("Amoxicillin", asked.Value.Items[0].Name);
        Assert.Equal(2, asked.Value.Items[0].ConditionCount);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefix()
    {
        var client = new TreatmentsClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var asked = await client.All();

        Assert.False(asked.Succeeded);
        Assert.StartsWith(TreatmentsClient.FailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task FindAsksForTheTreatmentAndReturnsItsConditions()
    {
        HttpRequestMessage? sent = null;
        var client = new TreatmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneTreatment);
        }));

        var asked = await client.Find(1);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/treatments/1", sent?.RequestUri?.ToString());
        Assert.Equal([4, 7], asked.Value!.Conditions.Select(condition => condition.Id));
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = new TreatmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneTreatment);
        }));

        var asked = await client.Create(new SaveTreatmentRequest("Amoxicillin", ConditionIds: [4, 7]));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/treatments", sent?.RequestUri?.ToString());
        Assert.Equal(1, asked.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheTreatment()
    {
        HttpRequestMessage? sent = null;
        var client = new TreatmentsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneTreatment);
        }));

        var asked = await client.Update(1, new SaveTreatmentRequest("Amoxicillin"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/treatments/1", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new TreatmentsClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.Create(new SaveTreatmentRequest(""));

        Assert.StartsWith(TreatmentsClient.SaveFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AFindThatFailedCarriesTheFindPrefix()
    {
        var client = new TreatmentsClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.Find(99);

        Assert.StartsWith(TreatmentsClient.FindFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task ConditionsAsksForTheConditionsInOneWidePage()
    {
        Uri? asked = null;
        var client = new TreatmentsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, Conditions);
        }));

        var conditions = await client.Conditions();

        Assert.Equal("http://api.example.test/api/conditions?pageSize=200", asked?.ToString());
        Assert.Equal([4, 7], conditions.Value!.Items.Select(condition => condition.Id));
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
