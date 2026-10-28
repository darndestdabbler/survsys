using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The person matching over small in-memory candidates: a score at the ceiling
/// is Exact and one below the floor is None, and in the fuzzy band between them
/// a shared medical record number (found case-insensitively) settles it as
/// Exact, a missing date of birth as None, and anything else as Fuzzy; the
/// thresholds can be moved; and the score sums the weights of the fields both
/// sides hold and agree on, saying why.
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

    [Fact]
    public void ClassifyUsesTheThresholdsItIsGiven()
    {
        var thresholds = new PersonMatchThresholds { Exact = 80.0, Probable = 50.0 };
        var probable = new PersonMatchCandidate(1, 60.0, BirthDate, []);
        var exact = new PersonMatchCandidate(2, 80.0, BirthDate, []);
        var none = new PersonMatchCandidate(3, 49.9, BirthDate, []);

        Assert.Equal(PersonMatchKind.Fuzzy, PersonMatchRules.Classify(probable, [], thresholds));
        Assert.Equal(PersonMatchKind.Exact, PersonMatchRules.Classify(exact, [], thresholds));
        Assert.Equal(PersonMatchKind.None, PersonMatchRules.Classify(none, [], thresholds));
    }

    [Fact]
    public void ScoreOfEveryFieldAgreeingIsOneHundred()
    {
        var incoming = Subject("  smith ", "JANE", BirthDate, "f", ["MRN-1"], ["1 main st_06101"]);
        var candidate = Subject("Smith", "Jane", BirthDate, "F", ["mrn-1"], ["1 main st_06101"]);

        var result = PersonMatchRules.Score(incoming, candidate);

        Assert.Equal(100.0, result.Score);
        Assert.Equal(
            ["Last name matches", "First name matches", "Birth date matches", "Sex at birth matches",
                "Address matches", "Shares identifier mrn-1"],
            result.Reasons);
    }

    [Fact]
    public void ScoreCountsDifferingFieldsAsReasonsAndSkipsFieldsOneSideLacks()
    {
        var incoming = Subject("Smith", "Jane", BirthDate, null, [], []);
        var candidate = Subject("Smith", "Joan", new DateOnly(1991, 1, 15), "F", [], ["1 main st_06101"]);

        var result = PersonMatchRules.Score(incoming, candidate);

        Assert.Equal(PersonMatchRules.LastNameWeight, result.Score);
        Assert.Equal(["Last name matches", "First name differs", "Birth date differs"], result.Reasons);
    }

    [Fact]
    public void ScoreSaysWhenBothSidesHoldAddressesAndIdentifiersButShareNone()
    {
        var incoming = Subject("Smith", "Jane", BirthDate, "F", ["MRN-1"], ["1 main st_06101"]);
        var candidate = Subject("Smith", "Jane", BirthDate, "F", ["MRN-2"], ["9 oak st_06101"]);

        var result = PersonMatchRules.Score(incoming, candidate);

        Assert.Equal(85.0, result.Score);
        Assert.Equal(
            ["Last name matches", "First name matches", "Birth date matches", "Sex at birth matches",
                "Address differs", "Shares no identifier"],
            result.Reasons);
    }

    [Theory]
    [InlineData("  1  Main   St ", "06101-1234", "1 main st_06101")]
    [InlineData("1 Main St", "061011234", "1 main st_06101")]
    [InlineData("1 Main St", "0610", null)]
    [InlineData("1 Main St", null, null)]
    [InlineData(" ", "06101", null)]
    public void AddressKeyIsTheCollapsedStreetAndFiveDigitZip(string? street, string? zip, string? expected)
    {
        Assert.Equal(expected, PersonMatchRules.AddressKey(street, zip));
    }

    private static PersonMatchSubject Subject(
        string? lastName, string? firstName, DateOnly? birthDate, string? birthGender,
        string[] identifiers, string[] addresses) =>
        new(lastName, firstName, birthDate, birthGender, identifiers, addresses);
}
