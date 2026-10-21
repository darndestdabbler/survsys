using Microsoft.EntityFrameworkCore;
using Survsys.Data;

namespace Survsys.IntegrationTests;

/// <summary>
/// The vocabulary and rules a test seeds so the fixture message
/// <c>oru-r01-chlamydia.hl7</c> is translated and stays queued for review:
/// master LOINC <c>11585-7</c> mapped to a fixture condition, the lab's result
/// code <c>260373001</c> translated to <c>Positive</c>, an always-accept
/// message filter and one case-management rule for a positive result. Disposing
/// it removes every row it added, so the next test starts from the same
/// vocabulary.
/// </summary>
internal sealed class IntakeVocabulary : IAsyncDisposable
{
    /// <summary>The master LOINC the fixture message's order and result carry.</summary>
    public const string Loinc = "11585-7";

    /// <summary>The coded result (OBX-5.1) the fixture message carries.</summary>
    public const string ResultCode = "260373001";

    private readonly SqlServerFixture _server;
    private readonly List<int> _ruleIds = [];
    private int _masterLoincId;
    private int _masterValueId;
    private int _childValueId;

    private IntakeVocabulary(SqlServerFixture server) => _server = server;

    /// <summary>The fixture condition the LOINC maps to and the rules are on.</summary>
    public int ConditionId { get; private set; }

    /// <summary>
    /// Seeds the accepting vocabulary for the sending facility on file under
    /// <paramref name="shortName"/>.
    /// </summary>
    public static async Task<IntakeVocabulary> SeedAccepting(SqlServerFixture server, string shortName)
    {
        var seeded = new IntakeVocabulary(server);
        await using var context = server.NewContext();

        var labId = await context.Facilities
            .Where(facility => facility.ShortName == shortName)
            .OrderBy(facility => facility.Id)
            .Select(facility => facility.Id)
            .FirstAsync();
        seeded.ConditionId = await context.Conditions
            .OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .FirstAsync();

        var masterLoinc = new VocabMasterLoinc { Loinc = Loinc, ConditionId = seeded.ConditionId };
        var masterValue = new VocabMasterValue { Category = "test_result", Concept = "Detected", AppValue = "Positive" };
        context.AddRange(masterLoinc, masterValue);
        await context.SaveChangesAsync();
        seeded._masterLoincId = masterLoinc.Id;
        seeded._masterValueId = masterValue.Id;

        var childValue = new VocabChildValue
        {
            LabId = labId,
            Category = "test_result",
            ChildCode = ResultCode,
            MasterId = masterValue.Id,
        };
        var filter = new VocabRule
        {
            ConditionId = seeded.ConditionId,
            Kind = VocabRule.MessageFilterKind,
            ExecutionOrder = 1,
        };
        var positive = new VocabRule
        {
            ConditionId = seeded.ConditionId,
            Kind = VocabRule.CaseManagementKind,
            ExecutionOrder = 1,
            AllowNewCase = true,
            AllowUpdateCase = true,
        };
        context.AddRange(childValue, filter, positive);
        await context.SaveChangesAsync();
        seeded._childValueId = childValue.Id;
        seeded._ruleIds.AddRange([filter.Id, positive.Id]);

        context.VocabRuleConditions.Add(new VocabRuleCondition
        {
            VocabRuleId = positive.Id,
            Field = "TestResult",
            Operator = "=",
            Value = "Positive",
        });
        await context.SaveChangesAsync();

        return seeded;
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = _server.NewContext();

        await context.VocabRuleConditions.Where(condition => _ruleIds.Contains(condition.VocabRuleId)).ExecuteDeleteAsync();
        await context.VocabRules.Where(rule => _ruleIds.Contains(rule.Id)).ExecuteDeleteAsync();
        await context.VocabChildValues.Where(child => child.Id == _childValueId).ExecuteDeleteAsync();
        await context.VocabMasterValues.Where(master => master.Id == _masterValueId).ExecuteDeleteAsync();
        await context.VocabMasterLoincs.Where(master => master.Id == _masterLoincId).ExecuteDeleteAsync();
    }
}
