using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The end of the legacy two-step creation flow, against the real database:
/// a case morbidity report opened about a seeded person, for a seeded
/// condition, with the record number this tier assigned — and read back from
/// the <c>Location</c> the creation gave.
/// </summary>
/// <remarks>
/// The fixture seeds no reports, which is what lets these tests say what the
/// first record number of a year is. Each one clears the table before it
/// asserts and again after it, because the record number a report takes
/// depends on the reports already there: a test that left one behind would
/// decide what the next test's record number was. The clearing is done through
/// the context, since this tier's delete is soft and leaves the row in place.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class PersonConditionEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The whole of what step two of the creation flow does, and what it
    /// leaves behind: a report with the first record number of the year, its
    /// event onset date set to the day it was created, and a
    /// <c>Location</c> that reads it back field for field.
    /// </summary>
    [Fact]
    public async Task AReportOpenedTakesTheFirstRecordNumberOfTheYearAndReadsBack()
    {
        await Clear();

        var client = api.CreateClient();
        var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        try
        {
            using var created = await client.PostAsJsonAsync(
                "/api/person-conditions",
                new CreatePersonConditionRequest(
                    PersonId: person,
                    ConditionId: condition,
                    AgencyId: 1,
                    FirstReportedPhDate: new DateOnly(2026, 3, 12)),
                Json);

            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            var report = (await created.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;

            Assert.True(report.Id > 0);
            Assert.Equal(person, report.PersonId);
            Assert.Equal(condition, report.ConditionId);
            Assert.Equal($"{DateTimeOffset.Now.Year}00001", report.RecordNumber);
            Assert.Equal(1, report.AgencyId);
            Assert.Equal(new DateOnly(2026, 3, 12), report.FirstReportedPhDate);
            Assert.Equal(today, report.EventOnsetDate);
            Assert.InRange(
                report.CreatedAt,
                DateTimeOffset.Now.AddMinutes(-5),
                DateTimeOffset.Now.AddMinutes(5));

            Assert.Equal(
                $"/api/person-conditions/{report.Id}",
                created.Headers.Location?.ToString());

            using var read = await client.GetAsync(created.Headers.Location);
            read.EnsureSuccessStatusCode();

            Assert.Equal(
                report,
                await read.Content.ReadFromJsonAsync<PersonConditionResponse>(Json));
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// The record number follows the highest already issued, which is the one
    /// thing the format is for. Three in a row, so the padding is exercised
    /// past the first.
    /// </summary>
    [Fact]
    public async Task EachReportTakesTheNextRecordNumberOfTheYear()
    {
        await Clear();

        var year = DateTimeOffset.Now.Year;
        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        try
        {
            var numbers = new List<string>();

            for (var opened = 0; opened < 3; opened++)
            {
                numbers.Add((await Open(new CreatePersonConditionRequest(person, condition)))
                    .RecordNumber);
            }

            Assert.Equal([$"{year}00001", $"{year}00002", $"{year}00003"], numbers);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Two reports about the same person for the same condition are two
    /// reports: the legacy schema allows a person to be reported for a
    /// condition more than once — a second episode is a second case — so
    /// nothing here refuses it, and the two are told apart by their record
    /// numbers.
    /// </summary>
    [Fact]
    public async Task TheSamePersonMayBeReportedForTheSameConditionTwice()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        try
        {
            var first = await Open(new CreatePersonConditionRequest(person, condition));
            var second = await Open(new CreatePersonConditionRequest(person, condition));

            Assert.NotEqual(first.Id, second.Id);
            Assert.NotEqual(first.RecordNumber, second.RecordNumber);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// A report with neither an agency nor a first-reported date, which is
    /// what the two nullable columns allow: the screen asks for both, and the
    /// endpoint does not insist on them.
    /// </summary>
    [Fact]
    public async Task AReportMayBeOpenedWithNoAgencyAndNoReportedDate()
    {
        await Clear();

        var person = await PersonId("Doe, ");
        var condition = await ActiveConditionId();

        try
        {
            var report = await Open(new CreatePersonConditionRequest(person, condition));

            Assert.Null(report.AgencyId);
            Assert.Null(report.FirstReportedPhDate);
            Assert.Equal(person, report.PersonId);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// A report opened without an event type is a morbidity report, and one
    /// opened as an assessment is stored and read back as one.
    /// </summary>
    [Fact]
    public async Task AReportIsAMorbidityReportUnlessOpenedAsAnAssessment()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        try
        {
            var morbidity = await Open(new CreatePersonConditionRequest(person, condition));
            var assessment = await Open(new CreatePersonConditionRequest(
                person, condition, EventType: EventTypes.Assessment));

            Assert.Equal(EventTypes.Morbidity, morbidity.EventType);
            Assert.Equal(EventTypes.Assessment, assessment.EventType);

            var read = await api.CreateClient().GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{assessment.Id}", Json);

            Assert.Equal(EventTypes.Assessment, read!.EventType);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// A contact event is opened from an exposure, so asking this route for
    /// one is refused and opens nothing.
    /// </summary>
    [Fact]
    public async Task AContactEventAskedForHereIsAProblemDetailsAndNothingIsOpened()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        var refused = await Refused(new CreatePersonConditionRequest(
            person, condition, EventType: EventTypes.Contact));

        Assert.Contains("eventType 'contact' cannot be opened here", refused,
            StringComparison.Ordinal);
        Assert.Equal(0, await Reports());
    }

    [Fact]
    public async Task APersonNobodyHasIsAProblemDetailsAndNothingIsOpened()
    {
        await Clear();

        var condition = await ActiveConditionId();

        var refused = await Refused(new CreatePersonConditionRequest(987654, condition));

        Assert.Contains("personId 987654 is not a person on file", refused,
            StringComparison.Ordinal);
        Assert.Equal(0, await Reports());
    }

    [Fact]
    public async Task AConditionNobodyHasIsAProblemDetailsAndNothingIsOpened()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");

        var refused = await Refused(new CreatePersonConditionRequest(person, 987654));

        Assert.Contains("conditionId 987654 is not a condition on file", refused,
            StringComparison.Ordinal);
        Assert.Equal(0, await Reports());
    }

    /// <summary>
    /// A condition that exists but may not be chosen, which is what
    /// <c>Condition.Active</c> means: the legacy creation screen offers the
    /// active conditions only, and a caller that went around the screen is
    /// told the same thing. "Influenza, animal isolates" is the inactive
    /// condition the fixture seeds.
    /// </summary>
    [Fact]
    public async Task AnInactiveConditionIsAProblemDetailsAndNothingIsOpened()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");
        var inactive = await ConditionId("Influenza, animal isolates");

        var refused = await Refused(new CreatePersonConditionRequest(person, inactive));

        Assert.Contains($"conditionId {inactive} is a condition that may not be chosen",
            refused, StringComparison.Ordinal);
        Assert.Equal(0, await Reports());
    }

    /// <summary>
    /// An agency the tier does not list. There is no foreign key behind
    /// <c>agency_id</c> to refuse it — the agency table is a later session's —
    /// so this is the check that stands in for one.
    /// </summary>
    [Fact]
    public async Task AnAgencyTheTierDoesNotListIsAProblemDetailsAndNothingIsOpened()
    {
        await Clear();

        var person = await PersonId("Rodriguez, Maria");
        var condition = await ActiveConditionId();

        var refused = await Refused(
            new CreatePersonConditionRequest(person, condition, AgencyId: 99));

        Assert.Contains("agencyId 99 is not an agency", refused, StringComparison.Ordinal);
        Assert.Equal(0, await Reports());
    }

    [Fact]
    public async Task AReportIdNobodyHasIsAProblemDetails()
    {
        using var response = await api.CreateClient().GetAsync("/api/person-conditions/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.Contains("No case morbidity report has id 987654",
            root.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));
    }

    /// <summary>
    /// The header's brief note is saved and read back on the report, a blank
    /// one clears it, an over-long one is refused, and a report nobody has is
    /// a 404; the LHD and state case status the Administrative tab saves are
    /// read back on the report too.
    /// </summary>
    [Fact]
    public async Task TheBriefNoteAndTheCaseStatusesReadBackOnTheReport()
    {
        await Clear();

        var person = await PersonId("Doe, ");
        var condition = await ActiveConditionId();
        var client = api.CreateClient();

        try
        {
            var report = await Open(new CreatePersonConditionRequest(person, condition));

            using (var saved = await client.PutAsJsonAsync(
                $"/api/person-conditions/{report.Id}/brief-note",
                new UpdateBriefNoteRequest("  Call back Monday  "),
                Json))
            {
                Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
                var answered = (await saved.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
                Assert.Equal("Call back Monday", answered.BriefNote);
            }

            var read = (await client.GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{report.Id}", Json))!;
            Assert.Equal("Call back Monday", read.BriefNote);

            using (var tooLong = await client.PutAsJsonAsync(
                $"/api/person-conditions/{report.Id}/brief-note",
                new UpdateBriefNoteRequest(new string('n', 501)),
                Json))
            {
                Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
            }

            using (var cleared = await client.PutAsJsonAsync(
                $"/api/person-conditions/{report.Id}/brief-note", new UpdateBriefNoteRequest("  "), Json))
            {
                Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
                Assert.Null((await cleared.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.BriefNote);
            }

            using (var admin = await client.PutAsJsonAsync(
                $"/api/person-conditions/{report.Id}/administrative",
                new UpdateAdministrativeRequest(11, 12, null, null, null, null, null, null, null),
                Json))
            {
                Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
            }

            var withStatuses = (await client.GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{report.Id}", Json))!;
            Assert.Equal(11, withStatuses.LhdCaseStatusId);
            Assert.Equal(12, withStatuses.StateCaseStatusId);
        }
        finally
        {
            await Clear();
        }

        using var missing = await client.PutAsJsonAsync(
            "/api/person-conditions/987654/brief-note", new UpdateBriefNoteRequest("note"), Json);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    /// <summary>
    /// A deleted report is kept and still read, carrying when it was deleted;
    /// deleting it again keeps that moment; a restore clears it; and a report
    /// nobody has is a 404 to both.
    /// </summary>
    [Fact]
    public async Task AReportDeletedIsKeptAndARestoreBringsItBack()
    {
        await Clear();

        var person = await PersonId("Doe, ");
        var condition = await ActiveConditionId();
        var client = api.CreateClient();

        try
        {
            var report = await Open(new CreatePersonConditionRequest(person, condition));

            using (var deleted = await client.DeleteAsync($"/api/person-conditions/{report.Id}"))
            {
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }

            var read = (await client.GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{report.Id}", Json))!;
            Assert.NotNull(read.DeletedAt);
            Assert.Equal(1, await Reports());

            using (var again = await client.DeleteAsync($"/api/person-conditions/{report.Id}"))
            {
                Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
            }

            var readAgain = (await client.GetFromJsonAsync<PersonConditionResponse>(
                $"/api/person-conditions/{report.Id}", Json))!;
            Assert.Equal(read.DeletedAt, readAgain.DeletedAt);

            using (var restored = await client.PostAsync($"/api/person-conditions/{report.Id}/restore", null))
            {
                Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
                Assert.Null((await restored.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.DeletedAt);
            }
        }
        finally
        {
            await Clear();
        }

        using var missingDelete = await client.DeleteAsync("/api/person-conditions/987654");
        Assert.Equal(HttpStatusCode.NotFound, missingDelete.StatusCode);

        using var missingRestore = await client.PostAsync("/api/person-conditions/987654/restore", null);
        Assert.Equal(HttpStatusCode.NotFound, missingRestore.StatusCode);
    }

    /// <summary>
    /// A body that is not a report at all: the framework refuses to bind it
    /// and <c>UseStatusCodePages</c> turns that into the one error shape this
    /// tier returns.
    /// </summary>
    [Fact]
    public async Task ABodyThatIsNotJsonIsAProblemDetails()
    {
        using var body = new StringContent(
            "a case of plague please", System.Text.Encoding.UTF8, "application/json");

        using var response = await api.CreateClient().PostAsync("/api/person-conditions", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Opens a report that is expected to succeed, and answers with it.
    /// </summary>
    [Fact]
    public async Task CreatingACaseAutoAssignsTheConditionsAutoAssignForm()
    {
        await Clear();
        await ClearForms();

        try
        {
            var person = await PersonId("Rodriguez, Maria");
            var condition = await ActiveConditionId();
            var formId = await BuildForm(condition, autoAssign: true);

            var report = await Open(new CreatePersonConditionRequest(person, condition));

            var assignments = await CaseForms(report.Id);
            Assert.Equal(formId, Assert.Single(assignments).FormId);
        }
        finally
        {
            await Clear();
            await ClearForms();
        }
    }

    [Fact]
    public async Task ANonAutoFormForTheSameConditionIsNotAssigned()
    {
        await Clear();
        await ClearForms();

        try
        {
            var person = await PersonId("Rodriguez, Maria");
            var condition = await ActiveConditionId();
            var autoFormId = await BuildForm(condition, autoAssign: true);
            await BuildForm(condition, autoAssign: false);

            var report = await Open(new CreatePersonConditionRequest(person, condition));

            var assignments = await CaseForms(report.Id);
            Assert.Equal(autoFormId, Assert.Single(assignments).FormId);
        }
        finally
        {
            await Clear();
            await ClearForms();
        }
    }

    [Fact]
    public async Task ACaseForAConditionWithNoAutoAssignFormIsLeftWithNone()
    {
        await Clear();
        await ClearForms();

        try
        {
            var person = await PersonId("Rodriguez, Maria");
            var condition = await ActiveConditionId();
            await BuildForm(condition, autoAssign: false);

            var report = await Open(new CreatePersonConditionRequest(person, condition));

            Assert.Empty(await CaseForms(report.Id));
        }
        finally
        {
            await Clear();
            await ClearForms();
        }
    }

    /// <summary>
    /// The legacy workflow walked end to end as the administrator, who holds
    /// every privilege: eleven moves through every transition but the
    /// assignment, each answering the state it lands in and leaving exactly one
    /// history row naming the move, the mover, both states and the note. The
    /// GET then reads the same history newest first, with the mover's name.
    /// </summary>
    [Fact]
    public async Task EveryTransitionMovesTheReportAndWritesOneHistoryRow()
    {
        await Clear();

        try
        {
            var client = api.CreateClient();
            var administrator = await Administrator(client);
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId(), AgencyId: 1));

            (string Transition, string From, string To)[] walk =
            [
                (WorkflowRules.AcceptCode, WorkflowRules.AssignedToLhdCode, WorkflowRules.AcceptedCode),
                (WorkflowRules.InvestigateCode, WorkflowRules.AcceptedCode, WorkflowRules.UnderInvestigationCode),
                (WorkflowRules.CloseCode, WorkflowRules.UnderInvestigationCode, WorkflowRules.ClosedCode),
                (WorkflowRules.ApproveCode, WorkflowRules.ClosedCode, WorkflowRules.ApprovedByStateCode),
                (WorkflowRules.ReopenCode, WorkflowRules.ApprovedByStateCode, WorkflowRules.UnderInvestigationCode),
                (WorkflowRules.CloseCode, WorkflowRules.UnderInvestigationCode, WorkflowRules.ClosedCode),
                (WorkflowRules.RejectCode, WorkflowRules.ClosedCode, WorkflowRules.RejectedByStateCode),
                (WorkflowRules.InvestigateCode, WorkflowRules.RejectedByStateCode, WorkflowRules.UnderInvestigationCode),
                (WorkflowRules.RouteCode, WorkflowRules.UnderInvestigationCode, WorkflowRules.AssignedToLhdCode),
                (WorkflowRules.DoNotInvestigateCode, WorkflowRules.AssignedToLhdCode, WorkflowRules.NotInvestigatedCode),
                (WorkflowRules.ReopenCode, WorkflowRules.NotInvestigatedCode, WorkflowRules.UnderInvestigationCode),
            ];

            var step = 0;

            foreach (var (transition, from, to) in walk)
            {
                step++;

                var moved = await Moved(client, report.Id, new WorkflowTransitionRequest(
                    transition, $"step {step}", AgencyId: transition == WorkflowRules.RouteCode ? 1 : null));

                Assert.Equal(to, moved.WorkflowState);

                var rows = await History(report.Id);
                Assert.Equal(step, rows.Count);

                var newest = rows[^1];
                Assert.Equal(transition, newest.Transition);
                Assert.Equal(from, newest.FromState);
                Assert.Equal(to, newest.ToState);
                Assert.Equal($"step {step}", newest.Note);
                Assert.Equal(administrator.Id, newest.UserId);
                Assert.InRange(newest.OccurredAt, DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddMinutes(5));
            }

            var workflow = await Workflow(client, report.Id);

            Assert.Equal(WorkflowRules.UnderInvestigationCode, workflow.WorkflowState);
            Assert.Equal(walk.Length, workflow.History.Count);
            Assert.Equal(WorkflowRules.ReopenCode, workflow.History[0].Transition);
            Assert.Equal("step 11", workflow.History[0].Note);
            Assert.Equal(administrator.DisplayName, workflow.History[0].UserDisplayName);
            Assert.Equal(WorkflowRules.AcceptCode, workflow.History[^1].Transition);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// Assigning an investigator names a user from the investigator list on the
    /// report and puts it under investigation, with its own history row.
    /// </summary>
    [Fact]
    public async Task AssignInvestigatorNamesAnActiveUserOnTheReport()
    {
        await Clear();

        try
        {
            var client = api.CreateClient();
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId()));
            await Moved(client, report.Id, new WorkflowTransitionRequest(WorkflowRules.AcceptCode));

            var investigators = (await client.GetFromJsonAsync<IReadOnlyList<InvestigatorResponse>>("/api/investigators", Json))!;
            var investigator = investigators[0];

            var assigned = await Moved(client, report.Id,
                new WorkflowTransitionRequest(WorkflowRules.AssignInvestigatorCode, InvestigatorId: investigator.Id));

            Assert.Equal(investigator.Id, assigned.InvestigatorId);
            Assert.Equal(WorkflowRules.UnderInvestigationCode, assigned.WorkflowState);

            var workflow = await Workflow(client, report.Id);
            Assert.Equal(investigator.Id, workflow.InvestigatorId);
            Assert.Equal(WorkflowRules.AssignInvestigatorCode, workflow.History[0].Transition);
            Assert.Null(workflow.History[0].Note);
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>
    /// A move the report's state does not allow is refused with a Problem
    /// Details that names the state and the moves it does allow, and nothing is
    /// written: no history row, and the state is where it was.
    /// </summary>
    [Fact]
    public async Task AMoveTheStateDoesNotAllowIsAProblemDetailsAndWritesNoRow()
    {
        await Clear();

        try
        {
            var client = api.CreateClient();
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId()));

            var detail = await RefusedMove(client, report.Id,
                new WorkflowTransitionRequest(WorkflowRules.ApproveCode), HttpStatusCode.BadRequest);

            Assert.Contains($"A report at {WorkflowRules.AssignedToLhdCode} may not {WorkflowRules.ApproveCode}",
                detail, StringComparison.Ordinal);
            Assert.Contains(WorkflowRules.AcceptCode, detail, StringComparison.Ordinal);
            Assert.Empty(await History(report.Id));
            Assert.Equal(WorkflowRules.AssignedToLhdCode, (await Workflow(client, report.Id)).WorkflowState);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ARouteToAnAgencyNotOnFileIsAProblemDetails()
    {
        await Clear();

        try
        {
            var client = api.CreateClient();
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId()));

            var detail = await RefusedMove(client, report.Id,
                new WorkflowTransitionRequest(WorkflowRules.RouteCode, AgencyId: 99), HttpStatusCode.BadRequest);

            Assert.Contains("agencyId 99 is not an agency", detail, StringComparison.Ordinal);
            Assert.Empty(await History(report.Id));
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnAssignmentToAUserNotOnFileIsAProblemDetails()
    {
        await Clear();

        try
        {
            var client = api.CreateClient();
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId()));
            await Moved(client, report.Id, new WorkflowTransitionRequest(WorkflowRules.AcceptCode));

            var detail = await RefusedMove(client, report.Id,
                new WorkflowTransitionRequest(WorkflowRules.AssignInvestigatorCode, InvestigatorId: 987654),
                HttpStatusCode.BadRequest);

            Assert.Contains("investigatorId 987654 is not an active user", detail, StringComparison.Ordinal);
            Assert.Single(await History(report.Id));
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AMoveOnAReportNobodyHasIsAProblemDetails()
    {
        var client = api.CreateClient();

        await RefusedMove(client, 987654, new WorkflowTransitionRequest(WorkflowRules.AcceptCode), HttpStatusCode.NotFound);

        using var read = await client.GetAsync("/api/person-conditions/987654/workflow");
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    /// <summary>
    /// The options a fresh report offers are the start state's three moves for
    /// the administrator, and only the department's two for a user whose role
    /// holds view_cases and edit_cases alone — who is refused a route with a
    /// 403 and no row written, and may accept.
    /// </summary>
    [Fact]
    public async Task TheOptionsAreTheStatesMovesNarrowedToTheCallersPrivileges()
    {
        await Clear();

        try
        {
            var admin = api.CreateClient();
            var report = await Open(new CreatePersonConditionRequest(
                await PersonId("Rodriguez, Maria"), await ActiveConditionId()));

            var forAdministrator = await Workflow(admin, report.Id);
            Assert.Equal(
                new[] { WorkflowRules.AcceptCode, WorkflowRules.RouteCode, WorkflowRules.DoNotInvestigateCode },
                forAdministrator.Options);
            Assert.Empty(forAdministrator.History);

            using var editor = await SignedInCaseEditor();

            var forEditor = await Workflow(editor, report.Id);
            Assert.Equal(new[] { WorkflowRules.AcceptCode, WorkflowRules.DoNotInvestigateCode }, forEditor.Options);

            var refused = await RefusedMove(editor, report.Id,
                new WorkflowTransitionRequest(WorkflowRules.RouteCode, AgencyId: 1), HttpStatusCode.Forbidden);
            Assert.Contains(Privileges.RouteCases, refused, StringComparison.Ordinal);
            Assert.Empty(await History(report.Id));

            var accepted = await Moved(editor, report.Id, new WorkflowTransitionRequest(WorkflowRules.AcceptCode));
            Assert.Equal(WorkflowRules.AcceptedCode, accepted.WorkflowState);
        }
        finally
        {
            await Clear();
        }
    }

    private async Task<PersonConditionResponse> Open(CreatePersonConditionRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync("/api/person-conditions", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
    }

    /// <summary>Makes a workflow move that is expected to succeed, and answers with the report as moved.</summary>
    private static async Task<PersonConditionResponse> Moved(HttpClient client, int id, WorkflowTransitionRequest request)
    {
        using var response = await client.PutAsJsonAsync($"/api/person-conditions/{id}/workflow", request, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
    }

    /// <summary>
    /// Makes a workflow move that is expected to be refused with that status,
    /// and answers with the <c>detail</c> of the Problem Details.
    /// </summary>
    private static async Task<string> RefusedMove(
        HttpClient client, int id, WorkflowTransitionRequest request, HttpStatusCode expected)
    {
        using var response = await client.PutAsJsonAsync($"/api/person-conditions/{id}/workflow", request, Json);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return problem.RootElement.GetProperty("detail").GetString()!;
    }

    private static async Task<WorkflowResponse> Workflow(HttpClient client, int id) =>
        (await client.GetFromJsonAsync<WorkflowResponse>($"/api/person-conditions/{id}/workflow", Json))!;

    /// <summary>The report's history rows as stored, oldest first.</summary>
    private async Task<List<WorkflowHistoryEntry>> History(int id)
    {
        await using var context = server.NewContext();

        return await context.WorkflowHistory
            .Where(entry => entry.PersonConditionId == id)
            .OrderBy(entry => entry.Id)
            .ToListAsync();
    }

    /// <summary>The administrator as a user, for the id and name a history row carries.</summary>
    private static async Task<UserResponse> Administrator(HttpClient admin)
    {
        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;

        return users.Items.Single(user => user.UserName == SurvsysApi.AdministratorUserName);
    }

    /// <summary>
    /// A client signed in as a user whose one role holds view_cases and
    /// edit_cases and nothing else, created by the administrator over the wire
    /// as <see cref="PrivilegeRulesTests"/> creates its reader, and found again
    /// on a later run rather than created twice.
    /// </summary>
    private async Task<HttpClient> SignedInCaseEditor()
    {
        const string editorPassword = "case-editor-password";
        var admin = api.CreateClient();

        var roles = (await admin.GetFromJsonAsync<PagedResult<RoleResponse>>("/api/roles", Json))!;
        var role = roles.Items.SingleOrDefault(role => role.Name == "Case editor")
            ?? await Created<RoleResponse>(admin, "/api/roles",
                new SaveRoleRequest("Case editor", [Privileges.ViewCases, Privileges.EditCases]));

        var users = (await admin.GetFromJsonAsync<PagedResult<UserResponse>>("/api/users", Json))!;
        if (users.Items.All(user => user.UserName != "case-editor"))
        {
            await Created<UserResponse>(admin, "/api/users",
                new SaveUserRequest("case-editor", "Case Editor", true, [role.Id], editorPassword));
        }

        var editor = api.CreateAnonymousClient();
        await api.SignIn(editor, "case-editor", editorPassword);

        return editor;
    }

    private static async Task<T> Created<T>(HttpClient client, string url, object body)
    {
        using var response = await client.PostAsJsonAsync(url, body, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    /// <summary>Builds a form for a condition, auto-assign or not, and answers its id.</summary>
    private async Task<int> BuildForm(int conditionId, bool autoAssign)
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/forms",
            new SaveFormRequest($"Supplement {Guid.NewGuid():N}", conditionId, AutoAssign: autoAssign),
            Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<FormDetailResponse>(Json))!.Id;
    }

    /// <summary>The forms assigned to a case.</summary>
    private async Task<IReadOnlyList<FormAssignmentResponse>> CaseForms(int personConditionId)
    {
        using var response = await api.CreateClient().GetAsync($"/api/cases/{personConditionId}/forms");
        response.EnsureSuccessStatusCode();

        return (await response.Content
            .ReadFromJsonAsync<IReadOnlyList<FormAssignmentResponse>>(Json))!;
    }

    /// <summary>
    /// Takes every form assignment and then every form out of the tables, so a
    /// test leaves no form behind; the assignments go first because a form a
    /// case holds cannot be deleted out from under it.
    /// </summary>
    private async Task ClearForms()
    {
        await using var context = server.NewContext();

        await context.FormAssignments.ExecuteDeleteAsync();
        await context.Forms.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Posts a report that is expected to be refused, and answers with the
    /// <c>detail</c> of the Problem Details it was refused with — having
    /// asserted the shape of it, which every refusal from this tier shares.
    /// </summary>
    private async Task<string> Refused(CreatePersonConditionRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync("/api/person-conditions", request, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = problem.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("correlationId").GetString()));

        return root.GetProperty("detail").GetString()!;
    }

    /// <summary>
    /// The identifier of a seeded person, found over the wire by the name
    /// <see cref="SqlServerFixture.PersonsByName"/> writes them under, so that
    /// nothing here depends on an identity value the fixture happened to
    /// assign.
    /// </summary>
    private async Task<int> PersonId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/persons?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<PersonResponse>>(Json))!;

        return page.Items
            .Single(person => $"{person.LastName}, {person.FirstName}" == named)
            .Id;
    }

    /// <summary>
    /// A seeded condition a report may be opened for. Malaria, which
    /// <c>Fixtures/conditions.sql</c> seeds as active — the legacy
    /// environment's own example was Plague, and these fixtures hold the seven
    /// real conditions session 5 chose instead.
    /// </summary>
    private Task<int> ActiveConditionId() => ConditionId("Malaria");

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }

    private async Task<int> Reports()
    {
        await using var context = server.NewContext();

        return await context.PersonConditions.CountAsync();
    }

    /// <summary>
    /// Takes every report out of the table, so that what record number the
    /// next one gets is a fact about this test rather than about whichever
    /// test ran before it.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditions.ExecuteDeleteAsync();
    }
}
