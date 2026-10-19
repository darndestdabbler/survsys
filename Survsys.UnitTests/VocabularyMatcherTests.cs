using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The matcher read over a small in-memory vocabulary: a master LOINC to its
/// condition and organism, a SNOMED to its organism, a lab-specific child
/// LOINC that overrides the master reading, an organism taken from a result,
/// a lab's specimen and result codes translated to their application values,
/// and codes the vocabulary does not carry, which are unmatched rather than
/// refused.
/// </summary>
public class VocabularyMatcherTests
{
    // Master LOINC 11585-7 maps to condition 42 and organism 7 on its own.
    // Lab 200 reports that same code as its local code for a different test,
    // and its child mapping resolves it to master 43304-5 (condition 60,
    // organism 8) -- the precedence the matcher must honour.
    private static VocabularyMatcher Matcher() => new(new Vocabulary(
        MasterLoincs:
        [
            new MasterLoincEntry("11585-7", ConditionId: 42, OrganismId: 7, OrganismFromResult: false),
            new MasterLoincEntry("99999-9", ConditionId: 50, OrganismId: null, OrganismFromResult: false),
            new MasterLoincEntry("43304-5", ConditionId: 60, OrganismId: 8, OrganismFromResult: false),
        ],
        ChildLoincs:
        [
            new ChildLoincEntry(ChildLoinc: "11585-7", LabId: 200, MasterLoinc: "43304-5"),
        ],
        MasterOrganisms:
        [
            new MasterOrganismEntry("260373001", OrganismId: 7, ConditionId: 42),
            new MasterOrganismEntry(
                "27268008", OrganismId: 9, ConditionId: 70, SnomedAlt: "115412001", OrganismName: "Salmonella"),
            new MasterOrganismEntry("409822003", OrganismId: 10, ConditionId: null, OrganismName: "Bacteria"),
        ],
        MasterValues:
        [
            new MasterValueEntry(1, VocabularyCategory.TestResult, "Detected", AppValue: "Positive"),
            new MasterValueEntry(2, VocabularyCategory.Specimen, "Blood", AppValue: "Blood"),
            new MasterValueEntry(3, VocabularyCategory.TestResult, "Unmapped", AppValue: null),
        ],
        ChildValues:
        [
            new ChildValueEntry(200, VocabularyCategory.TestResult, "DET", MasterId: 1),
            new ChildValueEntry(200, VocabularyCategory.Specimen, "BLD", MasterId: 2),
            new ChildValueEntry(200, VocabularyCategory.TestResult, "UNK", MasterId: 3),
            new ChildValueEntry(200, VocabularyCategory.TestResult, "WRONG", MasterId: 2),
            new ChildValueEntry(200, VocabularyCategory.Organism, "SAL", MasterId: 9),
        ]));

    // Master 625-4 takes its organism from the result; master 625-5 does too
    // but names a condition of its own, for an organism that names none.
    private static VocabularyMatcher OrganismMatcher() => new(new Vocabulary(
        MasterLoincs:
        [
            new MasterLoincEntry("625-4", ConditionId: null, OrganismId: null, OrganismFromResult: true),
            new MasterLoincEntry("625-5", ConditionId: 80, OrganismId: null, OrganismFromResult: true),
            new MasterLoincEntry("11585-7", ConditionId: 42, OrganismId: 7, OrganismFromResult: false),
        ],
        ChildLoincs: [],
        MasterOrganisms:
        [
            new MasterOrganismEntry("260373001", OrganismId: 7, ConditionId: 42, OrganismName: "Bordetella"),
            new MasterOrganismEntry(
                "27268008", OrganismId: 9, ConditionId: 70, SnomedAlt: "115412001", OrganismName: "Salmonella"),
            new MasterOrganismEntry("409822003", OrganismId: 10, ConditionId: null, OrganismName: "Bacteria"),
        ],
        MasterValues: [],
        ChildValues:
        [
            new ChildValueEntry(200, VocabularyCategory.Organism, "SAL", MasterId: 9),
        ]));

    [Fact]
    public void MasterLoincResolvesToItsConditionAndOrganism()
    {
        var match = Matcher().MatchLoinc("11585-7", labId: 999);

        Assert.True(match.Matched);
        Assert.Equal(42, match.ConditionId);
        Assert.Equal(7, match.OrganismId);
    }

    [Fact]
    public void MasterLoincWithoutAnOrganismResolvesTheConditionAlone()
    {
        var match = Matcher().MatchLoinc("99999-9", labId: 999);

        Assert.True(match.Matched);
        Assert.Equal(50, match.ConditionId);
        Assert.Null(match.OrganismId);
    }

    [Fact]
    public void SnomedResolvesToItsOrganismAndCondition()
    {
        var match = Matcher().MatchOrganism("260373001");

        Assert.True(match.Matched);
        Assert.Equal(7, match.OrganismId);
        Assert.Equal(42, match.ConditionId);
    }

    [Fact]
    public void LabSpecificChildMappingTakesPrecedenceOverTheMaster()
    {
        var matcher = Matcher();

        // The same LOINC, read directly as a master for a lab with no child
        // mapping, resolves to the master's own condition and organism.
        var byMaster = matcher.MatchLoinc("11585-7", labId: 999);
        Assert.Equal(42, byMaster.ConditionId);
        Assert.Equal(7, byMaster.OrganismId);

        // For lab 200 the child mapping wins: the code resolves through it to
        // master 43304-5 rather than by the default master reading.
        var byChild = matcher.MatchLoinc("11585-7", labId: 200);
        Assert.True(byChild.Matched);
        Assert.Equal(60, byChild.ConditionId);
        Assert.Equal(8, byChild.OrganismId);
    }

