using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The rule set endpoints, against the real database: the seven types listed,
/// and a configuration created, read back whole and replaced whole.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class RuleSetEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task TheSevenTypesAreListed()
    {
        var types = (await api.CreateClient().GetFromJsonAsync<List<RuleSetTypeRow>>(RuleSetEndpoints.TypesPath, Json))!;

        Assert.Equal(7, types.Count);
        Assert.Contains(types, type => type.Label == "conditionMap");
    }

    [Fact]
    public async Task AConfigurationIsCreatedReadAndReplacedWhole()
    {
        var client = api.CreateClient();
        var row = new RuleSetConfigRow(
            0,
            "phinValueSet",
            "PHVS_YesNoUnknown_CDC",
            false,
            ["hospitalized", "admit_date"],
            [new RuleSetDataRow("dataSet", "Lab")],
            [new RuleSetValueMapRow("true", "Y", null)],
            [
                new RuleSetExpressionRow("Y", null, 1,
                [
                    new RuleSetExpressionConditionRow("hospitalized", "=", "true", null, false),
                    new RuleSetExpressionConditionRow("admit_date", "<>", "", "and", false),
                ]),
            ]);
        int? id = null;

        try
        {
            using var created = await client.PostAsJsonAsync(RuleSetEndpoints.Path, row, Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            id = (await created.Content.ReadFromJsonAsync<RuleSetConfigRow>(Json))!.Id;

            var read = (await client.GetFromJsonAsync<RuleSetConfigRow>($"{RuleSetEndpoints.Path}/{id}", Json))!;
            Assert.Equal(["hospitalized", "admit_date"], read.Fields);
            Assert.Equal("AND", Assert.Single(read.Expressions).Conditions[1].AndOr);

            using var replaced = await client.PutAsJsonAsync(
                $"{RuleSetEndpoints.Path}/{id}",
                row with { FormatType = "string", Fields = ["record_number"], ValueMaps = [], Expressions = [] },
                Json);
            Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);

            var again = (await client.GetFromJsonAsync<RuleSetConfigRow>($"{RuleSetEndpoints.Path}/{id}", Json))!;
            Assert.Equal("string", again.FormatType);
            Assert.Equal(["record_number"], again.Fields);
            Assert.Empty(again.ValueMaps);
            Assert.Empty(again.Expressions);
            Assert.Equal("Lab", Assert.Single(again.Data).Value);

            using var missing = await client.GetAsync($"{RuleSetEndpoints.Path}/0");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            if (id is { } configId)
            {
                await RuleSetCleanup.Delete(server, configId);
            }
        }
    }
}

/// <summary>Takes a rule set configuration a test created out again, with everything under it.</summary>
internal static class RuleSetCleanup
{
    public static async Task Delete(SqlServerFixture server, int configId)
    {
        await using var context = server.NewContext();
        var expressionIds = context.RuleSetExpressions
            .Where(item => item.RuleSetConfigId == configId).Select(item => item.Id);
        await context.RuleSetExpressionConditions.Where(item => expressionIds.Contains(item.RuleSetExpressionId)).ExecuteDeleteAsync();
        await context.RuleSetExpressions.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync();
        await context.RuleSetFields.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync();
        await context.RuleSetData.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync();
        await context.RuleSetValueMaps.Where(item => item.RuleSetConfigId == configId).ExecuteDeleteAsync();
        await context.RuleSetConfigs.Where(item => item.Id == configId).ExecuteDeleteAsync();
    }
}
