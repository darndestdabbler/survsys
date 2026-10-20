using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// How a message's lab lands on a case: every outcome of
/// <see cref="LabComparison.Compare"/> — add, update by status upgrade and by
/// knitting, duplicate, and exception.
/// </summary>
public class LabComparisonTests
{
    private const int Preliminary = 677;
    private const int Final = 678;

    private static readonly IReadOnlyDictionary<int, int> Ranks = new Dictionary<int, int>
    {
        [Preliminary] = 30,
        [Final] = 40,
    };

    private static readonly DateOnly Collected = new(2026, 6, 1);

    private static CreateLabTestRequest Test(string loinc, string result, int? status = Final, int? organismId = null) =>
        new(TestStatusId: status, LoincCode: loinc, Results: [new CreateLabTestResultRequest(organismId, TestResult: result)]);

    private static CreateLabRequest Incoming(params CreateLabTestRequest[] tests) =>
        new(LabFacilityId: 5, CollectionDate: Collected, AccessionNo: "ACC-1", Tests: tests);

    private static StoredLab Stored(
        string accession = "ACC-1", int? facilityId = 5, IReadOnlyList<StoredResistTest>? resistTests = null) =>
        new(
            40,
            facilityId,
            Collected,
            accession,
            [new StoredTest(41, "625-4", Preliminary, null, "Positive", null)],
            resistTests ?? []);

    [Fact]
    public void ALabTheCaseDoesNotHoldIsAdded()
    {
        var result = LabComparison.Compare(Incoming(Test("6463-4", "Positive")), [Stored(accession: "ACC-2")], Ranks);

        Assert.Equal(LabComparisonOutcome.Add, result.Outcome);
    }

    [Fact]
    public void TheSameFindingFromAnotherLabIsADuplicate()
    {
        var result = LabComparison.Compare(
            Incoming(Test("625-4", "positive")), [Stored(accession: "OTHER", facilityId: 9)], Ranks);

        Assert.Equal(LabComparisonOutcome.Duplicate, result.Outcome);
    }

    [Fact]
    public void TheSameTestWithAHigherStatusIsAnUpgrade()
    {
        var result = LabComparison.Compare(Incoming(Test("625-4", "Positive", Final)), [Stored()], Ranks);

        Assert.Equal(LabComparisonOutcome.Update, result.Outcome);
        Assert.Equal(40, result.LabId);
        Assert.Equal([new TestStatusUpgrade(41, Final)], result.Upgrades);
        Assert.Empty(result.NewTests!);
    }

    [Fact]
    public void TheSameTestWithTheSameOrALowerStatusIsADuplicate()
    {
        var result = LabComparison.Compare(Incoming(Test("625-4", "Positive", Preliminary)), [Stored()], Ranks);

        Assert.Equal(LabComparisonOutcome.Duplicate, result.Outcome);
        Assert.Equal(40, result.LabId);
    }

    [Fact]
    public void ATestTheSameLabLacksIsKnittedOn()
    {
        var knitted = Test("6463-4", "Negative");

        var result = LabComparison.Compare(
            Incoming(Test("625-4", "Positive", Preliminary), knitted), [Stored()], Ranks);

        Assert.Equal(LabComparisonOutcome.Update, result.Outcome);
        Assert.Equal([knitted], result.NewTests);
        Assert.Empty(result.Upgrades!);
    }

    [Fact]
    public void TheSameTestWithAnotherResultIsAnException()
    {
        var result = LabComparison.Compare(Incoming(Test("625-4", "Negative")), [Stored()], Ranks);

        Assert.Equal(LabComparisonOutcome.Exception, result.Outcome);
        Assert.Contains("625-4", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ASusceptibilityIsAddedForANewAgentAndAnExceptionForAnotherResult()
    {
        var held = new StoredResistTest(1048, 1049);

        var added = LabComparison.Compare(
            Incoming() with { ResistTests = [new CreateLabResistTestRequest(ResistTestAgentId: 1044, ResistTestResultId: 1049)] },
            [Stored(resistTests: [held])],
            Ranks);
        var changed = LabComparison.Compare(
            Incoming() with { ResistTests = [new CreateLabResistTestRequest(ResistTestAgentId: 1048, ResistTestResultId: 1050)] },
            [Stored(resistTests: [held])],
            Ranks);

        Assert.Equal(LabComparisonOutcome.Update, added.Outcome);
        Assert.Single(added.NewResistTests!);
        Assert.Equal(LabComparisonOutcome.Exception, changed.Outcome);
    }
}