    [Fact]
    public void AnUnmatchedLoincYieldsAnUnmatchedResult()
    {
        var match = Matcher().MatchLoinc("00000-0", labId: 999);

        Assert.False(match.Matched);
        Assert.Null(match.ConditionId);
        Assert.Null(match.OrganismId);
    }

    [Fact]
    public void AnUnmatchedSnomedYieldsAnUnmatchedResult()
    {
        var match = Matcher().MatchOrganism("00000000");

        Assert.False(match.Matched);
        Assert.Null(match.ConditionId);
        Assert.Null(match.OrganismId);
    }

    [Theory]
    [InlineData("ct-ag")]
    [InlineData(" CT-AG ")]
    public void ALabsChildLoincIsMatchedWithoutRegardToCase(string code)
    {
        var matcher = new VocabularyMatcher(new Vocabulary(
            MasterLoincs: [new MasterLoincEntry("11585-7", ConditionId: 42, OrganismId: null, OrganismFromResult: false)],
            ChildLoincs: [new ChildLoincEntry("CT-AG", LabId: 200, MasterLoinc: "11585-7")],
            MasterOrganisms: [],
            MasterValues: [],
            ChildValues: []));

        var match = matcher.MatchLoinc(code, labId: 200);

        Assert.True(match.Matched);
        Assert.Equal("11585-7", match.MasterLoinc);
    }

    [Fact]
    public void MatchLoincCarriesTheMasterLoincItResolvedThrough()
    {
        Assert.Equal("43304-5", Matcher().MatchLoinc("11585-7", labId: 200).MasterLoinc);
        Assert.Equal("11585-7", Matcher().MatchLoinc("11585-7", labId: 999).MasterLoinc);
    }

    [Fact]
    public void MatchOrganismReadsTheAlternateSnomed()
    {
        var match = Matcher().MatchOrganism("115412001");

        Assert.True(match.Matched);
        Assert.Equal(9, match.OrganismId);
        Assert.Equal(70, match.ConditionId);
        Assert.Equal("Salmonella", match.OrganismName);
    }

    [Fact]
    public void MatchResultReadsAnOrdinaryLoincAsMatchLoincDoes()
    {
        var match = OrganismMatcher().MatchResult("11585-7", "27268008", labId: 999);

        Assert.True(match.Matched);
        Assert.Equal(42, match.ConditionId);
        Assert.Equal(7, match.OrganismId);
        Assert.Equal("Bordetella", match.OrganismName);
        Assert.Equal("11585-7", match.MasterLoinc);
    }

    [Theory]
    [InlineData("27268008")]
    [InlineData("115412001")]
    public void MatchResultTakesTheOrganismFromTheResultsSnomed(string snomed)
    {
        var match = OrganismMatcher().MatchResult("625-4", snomed, labId: 999);

        Assert.True(match.Matched);
        Assert.Equal(9, match.OrganismId);
        Assert.Equal(70, match.ConditionId);
        Assert.Equal("Salmonella", match.OrganismName);
        Assert.Equal("625-4", match.MasterLoinc);
    }

    [Fact]
    public void MatchResultReadsTheLabsChildOrganismCodeBeforeTheSnomed()
    {
        var match = OrganismMatcher().MatchResult("625-4", "sal", labId: 200);

        Assert.Equal(9, match.OrganismId);
        Assert.Equal(70, match.ConditionId);

        // Another lab has no such child code, and SAL is no SNOMED.
        var otherLab = OrganismMatcher().MatchResult("625-4", "SAL", labId: 999);
        Assert.True(otherLab.Matched);
        Assert.Null(otherLab.OrganismId);
        Assert.Null(otherLab.ConditionId);
    }

    [Fact]
    public void MatchResultFallsBackToTheMastersConditionForAnOrganismThatNamesNone()
    {
        var match = OrganismMatcher().MatchResult("625-5", "409822003", labId: 999);

        Assert.Equal(10, match.OrganismId);
        Assert.Equal(80, match.ConditionId);
    }

    [Fact]
    public void MatchResultIsUnmatchedForAnUnknownLoinc()
    {
        Assert.Same(VocabularyMatch.Unmatched, OrganismMatcher().MatchResult("00000-0", "27268008", labId: 999));
    }

    [Theory]
    [InlineData(VocabularyCategory.TestResult, "DET", 200, "Positive")]
    [InlineData(VocabularyCategory.TestResult, " det ", 200, "Positive")]
    [InlineData(VocabularyCategory.Specimen, "BLD", 200, "Blood")]
    public void MatchValueTranslatesALabsCodeToItsApplicationValue(
        string category, string code, int labId, string expected)
    {
        Assert.Equal(expected, Matcher().MatchValue(category, code, labId));
    }

    [Theory]
    [InlineData(VocabularyCategory.TestResult, "DET", 999)] // another lab's code
    [InlineData(VocabularyCategory.Specimen, "DET", 200)] // the wrong category
    [InlineData(VocabularyCategory.TestResult, "UNK", 200)] // a master with no application value
    [InlineData(VocabularyCategory.TestResult, "WRONG", 200)] // a master of another category
    [InlineData(VocabularyCategory.TestResult, "NOPE", 200)] // no child code at all
    [InlineData(VocabularyCategory.TestResult, null, 200)]
    public void MatchValueIsNullWhereATierIsMissing(string category, string? code, int labId)
    {
        Assert.Null(Matcher().MatchValue(category, code, labId));
    }
}
