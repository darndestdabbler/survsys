using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The legacy vocabulary and rule cases of
/// <c>../nedss/docs/business-logic/EMSA_VOCABULARY_SYSTEM.md</c> and
/// <c>EMSA_RULES_ENGINE.md</c>, replayed through
/// <see cref="ElrIntakeService.Receive"/> against a seeded vocabulary and the
/// real database: the three translation tiers, the message filter's outcomes,
/// the case-management rules' "exactly one must match", and the investigating
/// agency chosen from the patient's zip.
/// </summary>
/// <remarks>
/// Each message is the fixture <c>oru-r01-chlamydia.hl7</c> with its sender,
/// codes, collection date or zip changed for the case. The vocabulary is seeded
/// before each test and every vocabulary row, rule and the test's own zip rows
/// and sending facility are deleted after it, so the suite's other intake tests
/// start from an empty vocabulary as they expect. The collection dates are
/// relative to today, because the filter's window is.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class VocabularyIntakeTests(SqlServerFixture server) : IAsyncLifetime
{
    private const string Sender = "VOCAB_LAB";
    private const string SingleAgencyZip = "84901";
    private const string SharedZip = "84902";

    private int labId;
    private int chlamydia;
    private int salmonellosis;

    public async Task InitializeAsync()
    {
        await Clear();
        await using var context = server.NewContext();

        var conditions = await context.Conditions.OrderBy(condition => condition.Id)
            .Select(condition => condition.Id)
            .Take(2)
            .ToListAsync();
        (chlamydia, salmonellosis) = (conditions[0], conditions[1]);

        var facility = new Facility { Name = "Vocabulary intake test lab", ShortName = Sender, AgencyId = 1 };
        var salmonella = new VocabMasterOrganism
        {
            Snomed = "27268008",
            SnomedAlt = "115412001",
            OrganismName = "Salmonella",
            ConditionId = salmonellosis,
        };
        var detected = new VocabMasterValue { Category = "test_result", Concept = "Detected", AppValue = "Positive" };
        var notDetected = new VocabMasterValue
        {
            Category = "test_result",
            Concept = "Not detected",
            AppValue = "Negative",
        };
        var canonical = new VocabMasterLoinc { Loinc = "21613-5", ConditionId = chlamydia };
        context.AddRange(
            facility,
            salmonella,
            detected,
            notDetected,
            canonical,
            new VocabMasterLoinc { Loinc = "11585-7", ConditionId = chlamydia },
            new VocabMasterLoinc { Loinc = "625-4", OrganismFromResult = true },
            new ZipCodeAgency { ZipCode = SingleAgencyZip, AgencyId = 1 },
            new ZipCodeAgency { ZipCode = SharedZip, AgencyId = 2 },
            new ZipCodeAgency { ZipCode = SharedZip, AgencyId = 3 });
        await context.SaveChangesAsync();
        labId = facility.Id;

        context.AddRange(
            // The ARUP case: the lab's own 43304-5 resolves to the canonical master.
            new VocabChildLoinc { LabId = labId, ChildLoinc = "43304-5", MasterLoincId = canonical.Id },
            new VocabChildValue { LabId = labId, Category = "test_result", ChildCode = "260373001", MasterId = detected.Id },
            new VocabChildValue
            {
                LabId = labId,
                Category = "test_result",
                ChildCode = "260415000",
                MasterId = notDetected.Id,
            },
            new VocabChildValue { LabId = labId, Category = "organism", ChildCode = "SALM", MasterId = salmonella.Id });
        await context.SaveChangesAsync();

        // Chlamydia: a six-month filter; a positive opens and updates a case,
        // a negative only updates one, as surveillance.
        await Rule(chlamydia, VocabRule.MessageFilterKind, ("DaysSinceCollection", "<", "183"));
        await Rule(chlamydia, VocabRule.CaseManagementKind, new Flags(true, true, false), ("TestResult", "=", "Positive"));
        await Rule(chlamydia, VocabRule.CaseManagementKind, new Flags(false, true, true), ("TestResult", "=", "Negative"));

        // Salmonellosis: always accept; a Salmonella organism opens a case.
        await Rule(salmonellosis, VocabRule.MessageFilterKind);
        await Rule(salmonellosis, VocabRule.CaseManagementKind, new Flags(true, false, false), ("Organism", "=", "Salmonella"));
    }

    public Task DisposeAsync() => Clear();

    [Fact]
    public async Task TheArupChildLoincResolvesThroughItsMaster()
    {
        var staged = await Receive(Message(loinc: "43304-5"));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        var result = Assert.Single(staged.Results);
        Assert.Equal("21613-5", result.MasterLoinc);
        Assert.Equal(chlamydia, result.ConditionId);
        Assert.Equal(chlamydia, staged.ConditionId);
    }

    [Fact]
    public async Task AMasterLoincIsReadDirectlyAndItsResultInterpreted()
    {
        var staged = await Receive(Message());

        var result = Assert.Single(staged.Results);
        Assert.Equal("11585-7", result.MasterLoinc);
        Assert.Equal("Positive", result.Interpretation);
        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Equal((true, true, false), (staged.AllowNewCase, staged.AllowUpdateCase, staged.IsSurveillance));
    }

    [Fact]
    public async Task AChildResultCodeIsTranslatedToItsInterpretationAndDecidesTheRule()
    {
        var staged = await Receive(Message(result: "260415000^Not detected^SCT"));

        Assert.Equal("Negative", Assert.Single(staged.Results).Interpretation);
        Assert.Equal((false, true, true), (staged.AllowNewCase, staged.AllowUpdateCase, staged.IsSurveillance));
    }

    [Theory]
    [InlineData("27268008^Salmonella^SCT")]
    [InlineData("115412001^Salmonella^SCT")]
    [InlineData("SALM^Salmonella^L")]
    public async Task AnOrganismIsTakenFromTheResultsSnomedOrTheLabsOwnCode(string result)
    {
        var staged = await Receive(Message(loinc: "625-4", result: result));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
        Assert.Equal(salmonellosis, staged.ConditionId);
        Assert.Equal("625-4", Assert.Single(staged.Results).MasterLoinc);
        Assert.NotNull(staged.Results[0].OrganismId);
        Assert.True(staged.AllowNewCase);
    }

    [Fact]
    public async Task AnUnmatchedLoincIsHeldAsAnException()
    {
        var staged = await Receive(Message(loinc: "00000-0"));

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("vocabulary", staged.ExceptionDetail);
        Assert.Null(Assert.Single(staged.Results).MasterLoinc);
    }

    [Fact]
    public async Task TheAlwaysAcceptFilterAcceptsAnOldMessage()
    {
        var staged = await Receive(Message(loinc: "625-4", result: "27268008^Salmonella^SCT", collectedDaysAgo: 900));

        Assert.Equal(MessageReviewStatusRules.EntryCode, staged.ReviewStatus);
    }

    [Theory]
    [InlineData(10, MessageReviewStatusRules.EntryCode)]
    [InlineData(300, MessageReviewStatusRules.FilteredCode)]
    public async Task TheSixMonthWindowAcceptsARecentMessageAndFiltersAnOldOne(int daysAgo, string expected)
    {
        var staged = await Receive(Message(collectedDaysAgo: daysAgo));

        Assert.Equal(expected, staged.ReviewStatus);
    }

    [Fact]
    public async Task AMissingCollectionDateIsHeldAsAnException()
    {
        var staged = await Receive(Message(collectedDaysAgo: null));

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("collection date", staged.ExceptionDetail);
    }

    [Fact]
    public async Task AConditionWithNoFilterRuleIsHeldAsAnException()
    {
        await RemoveRules(chlamydia, VocabRule.MessageFilterKind);

        var staged = await Receive(Message());

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("no message filter rule", staged.ExceptionDetail);
    }

    [Fact]
    public async Task NoCaseManagementRuleDefinedIsHeldAsAnException()
    {
        await RemoveRules(chlamydia, VocabRule.CaseManagementKind);

        var staged = await Receive(Message());

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("has no case-management rule", staged.ExceptionDetail);
    }

    [Fact]
    public async Task NoCaseManagementRuleMatchedIsHeldAsAnException()
    {
        var staged = await Receive(Message(result: "419984006^Inconclusive^SCT"));

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("No case-management rule", staged.ExceptionDetail);
        Assert.Null(staged.AllowNewCase);
    }

    [Fact]
    public async Task MoreThanOneCaseManagementRuleMatchedIsHeldAsAnException()
    {
        await Rule(chlamydia, VocabRule.CaseManagementKind, new Flags(true, false, true), ("Loinc", "=", "11585-7"));

        var staged = await Receive(Message());

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, staged.ReviewStatus);
        Assert.Contains("More than one", staged.ExceptionDetail);
    }

    [Theory]
    [InlineData(SingleAgencyZip, 1)]
    [InlineData(SharedZip, null)]
    [InlineData("84101", null)]
    public async Task TheAgencyIsChosenFromThePatientsZip(string zip, int? expected)
    {
        var staged = await Receive(Message(zip: zip));

        Assert.Equal(expected, staged.AgencyId);
    }

    private sealed record Flags(bool AllowNewCase, bool AllowUpdateCase, bool IsSurveillance);

    private Task Rule(int conditionId, string kind, params (string Field, string Operator, string Value)[] conditions) =>
        Rule(conditionId, kind, new Flags(false, false, false), conditions);

    private async Task Rule(
        int conditionId, string kind, Flags flags, params (string Field, string Operator, string Value)[] conditions)
    {
        await using var context = server.NewContext();

        var rule = new VocabRule
        {
            ConditionId = conditionId,
            Kind = kind,
            ExecutionOrder = 1,
            AllowNewCase = flags.AllowNewCase,
            AllowUpdateCase = flags.AllowUpdateCase,
            IsSurveillance = flags.IsSurveillance,
        };
        context.VocabRules.Add(rule);
        await context.SaveChangesAsync();

        context.VocabRuleConditions.AddRange(conditions.Select(condition => new VocabRuleCondition
        {
            VocabRuleId = rule.Id,
            Field = condition.Field,
            Operator = condition.Operator,
            Value = condition.Value,
        }));
        await context.SaveChangesAsync();
    }

    private async Task RemoveRules(int conditionId, string kind)
    {
        await using var context = server.NewContext();

        var ids = await context.VocabRules
            .Where(rule => rule.ConditionId == conditionId && rule.Kind == kind)
            .Select(rule => rule.Id)
            .ToListAsync();
        await context.VocabRuleConditions.Where(condition => ids.Contains(condition.VocabRuleId)).ExecuteDeleteAsync();
        await context.VocabRules.Where(rule => ids.Contains(rule.Id)).ExecuteDeleteAsync();
    }

    /// <summary>
    /// The fixture message from this test's sender, with its LOINC (OBR-4 and
    /// OBX-3), its coded result (OBX-5), its collection date (OBR-7, null for
    /// none) and the patient's zip changed as asked.
    /// </summary>
    private static string Message(
        string loinc = "11585-7",
        string result = "260373001^Detected^SCT",
        int? collectedDaysAgo = 10,
        string zip = "84101")
    {
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Hl7", "oru-r01-chlamydia.hl7"));
        var collected = collectedDaysAgo is { } days
            ? DateTime.UtcNow.Date.AddDays(-days).ToString("yyyyMMdd", CultureInfo.InvariantCulture) + "080000"
            : string.Empty;

        return fixture
            .Replace("|TEST_LAB^2.16", $"|{Sender}^2.16", StringComparison.Ordinal)
            .Replace("|||20260312080000", $"|||{collected}", StringComparison.Ordinal)
            .Replace("11585-7^", $"{loinc}^", StringComparison.Ordinal)
            .Replace("260373001^Detected^SCT", result, StringComparison.Ordinal)
            .Replace("^UT^84101", $"^UT^{zip}", StringComparison.Ordinal);
    }

    private async Task<StagedMessage> Receive(string raw)
    {
        await using var context = server.NewContext();

        var staged = await ElrIntakeService.Receive(
            new OruR01Parser(), context, raw, "vocabulary-intake-test", CancellationToken.None);

        return Assert.Single(staged);
    }

    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.VocabRuleConditions.ExecuteDeleteAsync();
        await context.VocabRules.ExecuteDeleteAsync();
        await context.VocabChildValues.ExecuteDeleteAsync();
        await context.VocabChildLoincs.ExecuteDeleteAsync();
        await context.VocabMasterValues.ExecuteDeleteAsync();
        await context.VocabMasterLoincs.ExecuteDeleteAsync();
        await context.VocabMasterOrganisms.ExecuteDeleteAsync();
        await context.ZipCodeAgencies
            .Where(row => row.ZipCode == SingleAgencyZip || row.ZipCode == SharedZip)
            .ExecuteDeleteAsync();
        await context.Facilities.Where(facility => facility.ShortName == Sender).ExecuteDeleteAsync();
    }
}
