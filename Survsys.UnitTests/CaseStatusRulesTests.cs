using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The case status vocabulary: what each legacy code reads back to, that an
/// unclassified case is Unknown, and what the rule refuses to read or to be
/// set to.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database
/// or a host. The codes are EpiTrax's own, as
/// <c>../nedss/docs/business-logic/CLASSIFICATION_RULES.md</c> section 1.2
/// lists them — <c>C</c> Confirmed, <c>P</c> Probable, <c>S</c> Suspected,
/// <c>NC</c> Not a Case — with Unknown the empty or absent value. The two
/// legacy codes this installation does not use, <c>D</c> Discarded and
/// <c>OS</c> Out of State, are among the values the rule refuses, because they
/// are not among the five the session plan names.
/// </remarks>
public class CaseStatusRulesTests
{
    [Theory]
    [InlineData(CaseStatusRules.ConfirmedCode, CaseStatus.Confirmed)]
    [InlineData(CaseStatusRules.ProbableCode, CaseStatus.Probable)]
    [InlineData(CaseStatusRules.SuspectedCode, CaseStatus.Suspected)]
    [InlineData(CaseStatusRules.NotACaseCode, CaseStatus.NotACase)]
    public void ReadNamesTheStatusEachCodeIsStoredUnder(string code, CaseStatus expected)
    {
        Assert.Equal(expected, CaseStatusRules.Read(code));
    }

    /// <summary>
    /// The unclassified case. Empty, whitespace and absent all read as Unknown,
    /// because the legacy leaves an unclassified case's status empty rather
    /// than giving it a code of its own.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadIsUnknownForAnEmptyOrAbsentCode(string? storedCode)
    {
        Assert.Equal(CaseStatus.Unknown, CaseStatusRules.Read(storedCode));
    }

    /// <summary>
    /// Surrounding whitespace is trimmed before a non-empty code is matched,
    /// as the legacy data can carry a padded code.
    /// </summary>
    [Theory]
    [InlineData(" C ", CaseStatus.Confirmed)]
    [InlineData("\tNC\n", CaseStatus.NotACase)]
    public void ReadTrimsSurroundingWhitespace(string code, CaseStatus expected)
    {
        Assert.Equal(expected, CaseStatusRules.Read(code));
    }

    /// <summary>
    /// Each shape the reader refuses: a legacy code this installation does not
    /// use, a code in the wrong case, and a value that is not a code at all.
    /// The message names the value, because the row holding it is what someone
    /// will have to go and look at.
    /// </summary>
    [Theory]
    [InlineData("D")]
    [InlineData("OS")]
    [InlineData("c")]
    [InlineData("nc")]
    [InlineData("Confirmed")]
    [InlineData("X")]
    public void ReadRefusesACodeTheVocabularyDoesNotKnow(string storedCode)
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => CaseStatusRules.Read(storedCode));

        Assert.Contains(storedCode, refused.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// What a report's status may be set to: one of the four codes, or empty
    /// to clear it to Unknown.
    /// </summary>
    [Theory]
    [InlineData(CaseStatusRules.ConfirmedCode)]
    [InlineData(CaseStatusRules.ProbableCode)]
    [InlineData(CaseStatusRules.SuspectedCode)]
    [InlineData(CaseStatusRules.NotACaseCode)]
    [InlineData(" C ")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsAssignableAcceptsTheFourCodesAndAnEmptyCode(string? offeredCode)
    {
        Assert.True(CaseStatusRules.IsAssignable(offeredCode));
    }

    /// <summary>
    /// What a report's status may not be set to: the legacy codes this
    /// installation does not use, a code in the wrong case, and anything that
    /// is not a code. The endpoint turns a false here into a 400 rather than
    /// writing the value.
    /// </summary>
    [Theory]
    [InlineData("D")]
    [InlineData("OS")]
    [InlineData("c")]
    [InlineData("nc")]
    [InlineData("Confirmed")]
    [InlineData("X")]
    public void IsAssignableRefusesACodeTheVocabularyDoesNotKnow(string offeredCode)
    {
        Assert.False(CaseStatusRules.IsAssignable(offeredCode));
    }
}
