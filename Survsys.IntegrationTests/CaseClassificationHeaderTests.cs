using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// That every kind of route that classifies a case answers with what the
/// classification decided, in its <see cref="CaseClassificationHeader.Name"/>
/// header: opening a case, saving its clinical, investigation and
/// administrative data, moving its workflow, adding and removing a specimen,
/// and adding, saving over and removing an encounter.
/// </summary>
/// <remarks>
/// The case is a measles case, whose rule always has a line to give, for a
/// person with no address, so nothing else is found. The person and the
/// condition are made here and cleared in a finally with everything the case
/// gathered.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CaseClassificationHeaderTests(SqlServerFixture server) : IDisposable
{
    private const string Measles = "Measles (session 97 test)";

    private const string MeaslesFinding = "Measles: the status is left to the investigation.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task EveryClassifyingRouteAnswersWithTheCasesClassification()
    {
        var personId = 0;

        try
        {
            var conditionId = await MakeCondition();
            personId = await MakePerson();
            var client = api.CreateClient();

            using var opened = await client.PostAsJsonAsync(
                "/api/person-conditions", new CreatePersonConditionRequest(personId, conditionId), Json);
            Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
            var reportId = (await opened.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
            AssertClassified(opened, reportId);

            using var clinical = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/clinical",
                new UpdateClinicalRequest(null, null, null, null, null),
                Json);
            AssertClassified(clinical, reportId);

            using var investigation = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/investigation",
                new UpdateInvestigationRequest(null, null, null, null, null, null, null, null, null, null),
                Json);
            AssertClassified(investigation, reportId);

            using var administrative = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/administrative",
                new UpdateAdministrativeRequest(null, null, null, null, null, null, null, null, null),
                Json);
            AssertClassified(administrative, reportId);

            using var workflow = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/workflow",
                new WorkflowTransitionRequest(WorkflowRules.AcceptCode),
                Json);
            AssertClassified(workflow, reportId);

            using var lab = await client.PostAsJsonAsync(
                $"/api/person-conditions/{reportId}/labs", new CreateLabRequest(), Json);
            AssertClassified(lab, reportId);
            var labId = (await lab.Content.ReadFromJsonAsync<LabDetailResponse>(Json))!.Specimen.Id;

            using var labRemoved = await client.DeleteAsync($"/api/labs/{labId}");
            Assert.Equal(HttpStatusCode.NoContent, labRemoved.StatusCode);
            AssertClassified(labRemoved, reportId);

            using var encounter = await client.PostAsJsonAsync(
                $"/api/cases/{reportId}/encounters", new SaveEncounterRequest(new DateOnly(2026, 3, 2)), Json);
            AssertClassified(encounter, reportId);
            var encounterId = (await encounter.Content.ReadFromJsonAsync<EncounterResponse>(Json))!.Id;

            using var encounterSaved = await client.PutAsJsonAsync(
                $"/api/cases/encounters/{encounterId}", new SaveEncounterRequest(new DateOnly(2026, 3, 5)), Json);
            AssertClassified(encounterSaved, reportId);

            using var encounterRemoved = await client.DeleteAsync($"/api/cases/encounters/{encounterId}");
            Assert.Equal(HttpStatusCode.NoContent, encounterRemoved.StatusCode);
            AssertClassified(encounterRemoved, reportId);
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task AReadAnswersWithNoClassification()
    {
        using var read = await api.CreateClient().GetAsync("/api/conditions");

        read.EnsureSuccessStatusCode();
        Assert.False(read.Headers.Contains(CaseClassificationHeader.Name));
    }

    private static void AssertClassified(HttpResponseMessage response, int reportId)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.RequestMessage?.RequestUri} answered {response.StatusCode}.");

        var classification = JsonSerializer.Deserialize<CaseClassificationResponse>(
            Assert.Single(response.Headers.GetValues(CaseClassificationHeader.Name)), Json)!;
        Assert.Equal(reportId, classification.ReportId);
        Assert.Null(classification.CaseStatus);
        Assert.Contains(MeaslesFinding, classification.Findings);
    }

    private async Task<int> MakeCondition()
    {
        await using var context = server.NewContext();

        var condition = await context.Conditions.SingleOrDefaultAsync(row => row.Name == Measles)
            ?? context.Conditions.Add(new Condition { Name = Measles, Active = true }).Entity;
        await context.SaveChangesAsync();

        return condition.Id;
    }

    private async Task<int> MakePerson()
    {
        await using var context = server.NewContext();

        var person = context.Persons.Add(new Person { LastName = "Header", FirstName = "Session97" }).Entity;
        await context.SaveChangesAsync();

        return person.Id;
    }

    private async Task Clear(int personId)
    {
        await using var context = server.NewContext();

        var reports = context.PersonConditions.Where(report => report.PersonId == personId).Select(report => report.Id);
        await context.Notes.Where(note => note.PersonConditionId != null && reports.Contains(note.PersonConditionId.Value))
            .ExecuteDeleteAsync();
        await context.Labs.Where(lab => lab.PersonId == personId).ExecuteDeleteAsync();
        await context.PersonConditions.Where(report => report.PersonId == personId).ExecuteDeleteAsync();
        await context.Persons.Where(person => person.Id == personId).ExecuteDeleteAsync();
        await context.Conditions.Where(row => row.Name == Measles).ExecuteDeleteAsync();
    }
}
