using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Survsys.Contracts;
using Survsys.Web;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The application tier's writer and reader of <c>/api/person-conditions</c>:
/// what it sends, what it hands the screen back, and the phrase each of its
/// two failures starts with.
/// </summary>
/// <remarks>
/// Every way an ask can fail is <see cref="ApiAskTests"/>'s. What matters here
/// is that the report the screen filled in is the report that is sent, that
/// the record number the API assigned is what comes back — because the record
/// number is what the report shell shows at the top — and that a report read
/// by its identifier is the report the shell gets.
/// </remarks>
public class PersonConditionsClientTests
{
    private const string Json = "application/json";

    private const string Opened =
        """
        {
          "id": 11,
          "personId": 13,
          "conditionId": 74,
          "recordNumber": "202600001",
          "agencyId": 1,
          "firstReportedPhDate": "2026-03-12",
          "eventOnsetDate": "2026-03-12",
          "createdAt": "2026-03-12T22:07:16-06:00"
        }
        """;

    private const string Classified =
        """
        {
          "id": 11,
          "personId": 13,
          "conditionId": 74,
          "recordNumber": "202600001",
          "agencyId": 1,
          "firstReportedPhDate": "2026-03-12",
          "eventOnsetDate": "2026-03-12",
          "createdAt": "2026-03-12T22:07:16-06:00",
          "caseStatus": "C",
          "mmwrWeek": 10,
          "mmwrYear": 2026
        }
        """;

