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
/// The hepatitis lab group endpoints, against the real database: a group
/// created with its conditions and test types, saved whole, refused where a
/// condition is already in another group or a test type is not on file, and
/// deleted.
/// </summary>
/// <remarks>
/// Every group a test adds is named with <see cref="Marker"/>, and its test
/// type is the <c>test_type</c> code <see cref="TestTypeCode"/>; both are
/// deleted before and after each test.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class HepLabGroupEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Marker = "ITEST hep";
    private const string TestTypeCode = "ITEST-HBSAG";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AGroupIsCreatedSavedWholeAndDeleted()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            var (conditionIds, testTypeId) = await Setup();
            var row = new HepLabGroupRow(0, $"{Marker} B", [conditionIds[0]], [new(testTypeId, "HBsAg", 1)]);

            using var created = await client.PostAsJsonAsync(HepLabGroupEndpoints.Path, row, Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var group = (await created.Content.ReadFromJsonAsync<HepLabGroupRow>(Json))!;

            using var saved = await client.PutAsJsonAsync(
                $"{HepLabGroupEndpoints.Path}/{group.Id}",
                group with { ConditionIds = [conditionIds[0], conditionIds[1]], TestTypes = [new(testTypeId, "Surface antigen", 3)] },
                Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var listed = Assert.Single(
                (await client.GetFromJsonAsync<List<HepLabGroupRow>>(HepLabGroupEndpoints.Path, Json))!,
                item => item.Id == group.Id);
            Assert.Equal(conditionIds.Take(2).Order(), listed.ConditionIds);
            Assert.Equal("Surface antigen", Assert.Single(listed.TestTypes).TestGroupName);

            using var taken = await client.PostAsJsonAsync(
                HepLabGroupEndpoints.Path, new HepLabGroupRow(0, $"{Marker} C", [conditionIds[1]], []), Json);
            Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);

            using var unknownTestType = await client.PostAsJsonAsync(
                HepLabGroupEndpoints.Path, new HepLabGroupRow(0, $"{Marker} C", [], [new(987654321, "X", 1)]), Json);
            Assert.Equal(HttpStatusCode.BadRequest, unknownTestType.StatusCode);

            using var deleted = await client.DeleteAsync($"{HepLabGroupEndpoints.Path}/{group.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            await using var context = server.NewContext();
            Assert.False(await context.HepLabGroupConditions.AnyAsync(item => item.HepLabGroupId == group.Id));
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>Two conditions in no group, and a test type code to group.</summary>
    private async Task<(int[] ConditionIds, int TestTypeId)> Setup()
    {
        await using var context = server.NewContext();
        var conditionIds = await context.Conditions
            .Where(item => !context.HepLabGroupConditions.Any(member => member.ConditionId == item.Id))
            .OrderBy(item => item.Id).Select(item => item.Id).Take(2).ToArrayAsync();
        var testType = new CodeValue { List = CodeLists.TestType, Code = TestTypeCode, Description = "Itest HBsAg" };
        context.CodeValues.Add(testType);
        await context.SaveChangesAsync();
        return (conditionIds, testType.Id);
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.HepLabGroups.Where(item => item.Name.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.CodeValues.Where(item => item.List == CodeLists.TestType && item.Code == TestTypeCode).ExecuteDeleteAsync();
    }
}
