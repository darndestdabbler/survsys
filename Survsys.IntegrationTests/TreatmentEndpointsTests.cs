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
/// treatment list reads back the treatments listed for it.
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

    private async Task<TreatmentDetailResponse> Add(SaveTreatmentRequest request)
    {
        using var response = await api.CreateClient().PostAsJsonAsync("/api/treatments", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TreatmentDetailResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.Treatments.ExecuteDeleteAsync();
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
