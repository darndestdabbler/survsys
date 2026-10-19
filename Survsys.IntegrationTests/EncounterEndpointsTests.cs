using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The encounter endpoints, against the real database: that an encounter is
/// added to a case at a facility, listed with the facility's name, saved over
/// and removed; that a missing date and a facility not on file are refused; and
/// that an unknown case or encounter is not found.
/// </summary>
/// <remarks>
/// Driven through the endpoints. Each test opens its own case and deletes it in
/// a finally, which cascades to its encounters, and deletes the facility it
/// made, so a test leaves nothing behind.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class EncounterEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AnEncounterIsAddedListedSavedAndRemoved()
    {
        int caseId = 0;
        int facilityId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            facilityId = await CreateFacility("Encounter Test Hospital");

            var created = await Add(caseId, new SaveEncounterRequest(
                new DateOnly(2026, 3, 2),
                "ED",
                facilityId,
                "Seen in the emergency department"));
            Assert.Equal("Encounter Test Hospital", created.FacilityName);

            using var list = await api.CreateClient().GetAsync($"/api/cases/{caseId}/encounters");
            list.EnsureSuccessStatusCode();
            var read = Assert.Single((await list.Content
                .ReadFromJsonAsync<IReadOnlyList<EncounterResponse>>(Json))!);
            Assert.Equal(new DateOnly(2026, 3, 2), read.EncounterDate);
            Assert.Equal("ED", read.EncounterType);
            Assert.Equal(facilityId, read.FacilityId);
            Assert.Equal("Encounter Test Hospital", read.FacilityName);
            Assert.Equal("Seen in the emergency department", read.Description);

            using var edit = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/encounters/{created.Id}",
                new SaveEncounterRequest(new DateOnly(2026, 3, 5), "OP", Description: "Follow-up"),
                Json);
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            var saved = (await edit.Content.ReadFromJsonAsync<EncounterResponse>(Json))!;
            Assert.Equal(new DateOnly(2026, 3, 5), saved.EncounterDate);
            Assert.Equal("OP", saved.EncounterType);
            Assert.Null(saved.FacilityId);
            Assert.Null(saved.FacilityName);

            using var removed = await api.CreateClient()
                .DeleteAsync($"/api/cases/encounters/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            using var after = await api.CreateClient().GetAsync($"/api/cases/{caseId}/encounters");
            after.EnsureSuccessStatusCode();
            Assert.Empty((await after.Content
                .ReadFromJsonAsync<IReadOnlyList<EncounterResponse>>(Json))!);
        }
        finally
        {
            await DeleteCase(caseId);
            await DeleteFacility(facilityId);
        }
    }

    [Fact]
    public async Task AnEncounterWithNoDateIsRefused()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/encounters",
                new SaveEncounterRequest(null, "ED"),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task AnEncounterAtAFacilityNotOnFileIsRefused()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/encounters",
                new SaveEncounterRequest(new DateOnly(2026, 3, 2), FacilityId: 987654321),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            await using var context = server.NewContext();
            Assert.False(await context.Encounters.AnyAsync(encounter =>
                encounter.PersonConditionId == caseId));
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task AnUnknownCaseOrEncounterIsNotFound()
    {
        using var list = await api.CreateClient().GetAsync("/api/cases/987654321/encounters");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        using var add = await api.CreateClient().PostAsJsonAsync(
            "/api/cases/987654321/encounters",
            new SaveEncounterRequest(new DateOnly(2026, 3, 2)),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);

        using var edit = await api.CreateClient().PutAsJsonAsync(
            "/api/cases/encounters/987654321",
            new SaveEncounterRequest(new DateOnly(2026, 3, 2)),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);

        using var removed = await api.CreateClient().DeleteAsync("/api/cases/encounters/987654321");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private async Task<EncounterResponse> Add(int personConditionId, SaveEncounterRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync($"/api/cases/{personConditionId}/encounters", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<EncounterResponse>(Json))!;
    }

    private async Task<int> CreateFacility(string name)
    {
        await using var context = server.NewContext();
        var facility = new Facility { Name = name, AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        return facility.Id;
    }

    private async Task DeleteFacility(int facilityId)
    {
        if (facilityId == 0)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.Facilities.Where(facility => facility.Id == facilityId).ExecuteDeleteAsync();
    }

    private async Task<int> CreateCase(string personNamed, string conditionNamed)
    {
        var person = await PersonId(personNamed);
        var condition = await ConditionId(conditionNamed);

        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/person-conditions",
            new CreatePersonConditionRequest(person, condition),
            Json);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!.Id;
    }

    private async Task DeleteCase(int personConditionId)
    {
        if (personConditionId == 0)
        {
            return;
        }

        await using var context = server.NewContext();
        await context.PersonConditions
            .Where(report => report.Id == personConditionId)
            .ExecuteDeleteAsync();
    }

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

    private async Task<int> ConditionId(string named)
    {
        using var response = await api.CreateClient().GetAsync("/api/conditions?pageSize=200");
        response.EnsureSuccessStatusCode();

        var page = (await response.Content
            .ReadFromJsonAsync<PagedResult<ConditionResponse>>(Json))!;

        return page.Items.Single(condition => condition.Name == named).Id;
    }
}
