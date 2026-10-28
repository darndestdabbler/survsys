using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's reader and writer of <c>/api/forms</c>: the URLs it
/// asks, the list and records it hands a screen, and the phrases its failures
/// start with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What is this
/// client's own is small, which is the point of it being this small.
/// </remarks>
public class FormsClientTests
{
    private const string Json = "application/json";

    private const string TwoForms =
        """
        {
          "items": [
            { "id": 2, "name": "Gastro supplement", "conditionId": 4, "conditionName": "Malaria",
              "autoAssign": true, "createdAt": "2026-06-01T09:00:00+00:00" },
            { "id": 1, "name": "Travel history", "conditionId": 4, "conditionName": "Malaria",
              "autoAssign": false, "createdAt": "2026-03-01T09:00:00+00:00" }
          ],
          "page": 1,
          "pageSize": 200,
          "totalCount": 2
        }
        """;

    private const string OneForm =
        """
        {
          "id": 1, "name": "Gastro supplement", "description": "Extra questions",
          "conditionId": 4, "conditionName": "Malaria", "autoAssign": true,
          "createdAt": "2026-03-01T09:00:00+00:00",
          "sections": [
            { "id": 10, "title": "Exposure", "sortOrder": 0, "questions": [
              { "id": 100, "text": "Where did you eat?", "helpText": null, "answerType": 0,
                "isRequired": false, "sortOrder": 0, "options": [] }
            ] }
          ]
        }
        """;

    [Fact]
    public async Task TheRequestAsksForTheFormsInOneWidePage()
    {
        Uri? asked = null;

        var client = new FormsClient(Transport(request =>
        {
            asked = request.RequestUri;
            return Answer(HttpStatusCode.OK, TwoForms);
        }));

        await client.All();

        Assert.Equal("http://api.example.test/api/forms?pageSize=200", asked?.ToString());
    }

