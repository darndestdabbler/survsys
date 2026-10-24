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
/// The PHIN variable endpoints, against the real database: a variable created,
/// saved over, listed and deleted, one naming a rule set type not on file or
/// already on file refused, and one a guide carries kept.
/// </summary>
/// <remarks>
/// Every variable a test adds is <see cref="Variable"/>, and it and the guide
/// a test adds are deleted before and after each test.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class PhinVariableEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Variable = "ITEST01";
    private const string GuideName = "PHIN variable endpoints test guide";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AVariableIsCreatedSavedOverListedAndDeleted()
    {
        await Clear();
        var client = api.CreateClient();
        var row = new PhinVariableRow(Variable, "Integration variable", null, null, "ST", null, null, null, null, null);

        try
        {
            using var created = await client.PostAsJsonAsync(PhinVariableEndpoints.Path, row, Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);

            using var again = await client.PostAsJsonAsync(PhinVariableEndpoints.Path, row, Json);
            Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

            using var unknownType = await client.PutAsJsonAsync(
                $"{PhinVariableEndpoints.Path}/{Variable}", row with { RuleSetId = 999 }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);

            using var saved = await client.PutAsJsonAsync(
                $"{PhinVariableEndpoints.Path}/{Variable}", row with { Description = "Renamed", CodeSystem = "LN" }, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var listed = (await client.GetFromJsonAsync<List<PhinVariableRow>>(PhinVariableEndpoints.Path, Json))!;
            var ours = Assert.Single(listed, item => item.PhinVar == Variable);
            Assert.Equal("Renamed", ours.Description);
            Assert.Equal("LN", ours.CodeSystem);

            using var deleted = await client.DeleteAsync($"{PhinVariableEndpoints.Path}/{Variable}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var gone = await client.DeleteAsync($"{PhinVariableEndpoints.Path}/{Variable}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AVariableAGuideCarriesIsNotDeleted()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            using var created = await client.PostAsJsonAsync(
                PhinVariableEndpoints.Path,
                new PhinVariableRow(Variable, "Integration variable", null, null, null, null, null, null, null, null),
                Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            await using (var context = server.NewContext())
            {
                var guide = new NotificationGuide { Name = GuideName };
                context.NotificationGuides.Add(guide);
                await context.SaveChangesAsync();
                context.GuidePhinVariables.Add(new GuidePhinVariable { GuideId = guide.Id, PhinVar = Variable });
                await context.SaveChangesAsync();
            }

            using var deleted = await client.DeleteAsync($"{PhinVariableEndpoints.Path}/{Variable}");

            Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.GuidePhinVariables.Where(item => item.PhinVar == Variable).ExecuteDeleteAsync();
        await context.NotificationGuides.Where(item => item.Name == GuideName).ExecuteDeleteAsync();
        await context.PhinVariables.Where(item => item.PhinVar == Variable).ExecuteDeleteAsync();
    }
}
