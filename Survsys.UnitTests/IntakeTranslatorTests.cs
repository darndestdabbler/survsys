using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The translation intake applies to a queued message: its results through
/// the vocabulary, the message through its condition's filter and
/// case-management rules, and the patient's zip to an agency — every outcome,
/// over in-memory reference data.
/// </summary>
public class IntakeTranslatorTests
{
    private const int Lab = 200;
    private const int Chlamydia = 42;
    private const int Salmonellosis = 70;
    private static readonly DateOnly Today = new(2026, 3, 20);

    private static readonly VocabularyMatcher Matcher = new(new Vocabulary(
        MasterLoincs:
        [
            new MasterLoincEntry("11585-7", ConditionId: Chlamydia, OrganismId: 7, OrganismFromResult: false),
            new MasterLoincEntry("625-4", ConditionId: null, OrganismId: null, OrganismFromResult: true),
            new MasterLoincEntry("718-7", ConditionId: null, OrganismId: null, OrganismFromResult: false),
        ],
        ChildLoincs: [new ChildLoincEntry("CT-AG", Lab, "11585-7")],
        MasterOrganisms:
        [
            new MasterOrganismEntry("260373001", OrganismId: 7, ConditionId: Chlamydia, OrganismName: "Chlamydia"),
            new MasterOrganismEntry("27268008", OrganismId: 9, ConditionId: Salmonellosis, OrganismName: "Salmonella"),
        ],
        MasterValues:
        [
            new MasterValueEntry(1, VocabularyCategory.TestResult, "Detected", "Positive"),
            new MasterValueEntry(2, VocabularyCategory.TestResult, "Not detected", "Negative"),
            new MasterValueEntry(3, VocabularyCategory.Specimen, "Serum", "Serum"),
        ],
        ChildValues:
        [
            new ChildValueEntry(Lab, VocabularyCategory.TestResult, "260373001", 1),
            new ChildValueEntry(Lab, VocabularyCategory.TestResult, "Not detected", 2),
            new ChildValueEntry(Lab, VocabularyCategory.Specimen, "119364003", 3),
        ]));

    private static ConditionExpression When(params ExpressionCondition[] conditions) =>
        new(Value: null, OtherSpecified: null, ExecutionOrder: 1, Conditions: conditions);

    private static ExpressionCondition Is(string field, string @operator, string value) =>
        new(field, @operator, value, AndOr: null, ValueIsField: false);

    private static readonly CaseManagementRule PositiveOpensACase = new(
        When(Is(IntakeTranslator.TestResultField, "=", "Positive")),
        AllowNewCase: true, AllowUpdateCase: true, IsSurveillance: false);

    private static readonly CaseManagementRule NegativeUpdatesOnly = new(
        When(Is(IntakeTranslator.TestResultField, "=", "Negative")),
        AllowNewCase: false, AllowUpdateCase: true, IsSurveillance: true);

    private static IntakeReference Reference(
        IReadOnlyList<ConditionExpression>? filter = null,
        IReadOnlyList<CaseManagementRule>? caseManagement = null,
        IReadOnlyList<ZipCodeAgency>? zips = null) =>
        new(
            Matcher,
            new Dictionary<int, IntakeConditionRules>
            {
                [Chlamydia] = new(filter ?? [When()], caseManagement ?? [PositiveOpensACase, NegativeUpdatesOnly]),
                [Salmonellosis] = new(
                    [When()],
                    [new CaseManagementRule(
                        When(Is(IntakeTranslator.OrganismField, "=", "Salmonella")), true, false, false)]),
            },
            zips ?? []);

    private static StagedMessage Message(params StagedMessageResult[] results) => new()
    {
        ReviewStatus = MessageReviewStatusRules.EntryCode,
        SendingFacility = "TEST_LAB",
        PatientZip = "84101",
        SpecimenSourceCode = "119364003",
        CollectedOn = new DateOnly(2026, 3, 12),
        Results = results.ToList(),
    };

    private static StagedMessageResult Result(string? loinc, string? value, string? coded = null, int sequence = 0) =>
        new() { Sequence = sequence, ObservationCode = loinc, ResultValue = value, CodedResult = coded };

    [Fact]
    public void AResultIsTranslatedAndTheOneMatchingRuleSetsTheFlags()
    {
        var message = Message(Result("11585-7", "260373001", "Detected"));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal(MessageReviewStatusRules.EntryCode, message.ReviewStatus);
        Assert.Null(message.ExceptionDetail);
        Assert.Null(message.ExceptionCategory);
        Assert.Equal(Chlamydia, message.ConditionId);
        Assert.True(message.AllowNewCase);
        Assert.True(message.AllowUpdateCase);
        Assert.False(message.IsSurveillance);

        var result = Assert.Single(message.Results);
        Assert.Equal("11585-7", result.MasterLoinc);
        Assert.Equal(Chlamydia, result.ConditionId);
        Assert.Equal(7, result.OrganismId);
        Assert.Equal("Positive", result.Interpretation);
    }

    [Fact]
    public void ALabsChildLoincAndCodedTextAreTranslated()
    {
        var message = Message(Result("CT-AG", "N", "Not detected"));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        var result = Assert.Single(message.Results);
        Assert.Equal("11585-7", result.MasterLoinc);
        Assert.Equal("Negative", result.Interpretation);
        Assert.False(message.AllowNewCase);
        Assert.True(message.IsSurveillance);
    }

    [Fact]
    public void AnOrganismFromTheResultDecidesTheCondition()
    {
        var message = Message(Result("625-4", "27268008"));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal(MessageReviewStatusRules.EntryCode, message.ReviewStatus);
        Assert.Equal(Salmonellosis, message.ConditionId);
        Assert.Equal(9, Assert.Single(message.Results).OrganismId);
        Assert.True(message.AllowNewCase);
        Assert.False(message.AllowUpdateCase);
    }

