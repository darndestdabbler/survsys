using Survsys.Api;
using Survsys.Contracts;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The pure parts of turning an eICR into cases: whom the matcher's
/// candidates say its patient is, the match request made of the patient, and
/// how a written value is recorded and found edited.
/// </summary>
/// <remarks>
/// Linking, opening and writing cases are <c>Survsys.IntegrationTests</c>'s to
/// prove against a real SQL Server.
/// </remarks>
public class EcrCaseWriterTests
{
    [Fact]
    public void DecideMakesANewPersonWhereNoCandidateIsFound()
    {
        var decision = EcrCaseWriter.Decide([Candidate(3, PersonMatchCandidateResponse.None)]);

        Assert.True(decision.NewPerson);
        Assert.Null(decision.PersonId);
        Assert.Null(decision.Review);
    }

    [Fact]
    public void DecideTakesTheOneExactCandidate()
    {
        var decision = EcrCaseWriter.Decide(
            [Candidate(3, PersonMatchCandidateResponse.Exact), Candidate(4, PersonMatchCandidateResponse.None)]);

        Assert.Equal(3, decision.PersonId);
        Assert.False(decision.NewPerson);
        Assert.Null(decision.Review);
    }

    [Theory]
    [InlineData(PersonMatchCandidateResponse.Exact, PersonMatchCandidateResponse.Exact)]
    [InlineData(PersonMatchCandidateResponse.Exact, PersonMatchCandidateResponse.Probable)]
    [InlineData(PersonMatchCandidateResponse.Probable, PersonMatchCandidateResponse.None)]
    public void DecideLeavesAnyOtherMatchToAReviewer(string first, string second)
    {
        var decision = EcrCaseWriter.Decide([Candidate(3, first), Candidate(4, second)]);

        Assert.Null(decision.PersonId);
        Assert.False(decision.NewPerson);
        Assert.False(string.IsNullOrWhiteSpace(decision.Review));
    }

    [Fact]
    public void MatchRequestCarriesThePatientsNameBirthDateAndAddress()
    {
        var patient = new CdaParser().Read(CdaParserTests.ReadFixture(CdaParserTests.EicrR311)).Patient;

        var request = EcrCaseWriter.MatchRequest(patient);

        Assert.Equal(patient.Person.LastName, request.LastName);
        Assert.Equal(patient.Person.FirstName, request.FirstName);
        Assert.Equal(patient.Person.BirthDate, request.BirthDate);
        Assert.Equal(patient.Street, request.Street);
        Assert.Equal(patient.Zip, request.PostalCode);
    }

    [Fact]
    public void TextJoinsTheFieldsInvariantlyWithNullsAsNothing()
    {
        Assert.Equal(
            "Lyme|2026-10-02||3.5|True",
            EcrCaseWriter.Text("Lyme", new DateOnly(2026, 10, 2), null, 3.5m, true));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "Yes", true)]
    [InlineData("Yes", "Yes", true)]
    [InlineData("No", "Yes", false)]
    [InlineData("Yes", null, false)]
    public void MayWriteOnlyOverABlankOrAnUneditedValue(string? current, string? written, bool expected)
    {
        Assert.Equal(expected, EcrCaseWriter.MayWrite(current, written));
    }

    private static EcrMatchCandidate Candidate(int personId, string verdict) => new(personId, verdict);
}
