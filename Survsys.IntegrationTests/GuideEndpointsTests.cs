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
/// The guide endpoints, against the real database: a guide created, its
/// variables, links and header variables replaced, deleted unless a condition
/// is exported under it; and a guide exported as JSON, imported back with
/// nothing changed, and imported as another guide.
/// </summary>
/// <remarks>
/// Every guide a test adds is named with <see cref="Marker"/>, every variable
/// is <see cref="VariableA"/> or <see cref="VariableB"/>, and they, their rule
/// sets and any condition detail naming them are deleted before and after
/// each test.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class GuideEndpointsTests(SqlServerFixture server) : IDisposable
{
    private const string Marker = "ITEST guide";
    private const string VariableA = "ITESTGA";
    private const string VariableB = "ITESTGB";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AGuideIsCreatedFilledAndDeleted()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            await Variable(client, VariableA, null);
            var guide = await Created(client, new GuideRow(0, $"{Marker} one", "v1"));
            var linked = await Created(client, new GuideRow(0, $"{Marker} two", null));

            using var duplicate = await client.PostAsJsonAsync(GuideEndpoints.Path, new GuideRow(0, $"{Marker} one", "v1"), Json);
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

            var withVariables = await Put(client, $"{GuideEndpoints.Path}/{guide.Id}/phin-variables", new[] { VariableA });
            Assert.Equal([VariableA], withVariables.PhinVars);
            using var unknown = await client.PutAsJsonAsync(
                $"{GuideEndpoints.Path}/{guide.Id}/phin-variables", new[] { "NOSUCHVAR" }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

            var withLinks = await Put(client, $"{GuideEndpoints.Path}/{guide.Id}/linked-guides", new[] { linked.Id });
            Assert.Equal([linked.Id], withLinks.LinkedGuideIds);
            using var self = await client.PutAsJsonAsync($"{GuideEndpoints.Path}/{guide.Id}/linked-guides", new[] { guide.Id }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);

            var sendingApp = withLinks.Variables.Single(item => item.Name == GuideVariableDefault.SendingApp);
            var withHeader = await Put(
                client, $"{GuideEndpoints.Path}/{guide.Id}/variables",
                new[] { sendingApp with { Value = "CTEPI^2.16.840.1.114222.4.3.9^ISO" } });
            Assert.Equal(
                "CTEPI^2.16.840.1.114222.4.3.9^ISO",
                withHeader.Variables.Single(item => item.Name == GuideVariableDefault.SendingApp).Value);

            await using (var context = server.NewContext())
            {
                var condition = await context.Conditions.FirstAsync(item => !context.ConditionDetails.Any(detail => detail.ConditionId == item.Id));
                context.ConditionDetails.Add(new ConditionDetail { ConditionId = condition.Id, GuideId = guide.Id });
                await context.SaveChangesAsync();
            }

            using var inUse = await client.DeleteAsync($"{GuideEndpoints.Path}/{guide.Id}");
            Assert.Equal(HttpStatusCode.Conflict, inUse.StatusCode);
            using var deleted = await client.DeleteAsync($"{GuideEndpoints.Path}/{linked.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Empty((await client.GetFromJsonAsync<GuideDetailResponse>($"{GuideEndpoints.Path}/{guide.Id}", Json))!.LinkedGuideIds);
        }
        finally
        {
            await Clear();
        }
    }

    [Fact]
    public async Task AnExportedGuideImportsBackUnchangedAndAsAnotherGuide()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            var ruleSet = new RuleSetConfigRow(
                0, "phinValueSet", "PHVS_YesNoUnknown_CDC", false, ["hospitalized"],
                [new RuleSetDataRow("dataSet", "Lab")], [new RuleSetValueMapRow("true", "Y", null)],
                [new RuleSetExpressionRow("Y", null, 1, [new RuleSetExpressionConditionRow("hospitalized", "=", "true", null, false)])]);
            using var createdRuleSet = await client.PostAsJsonAsync(RuleSetEndpoints.Path, ruleSet, Json);
            var configId = (await createdRuleSet.Content.ReadFromJsonAsync<RuleSetConfigRow>(Json))!.Id;
            var types = (await client.GetFromJsonAsync<List<RuleSetTypeRow>>(RuleSetEndpoints.TypesPath, Json))!;
            await Variable(client, VariableA, (types.Single(type => type.Label == "fieldValueMap").Id, configId));
            await Variable(client, VariableB, null);
            var guide = await Created(client, new GuideRow(0, $"{Marker} export", "v2.0"));
            await Put(client, $"{GuideEndpoints.Path}/{guide.Id}/phin-variables", new[] { VariableA, VariableB });

            var export = (await client.GetFromJsonAsync<GuideExport>($"{GuideEndpoints.Path}/{guide.Id}/json", Json))!;
            var exported = export.PhinVars.Single(item => item.Variable.PhinVar == VariableA);
            Assert.Equal("fieldValueMap", exported.RuleSetType);
            Assert.Equal(["hospitalized"], exported.RuleSet!.Fields);

            var reimported = await Imported(client, export);
            Assert.Equal(guide.Id, reimported.Id);
            Assert.Equal([VariableA, VariableB], reimported.PhinVars);
            var again = (await client.GetFromJsonAsync<GuideExport>($"{GuideEndpoints.Path}/{guide.Id}/json", Json))!;
            Assert.Equal(JsonSerializer.Serialize(export, Json), JsonSerializer.Serialize(again, Json));

            var copy = await Imported(client, export with { Name = $"{Marker} copy", PhinVars = [exported] });
            Assert.NotEqual(guide.Id, copy.Id);
            Assert.Equal([VariableA], copy.PhinVars);

            using var unknownType = await client.PostAsJsonAsync(
                $"{GuideEndpoints.Path}/json", export with { PhinVars = [exported with { RuleSetType = "noSuchType" }] }, Json);
            Assert.Equal(HttpStatusCode.BadRequest, unknownType.StatusCode);
        }
        finally
        {
            await Clear();
        }
    }

    private static async Task Variable(HttpClient client, string phinVar, (int Type, int Config)? ruleSet)
    {
        using var response = await client.PostAsJsonAsync(
            PhinVariableEndpoints.Path,
            new PhinVariableRow(phinVar, $"{phinVar} description", null, null, "ST", ruleSet?.Type, ruleSet?.Config, null, null, null),
            Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<GuideDetailResponse> Created(HttpClient client, GuideRow row)
    {
        using var response = await client.PostAsJsonAsync(GuideEndpoints.Path, row, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuideDetailResponse>(Json))!;
    }

    private static async Task<GuideDetailResponse> Put<T>(HttpClient client, string path, T body)
    {
        using var response = await client.PutAsJsonAsync(path, body, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuideDetailResponse>(Json))!;
    }

    private static async Task<GuideDetailResponse> Imported(HttpClient client, GuideExport export)
    {
        using var response = await client.PostAsJsonAsync($"{GuideEndpoints.Path}/json", export, Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<GuideDetailResponse>(Json))!;
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();
        var guideIds = await context.NotificationGuides.Where(item => item.Name.StartsWith(Marker))
            .Select(item => item.Id).ToListAsync();
        var configIds = await context.PhinVariables
            .Where(item => (item.PhinVar == VariableA || item.PhinVar == VariableB) && item.RuleSetConfigId != null)
            .Select(item => item.RuleSetConfigId!.Value).ToListAsync();

        await context.ConditionDetails.Where(item => guideIds.Contains(item.GuideId)).ExecuteDeleteAsync();
        await context.GuidePhinVariables.Where(item => guideIds.Contains(item.GuideId)).ExecuteDeleteAsync();
        await context.GuideLinks.Where(item => guideIds.Contains(item.GuideId) || guideIds.Contains(item.LinkedGuideId))
            .ExecuteDeleteAsync();
        await context.NotificationGuides.Where(item => guideIds.Contains(item.Id)).ExecuteDeleteAsync();
        await context.PhinVariables.Where(item => item.PhinVar == VariableA || item.PhinVar == VariableB).ExecuteDeleteAsync();
        foreach (var configId in configIds)
        {
            await RuleSetCleanup.Delete(server, configId);
        }
    }
}