    [Fact]
    public async Task TheReportFilledInIsTheReportSent()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.Created, Opened);
        }));

        await client.Open(new CreatePersonConditionRequest(
            PersonId: 13,
            ConditionId: 74,
            AgencyId: 1,
            FirstReportedPhDate: new DateOnly(2026, 3, 12)));

        Assert.Equal(HttpMethod.Post, sent?.Method);
        Assert.Equal("http://api.example.test/api/person-conditions", sent?.RequestUri?.ToString());

        Assert.Contains("\"personId\":13", body, StringComparison.Ordinal);
        Assert.Contains("\"conditionId\":74", body, StringComparison.Ordinal);
        Assert.Contains("\"agencyId\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"firstReportedPhDate\":\"2026-03-12\"", body, StringComparison.Ordinal);

        // The record number is the other tier's to assign, so it is not in
        // what this one sends.
        Assert.DoesNotContain("recordNumber", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheReportOpenedComesBackWithItsRecordNumber()
    {
        var client = new PersonConditionsClient(
            Transport(_ => Answer(HttpStatusCode.Created, Opened)));

        var opened = await client.Open(new CreatePersonConditionRequest(13, 74, 1));

        Assert.True(opened.Succeeded);
        Assert.Equal("202600001", opened.Value?.RecordNumber);
        Assert.Equal(11, opened.Value?.Id);
        Assert.Equal(13, opened.Value?.PersonId);
        Assert.Equal(74, opened.Value?.ConditionId);
        Assert.Equal(1, opened.Value?.AgencyId);
        Assert.Equal(new DateOnly(2026, 3, 12), opened.Value?.EventOnsetDate);
    }

    [Fact]
    public async Task AFailureCarriesThisClientsOwnPrefixAndNotTheApisBody()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "conditionId 987654 is not a condition on file." }""")));

        var opened = await client.Open(new CreatePersonConditionRequest(13, 987654));

        Assert.False(opened.Succeeded);
        Assert.Null(opened.Value);
        Assert.StartsWith(PersonConditionsClient.FailurePrefix, opened.Failure);
        Assert.Contains("400", opened.Failure);
        Assert.DoesNotContain("987654", opened.Failure, StringComparison.Ordinal);
    }

    /// <summary>
    /// The read the report shell makes first: the report's own URL, and the
    /// report back — whose person and condition the shell then reads from
    /// their own resources.
    /// </summary>
    [Fact]
    public async Task TheReportFoundIsTheReportTheClientReturns()
    {
        HttpRequestMessage? sent = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            return Answer(HttpStatusCode.OK, Opened);
        }));

        var found = await client.Find(11);

        Assert.Equal(HttpMethod.Get, sent?.Method);
        Assert.Equal("http://api.example.test/api/person-conditions/11", sent?.RequestUri?.ToString());
        Assert.Null(sent?.Content);

        Assert.True(found.Succeeded);
        Assert.Equal(11, found.Value?.Id);
        Assert.Equal(13, found.Value?.PersonId);
        Assert.Equal(74, found.Value?.ConditionId);
        Assert.Equal("202600001", found.Value?.RecordNumber);
        Assert.Equal(1, found.Value?.AgencyId);
        Assert.Equal(new DateOnly(2026, 3, 12), found.Value?.FirstReportedPhDate);
        Assert.Equal(new DateOnly(2026, 3, 12), found.Value?.EventOnsetDate);
    }

    /// <summary>
    /// An identifier nobody has: the API tier answers 404, and what the shell
    /// gets is a message about loading — not about creating, which is the
    /// other thing this client does — rather than an exception.
    /// </summary>
    [Fact]
    public async Task AReportNobodyHasBecomesAMessageAboutLoading()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.NotFound,
            """{ "status": 404, "detail": "No case morbidity report has id 987654." }""")));

        var found = await client.Find(987654);

        Assert.False(found.Succeeded);
        Assert.Null(found.Value);
        Assert.StartsWith(PersonConditionsClient.FindFailurePrefix, found.Failure);
        Assert.Contains("404", found.Failure);
        Assert.DoesNotContain("987654", found.Failure, StringComparison.Ordinal);
        Assert.NotEqual(PersonConditionsClient.FailurePrefix, PersonConditionsClient.FindFailurePrefix);
    }

    /// <summary>
    /// Setting the status: the status the screen typed is the body sent to the
    /// report's own status URL, and the report that comes back carries the
    /// status as stored — along with the MMWR week and year the report already
    /// held.
    /// </summary>
    [Fact]
    public async Task TheStatusSetIsSentAndTheSavedReportComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, Classified);
        }));

        var saved = await client.SetStatus(11, new UpdateCaseStatusRequest("C"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/status",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"caseStatus\":\"C\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("C", saved.Value?.CaseStatus);
        Assert.Equal(10, saved.Value?.MmwrWeek);
        Assert.Equal(2026, saved.Value?.MmwrYear);
    }

    /// <summary>
    /// A status the API tier refused: the message starts with this client's
    /// third prefix — not the create or the load one — and names the status
    /// the API gave rather than the code it refused.
    /// </summary>
    [Fact]
    public async Task AStatusThatCouldNotBeSavedCarriesTheStatusPrefix()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "caseStatus 'X' is not a status a report may be set to." }""")));

        var saved = await client.SetStatus(11, new UpdateCaseStatusRequest("X"));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(PersonConditionsClient.StatusFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("'X'", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(
            PersonConditionsClient.FailurePrefix, PersonConditionsClient.StatusFailurePrefix);
        Assert.NotEqual(
            PersonConditionsClient.FindFailurePrefix, PersonConditionsClient.StatusFailurePrefix);
    }

    private const string Assigned =
        """
        {
          "id": 11,
          "personId": 13,
          "conditionId": 74,
          "recordNumber": "202600001",
          "agencyId": 3,
          "eventOnsetDate": "2026-03-12",
          "createdAt": "2026-03-12T22:07:16-06:00",
          "workflowState": "assigned_to_lhd"
        }
        """;

    private const string Accepted =
        """
        {
          "id": 11,
          "personId": 13,
          "conditionId": 74,
          "recordNumber": "202600001",
          "agencyId": 1,
          "eventOnsetDate": "2026-03-12",
          "createdAt": "2026-03-12T22:07:16-06:00",
          "workflowState": "accepted"
        }
        """;

    /// <summary>
    /// Assigning: the agency the screen chose is the body sent to the report's
    /// own assignment URL, and the report that comes back carries the agency
    /// and the assigned-to-LHD start the API returned it to.
    /// </summary>
    [Fact]
    public async Task TheAgencyAssignedIsSentAndTheAssignedReportComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, Assigned);
        }));

        var saved = await client.Assign(11, new AssignReportRequest(3));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/assignment",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"agencyId\":3", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal(3, saved.Value?.AgencyId);
        Assert.Equal("assigned_to_lhd", saved.Value?.WorkflowState);
    }

    /// <summary>
    /// An assignment the API tier refused: the message starts with this
    /// client's assignment prefix — not the create, load, status or workflow
    /// one — and names the status the API gave rather than the agency it
    /// refused.
    /// </summary>
    [Fact]
    public async Task AnAssignmentThatCouldNotBeSavedCarriesTheAssignPrefix()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "agencyId 99 is not an agency this tier knows." }""")));

        var saved = await client.Assign(11, new AssignReportRequest(99));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(PersonConditionsClient.AssignFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("99", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(
            PersonConditionsClient.StatusFailurePrefix, PersonConditionsClient.AssignFailurePrefix);
    }

    /// <summary>
    /// Advancing the workflow: the target state the screen asked for is the
    /// body sent to the report's own workflow URL, and the report that comes
    /// back carries the state the API moved it to.
    /// </summary>
    [Fact]
    public async Task TheWorkflowMoveIsSentAndTheAdvancedReportComesBack()
    {
        HttpRequestMessage? sent = null;
        string? body = null;

        var client = new PersonConditionsClient(Transport(request =>
        {
            sent = request;
            body = request.Content!.ReadAsStringAsync().Result;

            return Answer(HttpStatusCode.OK, Accepted);
        }));

        var saved = await client.Transition(11, new UpdateWorkflowStateRequest("accepted"));

        Assert.Equal(HttpMethod.Put, sent?.Method);
        Assert.Equal(
            "http://api.example.test/api/person-conditions/11/workflow",
            sent?.RequestUri?.ToString());
        Assert.Contains("\"workflowState\":\"accepted\"", body, StringComparison.Ordinal);

        Assert.True(saved.Succeeded);
        Assert.Equal("accepted", saved.Value?.WorkflowState);
    }

    /// <summary>
    /// A move the API tier refused: the message starts with this client's
    /// workflow prefix — distinct from every other — and names the status the
    /// API gave rather than the states it refused.
    /// </summary>
    [Fact]
    public async Task AWorkflowMoveThatCouldNotBeMadeCarriesTheWorkflowPrefix()
    {
        var client = new PersonConditionsClient(Transport(_ => Answer(
            HttpStatusCode.BadRequest,
            """{ "status": 400, "detail": "A report at accepted may not be moved to closed." }""")));

        var saved = await client.Transition(11, new UpdateWorkflowStateRequest("closed"));

        Assert.False(saved.Succeeded);
        Assert.Null(saved.Value);
        Assert.StartsWith(PersonConditionsClient.WorkflowFailurePrefix, saved.Failure);
        Assert.Contains("400", saved.Failure);
        Assert.DoesNotContain("accepted", saved.Failure, StringComparison.Ordinal);
        Assert.NotEqual(
            PersonConditionsClient.AssignFailurePrefix, PersonConditionsClient.WorkflowFailurePrefix);
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
