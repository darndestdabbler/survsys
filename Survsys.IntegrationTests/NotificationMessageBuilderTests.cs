using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// Building a case's notification against the real database: the case's guide
/// variables formatted by their rule sets into the message, under the
/// processing id given, and a variable with no rule set reported as a warning.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class NotificationMessageBuilderTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The record number reaches PID-3 through a field value map, the static
    /// value becomes its OBX, and MSH-11 carries the processing id.
    /// </summary>
    [Fact]
    public async Task BuildsTheCaseVariablesIntoTheMessage()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), CancellationToken.None);

            Assert.Equal(guideId, build.GuideId);
            Assert.Equal(report.RecordNumber, build.RecordNumber);
            var segments = build.Message!.Split('\r', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("T", segments.Single(segment => segment.StartsWith("MSH", StringComparison.Ordinal)).Split('|')[10]);
            Assert.StartsWith(
                report.RecordNumber,
                segments.Single(segment => segment.StartsWith("PID", StringComparison.Ordinal)).Split('|')[3],
                StringComparison.Ordinal);
            var obx = segments.Single(segment => segment.StartsWith("OBX", StringComparison.Ordinal)).Split('|');
            Assert.StartsWith(NotificationReference.StaticVariable, obx[3], StringComparison.Ordinal);
            Assert.Equal(NotificationReference.StaticValue, obx[5]);
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>A guide variable no rule set formats is a warning, and the message is still built.</summary>
    [Fact]
    public async Task AVariableWithNoRuleSetIsAWarning()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            await NotificationReference.Seed(context, report.ConditionId);

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "P", DateTimeOffset.UtcNow, CancellationToken.None);

            Assert.NotNull(build.Message);
            var warning = Assert.Single(build.Messages);
            Assert.Equal(NotificationReference.UnformattedVariable, warning.PhinVar);
            Assert.Equal(RuleSetMessageKind.Warning, warning.Severity);
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>
    /// Opens a case about the seeded person for the seeded condition, and
    /// answers with it.
    /// </summary>
    internal static async Task<PersonConditionResponse> Open(SurvsysApi api)
    {
        var client = api.CreateClient();
        var persons = (await client.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons?pageSize=200", Json))!;
        var conditions = (await client.GetFromJsonAsync<PagedResult<ConditionResponse>>("/api/conditions?pageSize=200", Json))!;
        var person = persons.Items.Single(item => $"{item.LastName}, {item.FirstName}" == SqlServerFixture.SeededReportPerson);
        var condition = conditions.Items.Single(item => item.Name == SqlServerFixture.SeededReportCondition);

        using var response = await client.PostAsJsonAsync(
            "/api/person-conditions", new CreatePersonConditionRequest(person.Id, condition.Id), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<PersonConditionResponse>(Json))!;
    }

    /// <summary>Takes a case opened by <see cref="Open"/> out again, with its queue entry and log rows.</summary>
    internal static Task Close(SurvsysDbContext context, int personConditionId) =>
        context.PersonConditions.Where(item => item.Id == personConditionId).ExecuteDeleteAsync();
}

/// <summary>
/// The notification reference rows a case's message is built from: one guide
/// for the case's condition with three variables — the record number into
/// PID-3 by a field value map, a static value into an OBX, and one with no
/// rule set.
/// </summary>
internal static class NotificationReference
{
    public const string GuideName = "Integration test guide";
    public const string StaticVariable = "TST100";
    public const string StaticValue = "10130";
    public const string UnformattedVariable = "TST999";
    private const string RecordNumberVariable = "DEM197";

    private static readonly string[] Variables = [RecordNumberVariable, StaticVariable, UnformattedVariable];

    /// <summary>Seeds the rows for <paramref name="conditionId"/> and answers with the guide's id.</summary>
    public static async Task<int> Seed(SurvsysDbContext context, int conditionId)
    {
        var ruleSets = await context.RuleSets.ToDictionaryAsync(ruleSet => ruleSet.Label, ruleSet => ruleSet.Id);
        var guide = new NotificationGuide { Name = GuideName };
        var recordNumberConfig = new RuleSetConfig();
        var staticConfig = new RuleSetConfig { FormatType = "string" };
        context.AddRange(guide, recordNumberConfig, staticConfig);
        await context.SaveChangesAsync();

        context.ConditionDetails.Add(new ConditionDetail { ConditionId = conditionId, GuideId = guide.Id });
        context.PhinVariables.AddRange(
            new Data.PhinVariable
            {
                PhinVar = RecordNumberVariable,
                Description = "Patient Identifier",
                Hl7DataType = "ST",
                RuleSetId = ruleSets["fieldValueMap"],
                RuleSetConfigId = recordNumberConfig.Id,
            },
            new Data.PhinVariable
            {
                PhinVar = StaticVariable,
                Description = "Condition Code",
                Hl7DataType = "ST",
                RuleSetId = ruleSets["staticValue"],
                RuleSetConfigId = staticConfig.Id,
            },
            new Data.PhinVariable { PhinVar = UnformattedVariable, Description = "Unformatted" });
        context.GuidePhinVariables.AddRange(
            Variables.Select(phinVar => new GuidePhinVariable { GuideId = guide.Id, PhinVar = phinVar }));
        context.RuleSetFields.Add(new RuleSetField { RuleSetConfigId = recordNumberConfig.Id, Field = "record_number" });
        context.RuleSetData.Add(new RuleSetData
        {
            RuleSetConfigId = staticConfig.Id, DataName = "staticValue", DataValue = StaticValue,
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return guide.Id;
    }

    /// <summary>Takes the seeded rows out again.</summary>
    public static async Task Clear(SurvsysDbContext context)
    {
        var guideIds = await context.NotificationGuides
            .Where(guide => guide.Name == GuideName).Select(guide => guide.Id).ToListAsync();
        var configIds = await context.PhinVariables
            .Where(variable => Variables.Contains(variable.PhinVar) && variable.RuleSetConfigId != null)
            .Select(variable => variable.RuleSetConfigId!.Value).ToListAsync();

        await context.GuidePhinVariables.Where(item => guideIds.Contains(item.GuideId)).ExecuteDeleteAsync();
        await context.ConditionDetails.Where(item => guideIds.Contains(item.GuideId)).ExecuteDeleteAsync();
        await context.PhinVariables.Where(item => Variables.Contains(item.PhinVar)).ExecuteDeleteAsync();
        await context.RuleSetFields.Where(item => configIds.Contains(item.RuleSetConfigId)).ExecuteDeleteAsync();
        await context.RuleSetData.Where(item => configIds.Contains(item.RuleSetConfigId)).ExecuteDeleteAsync();
        await context.RuleSetConfigs.Where(item => configIds.Contains(item.Id)).ExecuteDeleteAsync();
        await context.NotificationGuides.Where(item => guideIds.Contains(item.Id)).ExecuteDeleteAsync();
    }
}
