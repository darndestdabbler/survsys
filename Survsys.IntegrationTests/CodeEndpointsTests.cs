using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The code list endpoints, against the real database: that a seeded list
/// reads back in its order, that a code is added, saved over and retired out
/// of the default read, that a code already on the list and an unknown list
/// are refused, and that the test types are narrowed by a condition mapped to
/// them and left whole for a condition with none.
/// </summary>
/// <remarks>
/// Driven through the endpoints. Every code a test adds starts with
/// <see cref="Marker"/>, and those codes are deleted before and after each
/// test, which cascades to their condition links; the seeded codes are left as
/// they are.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CodeEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Marker = "ITEST-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task ASeededListReadsBackInItsOrder()
    {
        var statuses = await List($"/api/codes/{CodeLists.CaseStatus}");

        Assert.Equal(10, statuses.Count);
        Assert.Equal("UNK", statuses[0].Code);
        Assert.Equal("Unknown", statuses[0].Description);
        Assert.Equal(statuses.Select(code => code.SortOrder).Order(), statuses.Select(code => code.SortOrder));
        Assert.All(statuses, code => Assert.Equal(CodeLists.CaseStatus, code.List));
    }

    [Fact]
    public async Task ACodeIsAddedSavedOverAndRetired()
    {
        await Clear();

        try
        {
            var created = await Add(CodeLists.Race, new SaveCodeRequest($"{Marker}R", "Test race", SortOrder: 99));
            Assert.Equal($"{Marker}R", created.Code);
            Assert.Equal("Test race", created.Description);
            Assert.True(created.Active);
            Assert.Contains(await List($"/api/codes/{CodeLists.Race}"), code => code.Id == created.Id);

            using var saved = await api.CreateClient().PutAsJsonAsync(
                $"/api/codes/{created.Id}",
                new SaveCodeRequest($"{Marker}R2", "  Renamed  ", SortOrder: 1, Active: false),
                Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var read = (await saved.Content.ReadFromJsonAsync<CodeResponse>(Json))!;
            Assert.Equal($"{Marker}R2", read.Code);
            Assert.Equal("Renamed", read.Description);
            Assert.False(read.Active);

            Assert.DoesNotContain(await List($"/api/codes/{CodeLists.Race}"), code => code.Id == created.Id);
            Assert.Contains(
                await List($"/api/codes/{CodeLists.Race}?inactive=true"),
                code => code.Id == created.Id && !code.Active);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ACodeAlreadyOnTheListIsAConflict()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            $"/api/codes/{CodeLists.Race}",
            new SaveCodeRequest("w"),
            Json);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownListOrCodeIsNotFound()
    {
        using var get = await api.CreateClient().GetAsync("/api/codes/no_such_list");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using var put = await api.CreateClient().PutAsJsonAsync(
            "/api/codes/987654321",
            new SaveCodeRequest("X"),
            Json);
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
    }

    [Fact]
    public async Task ACodeForAConditionThatDoesNotExistIsRefused()
    {
        using var response = await api.CreateClient().PostAsJsonAsync(
            $"/api/codes/{CodeLists.TestType}",
            new SaveCodeRequest($"{Marker}T", ConditionIds: [987654321]),
            Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TestTypesAreNarrowedByAMappedConditionAndWholeForAnUnmappedOne()
    {
        await Clear();

        try
        {
            var malaria = await ConditionId("Malaria");
            var mapped = await Add(
                CodeLists.TestType,
                new SaveCodeRequest($"{Marker}Smear", ConditionIds: [malaria]));
            Assert.Equal(malaria, Assert.Single(mapped.ConditionIds));

            var forMalaria = await List($"/api/codes/{CodeLists.TestType}?conditionId={malaria}");
            Assert.Contains(forMalaria, code => code.Id == mapped.Id);
            Assert.All(forMalaria, code => Assert.Contains(malaria, code.ConditionIds));

            var whole = await List($"/api/codes/{CodeLists.TestType}");
            var forNone = await List($"/api/codes/{CodeLists.TestType}?conditionId=987654321");
            Assert.Equal(whole.Select(code => code.Id), forNone.Select(code => code.Id));
            Assert.True(whole.Count > forMalaria.Count);
        }
        finally
        {
            await Clear();
        }
    }

    private async Task<IReadOnlyList<CodeResponse>> List(string path)
    {
        using var response = await api.CreateClient().GetAsync(path);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<List<CodeResponse>>(Json))!;
    }

    private async Task<CodeResponse> Add(string list, SaveCodeRequest request)
    {
        using var response = await api.CreateClient().PostAsJsonAsync($"/api/codes/{list}", request, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<CodeResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.CodeValues.Where(code => code.Code.StartsWith(Marker)).ExecuteDeleteAsync();
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
