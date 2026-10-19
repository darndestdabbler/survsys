using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The treatment endpoints, against the real database: that a treatment is
/// created and read back with the conditions it is listed for, that a save
/// replaces that set, that the list comes back ordered by name, that an unknown
/// id and a condition that does not exist are refused, and that a condition's
/// treatment list reads back the treatments listed for it; and that a case's
/// treatment record is added, listed, saved and removed, a treatment not in the
/// catalogue or a stop before the start is refused, and an unknown case or
/// record is not found.
/// </summary>
/// <remarks>
/// Driven through the endpoints. The treatments table is cleared before and
/// after each test, which cascades to the condition-treatment links, so each
/// test starts and ends with no treatments; the seeded conditions are left as
/// they are, since the suite shares the one database and nothing here changes
/// them.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class TreatmentEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ATreatmentIsCreatedAndReadBackWithItsConditions()
    {
        await Clear();

        try
        {
            var malaria = await ConditionId("Malaria");

            var created = await Add(new SaveTreatmentRequest(
                "Doxycycline",
                Active: true,
                DefaultTreatment: true,
                ConditionIds: [malaria]));

            Assert.Equal("Doxycycline", created.Name);
            Assert.True(created.Active);
            Assert.True(created.DefaultTreatment);
            Assert.Equal(malaria, Assert.Single(created.Conditions).Id);
            Assert.Equal("Malaria", created.Conditions[0].Name);

            using var detail = await api.CreateClient().GetAsync($"/api/treatments/{created.Id}");
            detail.EnsureSuccessStatusCode();
            var read = (await detail.Content.ReadFromJsonAsync<TreatmentDetailResponse>(Json))!;
            Assert.Equal("Doxycycline", read.Name);
            Assert.Equal("Malaria", Assert.Single(read.Conditions).Name);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ASaveReplacesTheConditionList()
    {
        await Clear();

        try
        {
            var malaria = await ConditionId("Malaria");
            var lyme = await ConditionId("Lyme disease");

            var created = await Add(new SaveTreatmentRequest("Doxycycline", ConditionIds: [malaria]));

            using var response = await api.CreateClient().PutAsJsonAsync(
                $"/api/treatments/{created.Id}",
                new SaveTreatmentRequest("Doxycycline hyclate", Active: false, ConditionIds: [lyme]),
                Json);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var saved = (await response.Content.ReadFromJsonAsync<TreatmentDetailResponse>(Json))!;
            Assert.Equal("Doxycycline hyclate", saved.Name);
            Assert.False(saved.Active);
            Assert.Equal(lyme, Assert.Single(saved.Conditions).Id);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task TheListComesBackOrderedByNameWithTheConditionCount()
    {
        await Clear();

        try
        {
            var malaria = await ConditionId("Malaria");
            var lyme = await ConditionId("Lyme disease");

            await Add(new SaveTreatmentRequest("Rifampin"));
            await Add(new SaveTreatmentRequest("Amoxicillin", ConditionIds: [malaria, lyme]));

            using var response = await api.CreateClient().GetAsync("/api/treatments?pageSize=200");
            response.EnsureSuccessStatusCode();
            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<TreatmentResponse>>(Json))!;

            Assert.Equal(2, page.Items.Count);
            Assert.Equal("Amoxicillin", page.Items[0].Name);
            Assert.Equal(2, page.Items[0].ConditionCount);
            Assert.Equal("Rifampin", page.Items[1].Name);
            Assert.Equal(0, page.Items[1].ConditionCount);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownTreatmentIsNotFound()
    {
        using var get = await api.CreateClient().GetAsync("/api/treatments/987654321");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using var put = await api.CreateClient().PutAsJsonAsync(
            "/api/treatments/987654321",
            new SaveTreatmentRequest("Doxycycline"),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task ATreatmentForAConditionThatDoesNotExistIsRefused()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            "/api/treatments",
            new SaveTreatmentRequest("Doxycycline", ConditionIds: [987654321]),
            Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AConditionsTreatmentListIsRead()
    {
        await Clear();

        try
        {
            var malaria = await ConditionId("Malaria");
            var lyme = await ConditionId("Lyme disease");

            await Add(new SaveTreatmentRequest("Chloroquine", ConditionIds: [malaria]));
            await Add(new SaveTreatmentRequest("Vitamin A", ConditionIds: [lyme]));

            using var response = await api.CreateClient()
                .GetAsync($"/api/conditions/{malaria}/treatments?pageSize=200");
            response.EnsureSuccessStatusCode();
            var page = (await response.Content
                .ReadFromJsonAsync<PagedResult<TreatmentResponse>>(Json))!;

            Assert.Equal("Chloroquine", Assert.Single(page.Items).Name);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownConditionsTreatmentListIsNotFound()
    {
        using var response = await api.CreateClient()
            .GetAsync("/api/conditions/987654321/treatments");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ACaseTreatmentIsAddedListedSavedAndRemoved()
    {
        await Clear();
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var doxycycline = await Add(new SaveTreatmentRequest("Doxycycline"));
            var chloroquine = await Add(new SaveTreatmentRequest("Chloroquine"));

            var created = await AddRecord(caseId, new SaveTreatmentRecordRequest(
                doxycycline.Id,
                new DateOnly(2026, 3, 1),
                new DateOnly(2026, 3, 14),
                "100 mg twice daily",
                "Tolerated well"));
            Assert.Equal("Doxycycline", created.TreatmentName);

            using var list = await api.CreateClient().GetAsync($"/api/cases/{caseId}/treatments");
            list.EnsureSuccessStatusCode();
            var read = Assert.Single((await list.Content
                .ReadFromJsonAsync<IReadOnlyList<TreatmentRecordResponse>>(Json))!);
            Assert.Equal("Doxycycline", read.TreatmentName);
            Assert.Equal(new DateOnly(2026, 3, 1), read.TreatmentDate);
            Assert.Equal(new DateOnly(2026, 3, 14), read.StopTreatmentDate);
            Assert.Equal("100 mg twice daily", read.Dose);
            Assert.Equal("Tolerated well", read.Comment);

            using var edit = await api.CreateClient().PutAsJsonAsync(
                $"/api/cases/treatments/{created.Id}",
                new SaveTreatmentRecordRequest(chloroquine.Id, Dose: "500 mg"),
                Json);
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
            var saved = (await edit.Content.ReadFromJsonAsync<TreatmentRecordResponse>(Json))!;
            Assert.Equal("Chloroquine", saved.TreatmentName);
            Assert.Equal("500 mg", saved.Dose);
            Assert.Null(saved.Comment);

            using var removed = await api.CreateClient()
                .DeleteAsync($"/api/cases/treatments/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

            using var after = await api.CreateClient().GetAsync($"/api/cases/{caseId}/treatments");
            after.EnsureSuccessStatusCode();
            Assert.Empty((await after.Content
                .ReadFromJsonAsync<IReadOnlyList<TreatmentRecordResponse>>(Json))!);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task ACaseTreatmentNotInTheCatalogueIsRefused()
    {
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/treatments",
                new SaveTreatmentRecordRequest(987654321),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
        }
    }

    [Fact]
    public async Task ACaseTreatmentStoppedBeforeItStartedIsRefused()
    {
        await Clear();
        int caseId = 0;

        try
        {
            caseId = await CreateCase("Rodriguez, Maria", "Malaria");
            var doxycycline = await Add(new SaveTreatmentRequest("Doxycycline"));

            using var response = await api.CreateClient().PostAsJsonAsync(
                $"/api/cases/{caseId}/treatments",
                new SaveTreatmentRecordRequest(
                    doxycycline.Id,
                    new DateOnly(2026, 3, 14),
                    new DateOnly(2026, 3, 1)),
                Json);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            await DeleteCase(caseId);
            await Clear();
        }
    }

    [Fact]
    public async Task AnUnknownCasesTreatmentsAreNotFound()
    {
        using var list = await api.CreateClient().GetAsync("/api/cases/987654321/treatments");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);

        await Clear();

        try
        {
            var doxycycline = await Add(new SaveTreatmentRequest("Doxycycline"));

            using var add = await api.CreateClient().PostAsJsonAsync(
                "/api/cases/987654321/treatments",
                new SaveTreatmentRecordRequest(doxycycline.Id),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, add.StatusCode);

            using var edit = await api.CreateClient().PutAsJsonAsync(
                "/api/cases/treatments/987654321",
                new SaveTreatmentRecordRequest(doxycycline.Id),
                Json);
            Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        }
        finally
        {
            await Clear();
        }

        using var removed = await api.CreateClient().DeleteAsync("/api/cases/treatments/987654321");
        Assert.Equal(HttpStatusCode.NotFound, removed.StatusCode);
    }

    private async Task<TreatmentDetailResponse> Add(SaveTreatmentRequest request)
    {
        using var response = await api.CreateClient().PostAsJsonAsync("/api/treatments", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TreatmentDetailResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonTreatments.ExecuteDeleteAsync();
        await context.Treatments.ExecuteDeleteAsync();
    }

    private async Task<TreatmentRecordResponse> AddRecord(
        int personConditionId,
        SaveTreatmentRecordRequest request)
    {
        using var response = await api.CreateClient()
            .PostAsJsonAsync($"/api/cases/{personConditionId}/treatments", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TreatmentRecordResponse>(Json))!;
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