    [Fact]
    public async Task TheFormsTheApiReturnedAreTheOnesTheClientReturns()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.OK, TwoForms)));

        var asked = await client.All();

        Assert.True(asked.Succeeded);
        Assert.Equal([2, 1], asked.Value!.Items.Select(form => form.Id));
        Assert.Equal("Gastro supplement", asked.Value.Items[0].Name);
        Assert.Equal("Malaria", asked.Value.Items[0].ConditionName);
        Assert.True(asked.Value.Items[0].AutoAssign);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefix()
    {
        var client = new FormsClient(
            Transport(_ => Answer(HttpStatusCode.InternalServerError, "{}")));

        var asked = await client.All();

        Assert.False(asked.Succeeded);
        Assert.StartsWith(FormsClient.FailurePrefix, asked.Failure);
        Assert.NotEqual(OutbreaksClient.FailurePrefix, FormsClient.FailurePrefix);
    }

    [Fact]
    public async Task FindAsksForTheFormAndReturnsItsTree()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneForm);
        }));

        var asked = await client.Find(1);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/forms/1", sent?.RequestUri?.ToString());
        Assert.Equal("Malaria", asked.Value!.ConditionName);
        Assert.Equal("Where did you eat?", asked.Value.Sections[0].Questions[0].Text);
    }

    [Fact]
    public async Task CreatePostsToTheCollection()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.Created, OneForm);
        }));

        var asked = await client.Create(new SaveFormRequest("Gastro supplement", ConditionId: 4));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/forms", sent?.RequestUri?.ToString());
        Assert.Equal(1, asked.Value!.Id);
    }

    [Fact]
    public async Task UpdatePutsToTheForm()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneForm);
        }));

        var asked = await client.Update(1, new SaveFormRequest("Gastro supplement", ConditionId: 4));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/forms/1", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task ASaveThatFailedCarriesTheSavePrefix()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.Create(new SaveFormRequest("", ConditionId: 4));

        Assert.StartsWith(FormsClient.SaveFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AFindThatFailedCarriesTheFindPrefix()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.Find(99);

        Assert.StartsWith(FormsClient.FindFailurePrefix, asked.Failure);
    }

    private const string TwoAssignments =
        """
        [
          { "id": 5, "formId": 2, "formName": "Gastro supplement",
            "assignedAt": "2026-06-01T09:00:00+00:00" },
          { "id": 6, "formId": 3, "formName": "Travel history",
            "assignedAt": "2026-06-02T09:00:00+00:00" }
        ]
        """;

    private const string OneCaseForm =
        """
        {
          "assignmentId": 5, "formId": 2, "formName": "Gastro supplement",
          "description": "Extra questions",
          "sections": [
            { "id": 10, "title": "Exposure", "sortOrder": 0, "questions": [
              { "id": 100, "text": "Where did you eat?", "helpText": null, "answerType": 0,
                "isRequired": false, "sortOrder": 0, "options": [],
                "textAnswer": "At the diner", "code": null },
              { "id": 101, "text": "Shellfish?", "helpText": null, "answerType": 1,
                "isRequired": true, "sortOrder": 1,
                "options": [ { "id": 1, "code": "Y", "label": "Yes", "sortOrder": 0 } ],
                "textAnswer": null, "code": "Y" }
            ] }
          ]
        }
        """;

    [Fact]
    public async Task CaseFormsAsksForTheCasesAssignedForms()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, TwoAssignments);
        }));

        var asked = await client.CaseForms(7);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/7/forms", sent?.RequestUri?.ToString());
        Assert.Equal([5, 6], asked.Value!.Select(assignment => assignment.Id));
    }

    [Fact]
    public async Task CaseFormAsksForTheAssignedFormWithItsAnswers()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneCaseForm);
        }));

        var asked = await client.CaseForm(5);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/forms/5", sent?.RequestUri?.ToString());
        Assert.Equal("At the diner", asked.Value!.Sections[0].Questions[0].TextAnswer);
        Assert.Equal("Y", asked.Value.Sections[0].Questions[1].Code);
    }

    [Fact]
    public async Task SaveAnswersPutsToTheAssignmentsAnswers()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, OneCaseForm);
        }));

        var asked = await client.SaveAnswers(
            5,
            new SaveFormAnswersRequest([new FormAnswerInput(100, TextAnswer: "At the diner")]));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/forms/5/answers", sent?.RequestUri?.ToString());
        Assert.True(asked.Succeeded);
    }

    [Fact]
    public async Task AssignPostsToTheCasesFormRoute()
    {
        HttpRequestMessage? sent = null;
        var client = new FormsClient(Transport(request =>
        {
            sent = request;
            return Answer(
                HttpStatusCode.Created,
                """{ "id": 8, "formId": 2, "formName": "Gastro supplement", "assignedAt": "2026-06-03T09:00:00+00:00" }""");
        }));

        var asked = await client.Assign(7, 2);

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/cases/7/forms/2", sent?.RequestUri?.ToString());
        Assert.Equal(8, asked.Value?.Id);
        Assert.Equal("Gastro supplement", asked.Value?.FormName);
    }

    [Fact]
    public async Task AnAssignThatFailedCarriesTheAssignPrefix()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.Assign(7, 99);

        Assert.StartsWith(FormsClient.AssignFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task ACaseFormsLoadThatFailedCarriesTheCaseFormsPrefix()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.NotFound, "{}")));

        var asked = await client.CaseForms(99);

        Assert.StartsWith(FormsClient.CaseFormsFailurePrefix, asked.Failure);
    }

    [Fact]
    public async Task AnAnswersSaveThatFailedCarriesTheAnswersSavePrefix()
    {
        var client = new FormsClient(Transport(_ => Answer(HttpStatusCode.BadRequest, "{}")));

        var asked = await client.SaveAnswers(5, new SaveFormAnswersRequest([]));

        Assert.StartsWith(FormsClient.AnswersSaveFailurePrefix, asked.Failure);
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
