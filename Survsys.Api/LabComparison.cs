using Survsys.Contracts;

namespace Survsys.Api;

/// <summary>What comparing a message's lab with a case's labs decided.</summary>
public enum LabComparisonOutcome
{
    /// <summary>No lab on the case is this one: the message's is added.</summary>
    Add,

    /// <summary>The case holds this lab: tests it lacks are knitted on, and test statuses upgraded.</summary>
    Update,

    /// <summary>The case already holds what the message reports: no lab is written.</summary>
    Duplicate,

    /// <summary>The case holds this test with another result: a reviewer's.</summary>
    Exception,
}

/// <summary>A test on a case's lab, with its first result: what a message's test is compared with.</summary>
public sealed record StoredTest(int Id, string? LoincCode, int? TestStatusId, string? ResultValue, string? TestResult, int? OrganismId);

/// <summary>A susceptibility on a case's lab: its agent and result.</summary>
public sealed record StoredResistTest(int? ResistTestAgentId, int? ResistTestResultId);

/// <summary>A lab on a case, with its tests and susceptibilities.</summary>
public sealed record StoredLab(
    int Id,
    int? LabFacilityId,
    DateOnly? CollectionDate,
    string? AccessionNo,
    IReadOnlyList<StoredTest> Tests,
    IReadOnlyList<StoredResistTest> ResistTests);

/// <summary>A stored test whose status a message upgrades.</summary>
public sealed record TestStatusUpgrade(int TestId, int TestStatusId);

/// <summary>
/// The comparison's answer: the outcome, the stored lab an update is to, what
/// it knits onto that lab and the statuses it upgrades, and for an exception
/// why.
/// </summary>
public sealed record LabComparisonResult(
    LabComparisonOutcome Outcome,
    int? LabId = null,
    IReadOnlyList<CreateLabTestRequest>? NewTests = null,
    IReadOnlyList<CreateLabResistTestRequest>? NewResistTests = null,
    IReadOnlyList<TestStatusUpgrade>? Upgrades = null,
    string? Reason = null);

/// <summary>
/// The legacy <c>compareLabsForUpdateRecord</c>: how a message's lab lands on
/// a case that may already hold it.
/// </summary>
public static class LabComparison
{
    /// <summary>
    /// Compares the lab a message writes with the labs on the case.
    /// <paramref name="statusRank"/> ranks each test status id by the
    /// test_status list's sort order, so a later status upgrades an earlier.
    /// </summary>
    /// <remarks>
    /// The same lab is one from the same facility with the same accession and
    /// collection date. On it, a test the message reports under a LOINC the lab
    /// lacks is knitted on, and a susceptibility to an agent it lacks added; a
    /// test it holds with another result or organism, or a susceptibility with
    /// another result, is an exception; one it holds alike is upgraded where
    /// its status ranks higher. Nothing to knit or upgrade is a duplicate. With
    /// no same lab, a message whose every test another lab already reports on
    /// the same date alike is a duplicate from another reporting lab, and any
    /// other is added.
    /// </remarks>
    public static LabComparisonResult Compare(
        CreateLabRequest incoming,
        IReadOnlyList<StoredLab> stored,
        IReadOnlyDictionary<int, int> statusRank)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(statusRank);

        var tests = incoming.Tests ?? [];
        var resistTests = incoming.ResistTests ?? [];

        var same = stored.FirstOrDefault(lab =>
            lab.LabFacilityId == incoming.LabFacilityId
            && lab.CollectionDate == incoming.CollectionDate
            && Same(lab.AccessionNo, incoming.AccessionNo));

        if (same is null)
        {
            var reportedElsewhere = tests.Count > 0 && tests.All(test => stored.Any(lab =>
                lab.CollectionDate == incoming.CollectionDate
                && lab.Tests.Any(held => Same(held.LoincCode, test.LoincCode) && SameResult(held, test))));

            return new LabComparisonResult(reportedElsewhere ? LabComparisonOutcome.Duplicate : LabComparisonOutcome.Add);
        }

        var newTests = new List<CreateLabTestRequest>();
        var upgrades = new List<TestStatusUpgrade>();

        foreach (var test in tests)
        {
            if (same.Tests.FirstOrDefault(held => Same(held.LoincCode, test.LoincCode)) is not { } held)
            {
                newTests.Add(test);
            }
            else if (!SameResult(held, test))
            {
                return Exception(
                    $"Accession {incoming.AccessionNo} already reports LOINC {test.LoincCode} as "
                    + $"'{held.TestResult ?? held.ResultValue}', and this message reports "
                    + $"'{Result(test)?.TestResult ?? Result(test)?.ResultValue}'.");
            }
            else if (test.TestStatusId is { } status && Rank(status) > (held.TestStatusId is { } was ? Rank(was) : int.MinValue))
            {
                upgrades.Add(new TestStatusUpgrade(held.Id, status));
            }
        }

        var newResistTests = new List<CreateLabResistTestRequest>();

        foreach (var resist in resistTests)
        {
            if (same.ResistTests.FirstOrDefault(held => held.ResistTestAgentId == resist.ResistTestAgentId) is not { } held)
            {
                newResistTests.Add(resist);
            }
            else if (held.ResistTestResultId != resist.ResistTestResultId)
            {
                return Exception(
                    $"Accession {incoming.AccessionNo} already reports a different susceptibility "
                    + $"to agent {resist.ResistTestAgentId}.");
            }
        }

        return newTests.Count + newResistTests.Count + upgrades.Count == 0
            ? new LabComparisonResult(LabComparisonOutcome.Duplicate, same.Id)
            : new LabComparisonResult(LabComparisonOutcome.Update, same.Id, newTests, newResistTests, upgrades);

        int Rank(int statusId) => statusRank.TryGetValue(statusId, out var rank) ? rank : int.MinValue;
    }

    private static LabComparisonResult Exception(string reason) =>
        new(LabComparisonOutcome.Exception, Reason: reason);

    private static CreateLabTestResultRequest? Result(CreateLabTestRequest test) => test.Results?.FirstOrDefault();

    private static bool SameResult(StoredTest held, CreateLabTestRequest test) =>
        Result(test) is var result
        && Same(held.ResultValue, result?.ResultValue)
        && Same(held.TestResult, result?.TestResult)
        && held.OrganismId == result?.OrganismId;

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim() ?? "", right?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
}
