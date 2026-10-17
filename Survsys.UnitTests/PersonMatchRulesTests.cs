using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The person matching over small in-memory candidates: a score at the ceiling
/// is Exact and one below the floor is None, and in the fuzzy band between them
/// a shared medical record number (found case-insensitively) settles it as
/// Exact, a missing date of birth as None, and anything else as Fuzzy.
/// </summary>
public class PersonMatchRulesTests
{
    private static readonly DateOnly BirthDate = new(1990, 1, 15);

    [Fact]
    public void ScoreAtOrAboveAutoMatchThresholdIsExact()
    {
        var candidate = new PersonMatchCandidate(
            PersonId: 1, Score: PersonMatchRules.AutoMatchScore, DateOfBirth: null, MedicalRecordNumbers: []);

        Assert.Equal(PersonMatchKind.Exact, PersonMatchRules.Classify(candidate, []));
    }

    [Fact]
    public void ScoreBelowNoMatchThresholdIsNone()
    {
        var candidate = new PersonMatchCandidate(
            PersonId: 1,
            Score: PersonMatchRules.NoMatchScore - 0.1,
            DateOfBirth: BirthDate,
            MedicalRecordNumbers: ["MRN-1"]);

        Assert.Equal(PersonMatchKind.None, PersonMatchRules.Classify(candidate, ["MRN-1"]));
    }

    [Fact]
    public void FuzzyBandWithSharedMedicalRecordNumberIsExactAndCaseInsensitive()
    {
        var candidate = new PersonMatchCandidate(
            PersonId: 1, Score: 80.0, DateOfBirth: BirthDate, MedicalRecordNumbers: ["mrn-abc"]);

        Assert.Equal(PersonMatchKind.Exact, PersonMatchRules.Classify(candidate, ["MRN-ABC"]));
    }

    [Fact]
    public void FuzzyBandWithNoSharedMedicalRecordNumberAndNoDateOfBirthIsNone()
    {
        var candidate = new PersonMatchCandidate(
            PersonId: 1, Score: 80.0, DateOfBirth: null, MedicalRecordNumbers: ["MRN-1"]);

        Assert.Equal(PersonMatchKind.None, PersonMatchRules.Classify(candidate, ["MRN-2"]));
    }

    [Fact]
    public void FuzzyBandWithDateOfBirthAndNoSharedMedicalRecordNumberIsFuzzy()
    {
        var candidate = new PersonMatchCandidate(
            PersonId: 1, Score: 80.0, DateOfBirth: BirthDate, MedicalRecordNumbers: ["MRN-1"]);

        Assert.Equal(PersonMatchKind.Fuzzy, PersonMatchRules.Classify(candidate, ["MRN-2"]));
    }
}
