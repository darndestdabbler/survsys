using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The vocabulary and rules endpoints, against the real database: each table
/// created, saved over, listed and deleted, a child naming a master not on
/// file refused, a master a child still names kept, a CSV export imported back
/// as updates and a new row added by import, and a condition's rules replaced
/// whole.
/// </summary>
/// <remarks>
/// Driven through the endpoints as the administrator. Every code a test adds
/// starts with <see cref="Marker"/>, and the rows carrying it, the sending
/// facility the test adds and the rules on its condition are deleted before and
/// after each test.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class VocabularyEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Marker = "ITEST-";
    private const string FacilityName = "Vocabulary endpoints test lab";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task EachTableIsCreatedSavedOverListedAndDeleted()
    {
        var (labId, conditionId) = await Setup();
        var client = api.CreateClient();

        try
        {
            var organism = await Created<VocabMasterOrganismRow>(client, "organisms",
                new VocabMasterOrganismRow(0, $"{Marker}27268008", null, "Salmonella", conditionId));
            var master = await Created<VocabMasterLoincRow>(client, "master-loincs",
                new VocabMasterLoincRow(0, $"{Marker}625-4", "Stool culture", null, null, OrganismFromResult: true));
            var child = await Created<VocabChildLoincRow>(client, "child-loincs",
                new VocabChildLoincRow(0, labId, $"{Marker}SCUL", master.Loinc, null, false));
            Assert.Equal(master.Loinc, child.MasterLoinc);
            var value = await Created<VocabMasterValueRow>(client, "master-values",
                new VocabMasterValueRow(0, "test_result", $"{Marker}Detected", "Positive"));
            var code = await Created<VocabChildValueRow>(client, "child-values",
                new VocabChildValueRow(0, labId, "test_result", $"{Marker}DET", value.Concept));
            var organismCode = await Created<VocabChildValueRow>(client, "child-values",
                new VocabChildValueRow(0, labId, "organism", $"{Marker}SAL", organism.Snomed));
            Assert.Equal(organism.Snomed, organismCode.Master);

            // Saved over: the trimmed values read back.
            using var saved = await client.PutAsJsonAsync(
                $"/api/vocabulary/master-values/{value.Id}", value with { AppValue = "  Reactive " }, Json);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.Equal("Reactive", (await saved.Content.ReadFromJsonAsync<VocabMasterValueRow>(Json))!.AppValue);

            Assert.Contains(await List<VocabChildValueRow>(client, "child-values"), row => row.Id == code.Id);

            // A child naming a master not on file, and a second entry with a
            // key already taken, are refused.
            using var dangling = await client.PostAsJsonAsync("/api/vocabulary/child-loincs",
                new VocabChildLoincRow(0, labId, $"{Marker}X", $"{Marker}0000-0", null, false), Json);
            Assert.Equal(HttpStatusCode.BadRequest, dangling.StatusCode);
            using var taken = await client.PostAsJsonAsync("/api/vocabulary/master-loincs", master with { Id = 0 }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, taken.StatusCode);

            // A master a child still names is kept.
            using var kept = await client.DeleteAsync($"/api/vocabulary/master-loincs/{master.Id}");
            Assert.Equal(HttpStatusCode.Conflict, kept.StatusCode);
            using var keptOrganism = await client.DeleteAsync($"/api/vocabulary/organisms/{organism.Id}");
            Assert.Equal(HttpStatusCode.Conflict, keptOrganism.StatusCode);

            // Children first, then their masters.
            foreach (var path in new[]
            {
                $"child-loincs/{child.Id}", $"child-values/{code.Id}", $"child-values/{organismCode.Id}",
                $"master-loincs/{master.Id}", $"master-values/{value.Id}", $"organisms/{organism.Id}",
            })
            {
                using var deleted = await client.DeleteAsync($"/api/vocabulary/{path}");
                Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            }

            using var gone = await client.DeleteAsync($"/api/vocabulary/organisms/{organism.Id}");
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task ACsvExportImportsBackAsUpdatesAndAddsANewRow()
    {
        var (labId, _) = await Setup();
        var client = api.CreateClient();

        try
        {
            await Created<VocabMasterValueRow>(client, "master-values",
                new VocabMasterValueRow(0, "specimen", $"{Marker}Serum", "Serum"));
            await Created<VocabChildValueRow>(client, "child-values",
                new VocabChildValueRow(0, labId, "specimen", $"{Marker}119364003", $"{Marker}Serum"));

            var exported = await client.GetStringAsync("/api/vocabulary/child-values/csv");
            var ours = string.Join("\r\n", exported
                .Split("\r\n")
                .Where((line, at) => at == 0 || line.Contains(Marker, StringComparison.Ordinal)));

            var import = ours
                + $"\r\n{labId},specimen,{Marker}BLD,{Marker}Serum"
                + $"\r\n{labId},specimen,{Marker}URN,{Marker}Urine"
                + $"\r\n{labId},specimen\r\n";
            using var posted = await client.PostAsync(
                "/api/vocabulary/child-values/csv", new StringContent(import, Encoding.UTF8, "text/csv"));
            Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
            var result = (await posted.Content.ReadFromJsonAsync<VocabImportResponse>(Json))!;

            Assert.Equal(1, result.Added);
            Assert.Equal(1, result.Updated);
            Assert.Equal([4, 5], result.Refused.Select(problem => problem.Line));
            Assert.Contains("Urine", result.Refused[0].Reason);

            var children = (await List<VocabChildValueRow>(client, "child-values"))
                .Where(row => row.ChildCode.StartsWith(Marker, StringComparison.Ordinal))
                .ToList();
            Assert.Equal([$"{Marker}119364003", $"{Marker}BLD"], children.Select(row => row.ChildCode));
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AConditionsRulesAreReplacedWhole()
    {
        var (_, conditionId) = await Setup();
        var client = api.CreateClient();

        try
        {
            var first = new VocabRuleRow[]
            {
                new(0, "message_filter", 1, false, false, false, []),
                new(0, "case_management", 1, true, true, false,
                    [new VocabRuleConditionRow("TestResult", "=", "Positive", null)]),
            };
            using var put = await client.PutAsJsonAsync($"/api/conditions/{conditionId}/rules", first, Json);
            Assert.Equal(HttpStatusCode.OK, put.StatusCode);

            var second = new VocabRuleRow[]
            {
                new(0, "message_filter", 1, false, false, false,
                    [new VocabRuleConditionRow("DaysSinceCollection", "<", "183", null)]),
                new(0, "case_management", 2, false, true, true,
                [
                    new VocabRuleConditionRow("TestResult", "=", "Negative", null),
                    new VocabRuleConditionRow("TestResult", "=", "Equivocal", "or"),
                ]),
            };
            using var replaced = await client.PutAsJsonAsync($"/api/conditions/{conditionId}/rules", second, Json);
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);

            var read = (await client.GetFromJsonAsync<List<VocabRuleRow>>(
                $"/api/conditions/{conditionId}/rules", Json))!;
            Assert.Equal(2, read.Count);
            Assert.Equal("183", Assert.Single(read[0].Conditions).Value);
            Assert.Equal("case_management", read[1].Kind);
            Assert.True(read[1].IsSurveillance);
            Assert.Equal(["Negative", "Equivocal"], read[1].Conditions.Select(condition => condition.Value));
            Assert.Equal("OR", read[1].Conditions[1].AndOr);

            using var bad = await client.PutAsJsonAsync($"/api/conditions/{conditionId}/rules",
                new[] { new VocabRuleRow(0, "graylist", 1, false, false, false, []) }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
            using var noCondition = await client.GetAsync("/api/conditions/999999/rules");
            Assert.Equal(HttpStatusCode.NotFound, noCondition.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    private static async Task<T> Created<T>(HttpClient client, string table, T row)
    {
        using var response = await client.PostAsJsonAsync($"/api/vocabulary/{table}", row, Json);
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"{table}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private static async Task<List<T>> List<T>(HttpClient client, string table) =>
        (await client.GetFromJsonAsync<List<T>>($"/api/vocabulary/{table}", Json))!;

    /// <summary>
    /// A clean start: the test's own rows cleared, a sending facility of its
    /// own, and the fixture condition its rules go on.
    /// </summary>
    private async Task<(int LabId, int ConditionId)> Setup()
    {
        await Clear();
        await using var context = server.NewContext();

        var facility = new Facility { Name = FacilityName, ShortName = $"{Marker}LAB", AgencyId = 1 };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var conditionId = await context.Conditions.OrderByDescending(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        return (facility.Id, conditionId);
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        var conditionId = await context.Conditions.OrderByDescending(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();
        var ruleIds = await context.VocabRules.Where(rule => rule.ConditionId == conditionId)
            .Select(rule => rule.Id)
            .ToListAsync();
        await context.VocabRuleConditions.Where(condition => ruleIds.Contains(condition.VocabRuleId)).ExecuteDeleteAsync();
        await context.VocabRules.Where(rule => ruleIds.Contains(rule.Id)).ExecuteDeleteAsync();

        await context.VocabChildValues.Where(child => child.ChildCode.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.VocabChildLoincs.Where(child => child.ChildLoinc.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.VocabMasterValues.Where(master => master.Concept.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.VocabMasterLoincs.Where(master => master.Loinc.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.VocabMasterOrganisms.Where(organism => organism.Snomed.StartsWith(Marker)).ExecuteDeleteAsync();
        await context.Facilities.Where(facility => facility.Name == FacilityName).ExecuteDeleteAsync();
    }
}
