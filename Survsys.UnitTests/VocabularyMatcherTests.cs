using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The matcher read over a small in-memory vocabulary: a master LOINC to its
/// condition and organism, a SNOMED to its organism, a lab-specific child
/// LOINC that overrides the master reading, and codes the vocabulary does not
/// carry, which are unmatched rather than refused.
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
}
