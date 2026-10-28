using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The walkthrough contract, as far as a machine can keep it.
/// </summary>
/// <remarks>
/// The contract is in <c>docs/planning/solution-plan.md</c> and the operator
/// calls it non-negotiable: a walkthrough the reader cannot complete exactly
/// as written is a failure of the release. Most of it is a matter of what a
/// document says and what its test does, which only a reader can judge. Two
/// clauses are not: <strong>every walkthrough has the one test named after
/// it</strong>, and <strong>the index lists every walkthrough</strong>. Those
/// are the ones a session forgets, and they are the ones asserted here — in
/// the run of record, so that the session that forgot is told before the
/// operator finds out.
///
/// Named after the rule it keeps rather than after a source file, as
/// <see cref="ArchitectureTests"/> is.
/// </remarks>
public class WalkthroughContractTests
{
    /// <summary>The walkthroughs, relative to the repository root.</summary>
    private const string Walkthroughs = "docs/walkthroughs";

    /// <summary>The index, one of the two files in that folder that are not walkthroughs.</summary>
    private const string Index = "README.md";

    /// <summary>
    /// The guide to performing every walkthrough by hand, the other file in
    /// that folder that is not a walkthrough.
    /// </summary>
    private const string Guide = "testing-by-hand.md";

    /// <summary>The suite a walkthrough's test lives in.</summary>
    private const string Suite = "Survsys.E2ETests";

    [Fact]
    public void EveryWalkthroughHasTheOneTestNamedAfterIt()
    {
        var missing = Documents()
            .Select(document => (Document: document, Test: TestFileFor(document)))
            .Where(pair => !File.Exists(Path.Combine(Root(), Suite, pair.Test)))
            .Select(pair => $"{pair.Document} wants {Suite}/{pair.Test}")
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"A walkthrough has no test named after it: {string.Join("; ", missing)}. One "
            + "document, one test, a name that matches: a walkthrough nothing performs is a "
            + "promise to the operator that nothing is keeping.");
    }

    [Fact]
    public void TheIndexLinksEveryWalkthrough()
    {
        var index = File.ReadAllText(Path.Combine(Root(), Walkthroughs, Index));

        var unlisted = Documents()
            .Where(document => !index.Contains($"({document})", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            unlisted.Count == 0,
            $"{Walkthroughs}/{Index} does not link {string.Join(", ", unlisted)}. The index is "
            + "where the operator goes to find out what they can walk through, so a "
            + "walkthrough it does not link is one nobody will perform.");
    }

    [Fact]
    public void TheTestingByHandGuideLinksEveryWalkthrough()
    {
        var guide = File.ReadAllText(Path.Combine(Root(), Walkthroughs, Guide));

        var unlisted = Documents()
            .Where(document => !guide.Contains($"({document})", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            unlisted.Count == 0,
            $"{Walkthroughs}/{Guide} does not place {string.Join(", ", unlisted)}. The guide is "
            + "the order a person performs every walkthrough in by hand, so a walkthrough it "
            + "leaves out is one a round of testing by hand never reaches.");
    }

    /// <summary>
    /// The walkthrough documents, by file name. Asserted non-empty, because
    /// every check here would otherwise pass on a folder this test failed to
    /// find.
    /// </summary>
    private static IReadOnlyList<string> Documents()
    {
        var folder = Path.Combine(Root(), Walkthroughs);

        var documents = Directory
            .EnumerateFiles(folder, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(name => name != Index && name != Guide)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            documents.Count > 0,
            $"There are no walkthrough documents in {folder}. This repository has had one "
            + "since session 7, so either they have gone or this test is looking in the "
            + "wrong place — and either way the clauses below would pass on nothing.");

        return documents;
    }

    /// <summary>
    /// The test file a document's name asks for:
    /// <c>review-the-conditions-catalogue.md</c> wants
    /// <c>ReviewTheConditionsCatalogueTests.cs</c>.
    /// </summary>
    private static string TestFileFor(string document) =>
        string.Concat(Path.GetFileNameWithoutExtension(document)
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..]))
        + "Tests.cs";

    /// <summary>
    /// The repository root, found by walking up from this assembly's output
    /// until the solution file is beside us. Searched for rather than counted
    /// in parents, so a different build layout moves this test rather than
    /// breaking it.
    /// </summary>
    private static string Root()
    {
        for (var here = new DirectoryInfo(AppContext.BaseDirectory);
             here is not null;
             here = here.Parent)
        {
            if (File.Exists(Path.Combine(here.FullName, "Survsys.sln")))
            {
                return here.FullName;
            }
        }

        throw new InvalidOperationException(
            $"No Survsys.sln above {AppContext.BaseDirectory}, so this test cannot find the "
            + "repository root and therefore cannot read the walkthroughs. Run the suite from "
            + "its own build output inside the repository.");
    }
}