    [Fact]
    public void TheFirstResultThatResolvesToAConditionDecides()
    {
        var message = Message(
            Result("718-7", "13.5", sequence: 0),
            Result("99999-9", "x", sequence: 1),
            Result("11585-7", "260373001", sequence: 2));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal(Chlamydia, message.ConditionId);
        Assert.Equal("718-7", message.Results[0].MasterLoinc);
        Assert.Null(message.Results[0].ConditionId);
        Assert.Null(message.Results[1].MasterLoinc);
    }

    [Fact]
    public void AMessageNoResultOfWhichResolvesIsHeld()
    {
        var message = Message(Result("99999-9", "260373001"), Result("718-7", "13.5"));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("vocabulary", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.LoincNotMappedCode, message.ExceptionCategory);
        Assert.Null(message.ConditionId);
    }

    [Fact]
    public void AnOldMessageIsFiltered()
    {
        var withinSixMonths = When(Is(IntakeTranslator.DaysSinceCollectionField, "<", "183"));
        var message = Message(Result("11585-7", "260373001"));
        message.CollectedOn = Today.AddDays(-200);

        IntakeTranslator.Translate(message, Lab, Reference(filter: [withinSixMonths]), Today);

        Assert.Equal(MessageReviewStatusRules.FilteredCode, message.ReviewStatus);
        Assert.Null(message.AllowNewCase);
    }

    [Fact]
    public void ASpecimenIsTranslatedForTheRules()
    {
        var serumOnly = When(Is(IntakeTranslator.SpecimenField, "=", "Serum"));
        var message = Message(Result("11585-7", "260373001"));

        IntakeTranslator.Translate(message, Lab, Reference(filter: [serumOnly]), Today);

        Assert.Equal(MessageReviewStatusRules.EntryCode, message.ReviewStatus);
    }

    [Fact]
    public void AMissingCollectionDateIsHeld()
    {
        var withinSixMonths = When(Is(IntakeTranslator.DaysSinceCollectionField, "<", "183"));
        var message = Message(Result("11585-7", "260373001"));
        message.CollectedOn = null;

        IntakeTranslator.Translate(message, Lab, Reference(filter: [withinSixMonths]), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("collection date", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.MissingRequiredFieldCode, message.ExceptionCategory);
    }

    [Fact]
    public void AConditionWithNoFilterRuleIsHeld()
    {
        var message = Message(Result("11585-7", "260373001"));

        IntakeTranslator.Translate(message, Lab, Reference(filter: []), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("message filter", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.NoCaseManagementRulesCode, message.ExceptionCategory);
    }

    [Fact]
    public void AConditionWithNoRulesAtAllIsHeld()
    {
        var message = Message(Result("11585-7", "260373001"));
        var reference = new IntakeReference(Matcher, new Dictionary<int, IntakeConditionRules>(), []);

        IntakeTranslator.Translate(message, Lab, reference, Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Equal(Chlamydia, message.ConditionId);
    }

    [Fact]
    public void NoCaseManagementRuleMatchingIsHeld()
    {
        // A result whose code translates to no interpretation matches neither rule.
        var message = Message(Result("11585-7", "EQV", "Equivocal"));

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("No case-management rule", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.NoCaseManagementRuleTrueCode, message.ExceptionCategory);
        Assert.Null(message.AllowNewCase);
    }

    [Fact]
    public void NoCaseManagementRuleDefinedIsHeld()
    {
        var message = Message(Result("11585-7", "260373001"));

        IntakeTranslator.Translate(message, Lab, Reference(caseManagement: []), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("has no case-management rule", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.NoCaseManagementRulesCode, message.ExceptionCategory);
    }

    [Fact]
    public void MoreThanOneCaseManagementRuleMatchingIsHeld()
    {
        var message = Message(Result("11585-7", "260373001"));

        IntakeTranslator.Translate(
            message, Lab, Reference(caseManagement: [PositiveOpensACase, PositiveOpensACase]), Today);

        Assert.Equal(MessageReviewStatusRules.ExceptionCode, message.ReviewStatus);
        Assert.Contains("More than one", message.ExceptionDetail);
        Assert.Equal(MessageExceptionRules.NoCaseManagementRuleTrueCode, message.ExceptionCategory);
    }

    [Theory]
    [InlineData("84101", 5)]
    [InlineData("84101-1234", 5)]
    [InlineData("84102", null)] // two agencies claim it
    [InlineData("99999", null)] // no agency claims it
    [InlineData(null, null)]
    public void TheAgencyIsTheOneThePatientsZipResolvesTo(string? zip, int? expected)
    {
        var mapping = new List<ZipCodeAgency>
        {
            new() { ZipCode = "84101", AgencyId = 5 },
            new() { ZipCode = "84102", AgencyId = 5 },
            new() { ZipCode = "84102", AgencyId = 6 },
        };
        var message = Message(Result("11585-7", "260373001"));
        message.PatientZip = zip;

        IntakeTranslator.Translate(message, Lab, Reference(zips: mapping), Today);

        Assert.Equal(expected, message.AgencyId);
    }

    [Fact]
    public void AMessageAlreadyHeldIsLeftAsItIs()
    {
        var message = Message(Result("11585-7", "260373001"));
        message.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
        message.ExceptionDetail = "unknown sender";

        IntakeTranslator.Translate(message, Lab, Reference(), Today);

        Assert.Equal("unknown sender", message.ExceptionDetail);
        Assert.Null(message.ConditionId);
        Assert.Null(message.Results[0].MasterLoinc);
    }
}
