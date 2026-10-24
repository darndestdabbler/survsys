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

    /// <summary>
    /// A retraction is built with its notification status NOT118 set to X,
    /// which the generator writes to OBR-25.
    /// </summary>
    [Fact]
    public async Task ARetractionSetsTheNotificationStatusToX()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            await NotificationReference.Seed(context, report.ConditionId);

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), CancellationToken.None,
                retract: true);

            var obr = build.Message!.Split('\r', StringSplitOptions.RemoveEmptyEntries)
                .Single(segment => segment.StartsWith("OBR", StringComparison.Ordinal)).Split('|');
            Assert.Equal("X", obr[25]);
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
    /// MSH-3 to MSH-6 come from the guide's variables over their defaults: the
    /// guide's own sending facility, and the seeded default for the rest.
    /// </summary>
    [Fact]
    public async Task TheHeaderIsTheGuidesVariablesOverTheirDefaults()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);
            var sendingFacility = await context.GuideVariableDefaults
                .SingleAsync(item => item.Name == GuideVariableDefault.SendingFacility);
            context.GuideVariables.Add(new GuideVariableValue
            {
                GuideId = guideId, GuideVariableDefaultId = sendingFacility.Id, Value = "CTLAB^2.16.840.1.114222.4.1.9999^ISO",
            });
            await context.SaveChangesAsync();

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", DateTimeOffset.UtcNow, CancellationToken.None);

            var msh = Segments(build).Single(segment => segment.StartsWith("MSH", StringComparison.Ordinal)).Split('|');
            Assert.Equal("Survsys^2.16.840.1.114222.4.3.3.2.1^ISO", msh[2]);
            Assert.Equal("CTLAB^2.16.840.1.114222.4.1.9999^ISO", msh[3]);
            Assert.Equal("2.5.1", msh[11]);
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>A guide whose version is the arbovirus profile is generated with two OBRs, under NND_ORU_v2.0.</summary>
    [Fact]
    public async Task TheGuidesVersionIsTheProfileItIsGeneratedFor()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);
            await context.NotificationGuides.Where(guide => guide.Id == guideId)
                .ExecuteUpdateAsync(set => set.SetProperty(guide => guide.Version, OruR01Generator.ArboGuideProfile));

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", DateTimeOffset.UtcNow, CancellationToken.None);

            var segments = Segments(build);
            Assert.Equal(2, segments.Count(segment => segment.StartsWith("OBR", StringComparison.Ordinal)));
            Assert.Equal(
                OruR01Generator.ArboProfileId,
                segments.Single(segment => segment.StartsWith("MSH", StringComparison.Ordinal)).Split('|')[20]);
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>
    /// A rule set's value set is read from the PHIN value sets, and the
    /// concept's CDC name is written over its preferred name.
    /// </summary>
    [Fact]
    public async Task ARuleSetsValueSetIsReadFromThePhinValueSets()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);
            await NotificationReference.AddVariable(
                context, guideId, NotificationReference.CodedVariable, "staticValue",
                new RuleSetConfig { FormatType = "phinValueSet", ValueSetName = NotificationReference.ValueSetName },
                data: ("staticValue", "Y"));
            context.PhinValueSets.Add(new PhinValueSetConcept
            {
                ValueSetName = NotificationReference.ValueSetName,
                ConceptCode = "Y",
                PreferredConceptName = "Yes",
                CdcName = "Yes (CDC)",
                Hl7Code = "Y",
                CodeSystemOid = "2.16.840.1.113883.12.136",
            });
            await context.SaveChangesAsync();

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", DateTimeOffset.UtcNow, CancellationToken.None);

            var obx = Observation(build, NotificationReference.CodedVariable);
            Assert.Equal("Y^Yes (CDC)^2.16.840.1.113883.12.136", obx[5]);
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>A rule set naming a value set that is not on file is a warning.</summary>
    [Fact]
    public async Task AValueSetNotOnFileIsAWarning()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);
            await NotificationReference.AddVariable(
                context, guideId, NotificationReference.CodedVariable, "fieldValueMap",
                new RuleSetConfig { FormatType = "phinValueSet", ValueSetName = NotificationReference.ValueSetName },
                field: "record_number");

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", DateTimeOffset.UtcNow, CancellationToken.None);

            Assert.Contains(build.Messages, message => message.PhinVar == NotificationReference.CodedVariable
                && message.Severity == RuleSetMessageKind.Warning
                && message.Message.Contains(NotificationReference.ValueSetName, StringComparison.Ordinal));
        }
        finally
        {
            await NotificationReference.Clear(context);
            await Close(context, report.Id);
        }
    }

    /// <summary>
    /// The Lab dataset carries only the labs the condition's test-type
    /// overrides allow, and the HepLab dataset groups them under the
    /// condition's hepatitis lab group.
    /// </summary>
    [Fact]
    public async Task TheLabsAreNarrowedByTheOverridesAndGroupedForHepatitis()
    {
        await using var context = server.NewContext();
        var report = await Open(api);

        try
        {
            var guideId = await NotificationReference.Seed(context, report.ConditionId);
            await NotificationReference.AddVariable(
                context, guideId, NotificationReference.LabVariable, "fieldValueMap",
                new RuleSetConfig { FormatType = "string" }, field: "test_result", data: ("dataSet", "Lab"));
            await NotificationReference.AddVariable(
                context, guideId, NotificationReference.HepLabVariable, "fieldValueMap",
                new RuleSetConfig { FormatType = "string" },
                field: HepatitisLabGrouping.TestGroupField, data: ("dataSet", HepatitisLabGrouping.DataSet));
            await AddLab(context, report, NotificationReference.KeptTestType, "Reactive");
            await AddLab(context, report, NotificationReference.ExcludedTestType, "Positive");
            context.ConditionLabOverrides.AddRange(
                new ConditionLabOverride
                {
                    ConditionId = report.ConditionId, Kind = ConditionLabOverride.TestTypeKind,
                    ItemId = NotificationReference.KeptTestType, Include = true,
                },
                new ConditionLabOverride
                {
                    ConditionId = report.ConditionId, Kind = ConditionLabOverride.TestTypeKind,
                    ItemId = NotificationReference.ExcludedTestType, Include = false,
                });
            var group = new HepLabGroup { Name = NotificationReference.HepLabGroupName };
            context.HepLabGroups.Add(group);
            await context.SaveChangesAsync();
            context.HepLabGroupConditions.Add(new HepLabGroupCondition { HepLabGroupId = group.Id, ConditionId = report.ConditionId });
            context.HepLabGroupTestTypes.AddRange(
                new HepLabGroupTestType
                {
                    HepLabGroupId = group.Id, TestTypeId = NotificationReference.KeptTestType, TestGroupName = "HBsAg",
                },
                new HepLabGroupTestType
                {
                    HepLabGroupId = group.Id, TestTypeId = NotificationReference.ExcludedTestType, TestGroupName = "anti-HCV",
                });
            await context.SaveChangesAsync();

            var build = await new NotificationMessageBuilder(context).Build(
                report.Id, "T", DateTimeOffset.UtcNow, CancellationToken.None);

            Assert.Equal("Reactive", Assert.Single(Observations(build, NotificationReference.LabVariable))[5]);
            Assert.Equal("HBsAg", Assert.Single(Observations(build, NotificationReference.HepLabVariable))[5]);
        }
        finally
        {
            await context.Labs.Where(lab => lab.PersonConditionId == report.Id).ExecuteDeleteAsync();
            await NotificationReference.Clear(context, report.ConditionId);
            await Close(context, report.Id);
        }
    }

    private static async Task AddLab(SurvsysDbContext context, PersonConditionResponse report, int testType, string result)
    {
        var lab = new Lab { PersonId = report.PersonId, PersonConditionId = report.Id };
        context.Labs.Add(lab);
        await context.SaveChangesAsync();
        var test = new LabTest { LabId = lab.Id, TestTypeId = testType };
        context.LabTests.Add(test);
        await context.SaveChangesAsync();
        context.LabTestResults.Add(new LabTestResult { LabTestId = test.Id, TestResult = result });
        await context.SaveChangesAsync();
    }

    private static string[] Segments(NotificationBuild build) =>
        build.Message!.Split('\r', StringSplitOptions.RemoveEmptyEntries);

    private static List<string[]> Observations(NotificationBuild build, string phinVar) =>
        Segments(build)
            .Where(segment => segment.StartsWith("OBX", StringComparison.Ordinal))
            .Select(segment => segment.Split('|'))
            .Where(fields => fields[3].StartsWith(phinVar + "^", StringComparison.Ordinal))
            .ToList();

    private static string[] Observation(NotificationBuild build, string phinVar) =>
        Assert.Single(Observations(build, phinVar));

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
    public const string CodedVariable = "TST200";
    public const string LabVariable = "TST201";
    public const string HepLabVariable = "TST202";
    public const string ValueSetName = "TST_Integration_YesNo";
    public const string HepLabGroupName = "Integration test hepatitis";
    public const int KeptTestType = 990001;
    public const int ExcludedTestType = 990002;
    private const string RecordNumberVariable = "DEM197";

    private static readonly string[] SeededVariables = [RecordNumberVariable, StaticVariable, UnformattedVariable];

    private static readonly string[] Variables = [.. SeededVariables, CodedVariable, LabVariable, HepLabVariable];

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
            SeededVariables.Select(phinVar => new GuidePhinVariable { GuideId = guide.Id, PhinVar = phinVar }));
        context.RuleSetFields.Add(new RuleSetField { RuleSetConfigId = recordNumberConfig.Id, Field = "record_number" });
        context.RuleSetData.Add(new RuleSetData
        {
            RuleSetConfigId = staticConfig.Id, DataName = "staticValue", DataValue = StaticValue,
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        return guide.Id;
    }

    /// <summary>
    /// Adds a variable to the guide, formatted by a <paramref name="ruleSet"/>
    /// rule set configured as <paramref name="config"/>, reading
    /// <paramref name="field"/> and carrying <paramref name="data"/>.
    /// </summary>
    public static async Task AddVariable(
        SurvsysDbContext context,
        int guideId,
        string phinVar,
        string ruleSet,
        RuleSetConfig config,
        string? field = null,
        (string Name, string Value)? data = null)
    {
        var ruleSetId = await context.RuleSets.Where(item => item.Label == ruleSet).Select(item => item.Id).SingleAsync();
        context.RuleSetConfigs.Add(config);
        await context.SaveChangesAsync();

        context.PhinVariables.Add(new Data.PhinVariable
        {
            PhinVar = phinVar, Description = phinVar, RuleSetId = ruleSetId, RuleSetConfigId = config.Id,
        });
        context.GuidePhinVariables.Add(new GuidePhinVariable { GuideId = guideId, PhinVar = phinVar });
        if (field is not null)
        {
            context.RuleSetFields.Add(new RuleSetField { RuleSetConfigId = config.Id, Field = field });
        }

        if (data is { } item)
        {
            context.RuleSetData.Add(new RuleSetData { RuleSetConfigId = config.Id, DataName = item.Name, DataValue = item.Value });
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    /// <summary>Takes the seeded rows out again, with <paramref name="conditionId"/>'s lab overrides.</summary>
    public static async Task Clear(SurvsysDbContext context, int? conditionId = null)
    {
        await context.PhinValueSets.Where(item => item.ValueSetName == ValueSetName).ExecuteDeleteAsync();
        await context.HepLabGroups.Where(item => item.Name == HepLabGroupName).ExecuteDeleteAsync();
        if (conditionId is { } id)
        {
            await context.ConditionLabOverrides.Where(item => item.ConditionId == id).ExecuteDeleteAsync();
        }

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
