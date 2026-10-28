using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The eCR condition mapping endpoints, against the real database: a code is
/// added, mapped, listed with its condition's name, refused where it is
/// already on file or names no condition, and deleted; and a CSV file, as an
/// RCTC release's condition list comes in, is previewed, imported and
/// exported.
/// </summary>
/// <remarks>
/// Every code these tests write is in a code system of their own, deleted in
/// a finally, so the suite's shared database is left as it was.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class EcrConditionMappingEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string TestCodeSystem = "2.16.840.1.113883.9.9.9.ITEST";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AMappingIsAddedMappedListedAndDeleted()
    {
        var client = api.CreateClient();
        await ClearTestCodes();

        try
        {
            var malaria = await ConditionId("Malaria");

            using var created = await client.PostAsJsonAsync(
                "/api/ecr-condition-mappings", new SaveEcrConditionMappingRequest(TestCodeSystem, "1001", "Test code"));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var mapping = (await created.Content.ReadFromJsonAsync<EcrConditionMappingResponse>(Json))!;
            Assert.Null(mapping.ConditionId);

            using var taken = await client.PostAsJsonAsync(
                "/api/ecr-condition-mappings", new SaveEcrConditionMappingRequest(TestCodeSystem, " 1001 "));
            Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

            using var noCondition = await client.PutAsJsonAsync(
                $"/api/ecr-condition-mappings/{mapping.Id}",
                new SaveEcrConditionMappingRequest(TestCodeSystem, "1001", "Test code", int.MaxValue));
            Assert.Equal(HttpStatusCode.BadRequest, noCondition.StatusCode);

            using var saved = await client.PutAsJsonAsync(
                $"/api/ecr-condition-mappings/{mapping.Id}",
                new SaveEcrConditionMappingRequest(TestCodeSystem, "1001", "Test code", malaria));
            saved.EnsureSuccessStatusCode();

            var listed = Assert.Single(
                (await client.GetFromJsonAsync<List<EcrConditionMappingResponse>>("/api/ecr-condition-mappings", Json))!,
                row => row.CodeSystem == TestCodeSystem);
            Assert.Equal(new EcrConditionMappingResponse(mapping.Id, TestCodeSystem, "1001", "Test code", malaria, "Malaria"), listed);

            using var deleted = await client.DeleteAsync($"/api/ecr-condition-mappings/{mapping.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var again = await client.DeleteAsync($"/api/ecr-condition-mappings/{mapping.Id}");
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        }
        finally
        {
            await ClearTestCodes();
        }
    }

    [Fact]
    public async Task AnRctcConditionListIsPreviewedThenImportedAndExported()
    {
        var client = api.CreateClient();
        await ClearTestCodes();

        try
        {
            var csv = "code_system,code,display_name,condition\r\n"
                + $"{TestCodeSystem},2001,Malaria (disorder),Malaria\r\n"
                + $"{TestCodeSystem},2002,Unmapped (disorder),\r\n"
                + $"{TestCodeSystem},2003,Unknown (disorder),No such condition\r\n"
                + $"{TestCodeSystem},,Codeless,\r\n";

            var preview = await Import(client, csv, confirm: false);

            Assert.Equal(
                [new ReferenceImportRow(2, $"{TestCodeSystem}|2001"), new ReferenceImportRow(3, $"{TestCodeSystem}|2002")],
                preview.Added);
            Assert.Collection(
                preview.Refused,
                refused =>
                {
                    Assert.Equal(4, refused.Line);
                    Assert.Contains("'No such condition' is not a condition on file", refused.Reason);
                },
                refused =>
                {
                    Assert.Equal(5, refused.Line);
                    Assert.Contains("code is required", refused.Reason);
                });
            await using (var context = server.NewContext())
            {
                Assert.False(await context.EcrConditionMappings.AnyAsync(mapping => mapping.CodeSystem == TestCodeSystem));
            }

            await Import(client, csv, confirm: true);

            var exported = await client.GetStringAsync("/api/ecr-condition-mappings/csv");
            Assert.StartsWith("code_system,code,display_name,condition\r\n", exported);
            Assert.Contains($"{TestCodeSystem},2001,Malaria (disorder),Malaria\r\n", exported);
            Assert.Contains($"{TestCodeSystem},2002,Unmapped (disorder),\r\n", exported);

            var unchanged = await Import(client, exported, confirm: false);
            Assert.Empty(unchanged.Added);
            Assert.Empty(unchanged.Changed);
            Assert.Empty(unchanged.Refused);
        }
        finally
        {
            await ClearTestCodes();
        }
    }

    private static async Task<ReferenceImportResponse> Import(HttpClient client, string csv, bool confirm)
    {
        using var posted = await client.PostAsync(
            $"/api/ecr-condition-mappings/csv{(confirm ? "?confirm=true" : "")}",
            new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        return (await posted.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private async Task<int> ConditionId(string name)
    {
        await using var context = server.NewContext();
        return await context.Conditions.Where(condition => condition.Name == name).Select(condition => condition.Id).SingleAsync();
    }

    private async Task ClearTestCodes()
    {
        await using var context = server.NewContext();
        await context.EcrConditionMappings.Where(mapping => mapping.CodeSystem == TestCodeSystem).ExecuteDeleteAsync();
    }
}
