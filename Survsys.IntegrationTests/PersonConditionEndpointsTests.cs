using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
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
/// the context, since this tier has no delete endpoint and is not getting one.
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

    private async Task<PersonConditionResponse> Open(CreatePersonConditionRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync("/api/person-conditions", request, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
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
