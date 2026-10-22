using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The PHIN value set and unit of measure endpoints, against the real
/// database: each created, saved over, listed and deleted, a second concept
/// under a key already on file refused, and a CSV import previewed without
/// writing and then confirmed.
/// </summary>
/// <remarks>
/// Driven through the endpoints as the administrator. Every value set a test
/// adds is <see cref="ValueSet"/>, and its rows are deleted before and after
/// each test.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ValueSetEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string ValueSet = "ITEST_PHVS_YesNo";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string PhinPath = $"{ValueSetEndpoints.Root}/{ValueSetEndpoints.PhinValueSets}";

    private static readonly string UnitPath = $"{ValueSetEndpoints.Root}/{ValueSetEndpoints.UnitsOfMeasure}";

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task EachTableIsCreatedSavedOverListedAndDeleted()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            using var created = await client.PostAsJsonAsync(
                PhinPath, new PhinValueSetRow(0, ValueSet, "Y", "Yes", "Y", "2.16.840.1.113883.12.136", null), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var concept = (await created.Content.ReadFromJsonAsync<PhinValueSetRow>(Json))!;

            using var saved = await client.PutAsJsonAsync(
                $"{PhinPath}/{concept.Id}", concept with { CdcName = "Yes (CDC)" }, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            var listed = (await client.GetFromJsonAsync<List<PhinValueSetRow>>(PhinPath, Json))!;
            Assert.Equal("Yes (CDC)", Assert.Single(listed, row => row.ValueSetName == ValueSet).CdcName);

            using var duplicate = await client.PostAsJsonAsync(PhinPath, concept with { Id = 0 }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

            using var unit = await client.PostAsJsonAsync(
                UnitPath, new PhinUnitOfMeasureRow(0, ValueSet, "mg", "milligram", "mg", "2.16.840.1.113883.6.8"), Json);
            Assert.Equal(HttpStatusCode.Created, unit.StatusCode);
            var unitRow = (await unit.Content.ReadFromJsonAsync<PhinUnitOfMeasureRow>(Json))!;

            using var deleted = await client.DeleteAsync($"{PhinPath}/{concept.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var unitDeleted = await client.DeleteAsync($"{UnitPath}/{unitRow.Id}");
            Assert.Equal(HttpStatusCode.NoContent, unitDeleted.StatusCode);
            Assert.DoesNotContain(
                (await client.GetFromJsonAsync<List<PhinValueSetRow>>(PhinPath, Json))!, row => row.ValueSetName == ValueSet);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ACsvImportIsPreviewedThenSavedOnceConfirmed()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            using var created = await client.PostAsJsonAsync(
                PhinPath, new PhinValueSetRow(0, ValueSet, "Y", "Yes", "Y", null, null), Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var csv =
                "value_set_name,concept_code,preferred_concept_name,hl7_code,code_system_oid,cdc_name\n"
                + $"{ValueSet},Y,Yes,Y,,Yes (CDC)\n"
                + $"{ValueSet},N,No,N,,\n"
                + $"{ValueSet},,No code,,,\n";

            var preview = await Imported(client, csv, confirm: false);

            Assert.False(preview.Applied);
            Assert.Equal([$"{ValueSet} N"], preview.Added.Select(row => row.Key));
            Assert.Equal([$"{ValueSet} Y"], preview.Changed.Select(row => row.Key));
            Assert.Contains("conceptCode", Assert.Single(preview.Refused).Reason);
            Assert.Null(Ours(await client.GetFromJsonAsync<List<PhinValueSetRow>>(PhinPath, Json))["Y"].CdcName);

            var confirmed = await Imported(client, csv, confirm: true);

            Assert.True(confirmed.Applied);
            var ours = Ours(await client.GetFromJsonAsync<List<PhinValueSetRow>>(PhinPath, Json));
            Assert.Equal("Yes (CDC)", ours["Y"].CdcName);
            Assert.Equal("No", ours["N"].PreferredConceptName);

            var exported = await client.GetStringAsync($"{PhinPath}/csv");
            Assert.Contains($"{ValueSet},N,No,N,,", exported, StringComparison.Ordinal);
        }
        finally
        {
            await Clear();
        }
    }

    private static Dictionary<string, PhinValueSetRow> Ours(List<PhinValueSetRow>? rows) =>
        rows!.Where(row => row.ValueSetName == ValueSet).ToDictionary(row => row.ConceptCode);

    private static async Task<ReferenceImportResponse> Imported(HttpClient client, string csv, bool confirm)
    {
        using var response = await client.PostAsync(
            $"{PhinPath}/csv{(confirm ? "?confirm=true" : "")}", new StringContent(csv, Encoding.UTF8, "text/csv"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ReferenceImportResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        await context.PhinValueSets.Where(item => item.ValueSetName == ValueSet).ExecuteDeleteAsync();
        await context.PhinUnitsOfMeasure.Where(item => item.ValueSetName == ValueSet).ExecuteDeleteAsync();
    }
}
