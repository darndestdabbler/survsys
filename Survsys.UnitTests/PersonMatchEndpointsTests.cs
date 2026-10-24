using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The match slice's pure parts: which requests it can answer, how a request
/// and a person on file become subjects the rules compare, and how scored
/// candidates are ranked and named on the wire.
/// </summary>
public class PersonMatchEndpointsTests
{
    private static readonly DateOnly BirthDate = new(1985, 6, 15);

    [Theory]
    [InlineData("Rodriguez", false, false, false)]
    [InlineData(null, true, false, false)]
    [InlineData(" ", false, true, false)]
    [InlineData(null, false, false, true)]
    public void WhyNotAMatchRequestAcceptsAnythingToFindCandidatesBy(
        string? lastName, bool withBirthDate, bool withIdentifier, bool withPersonIds)
    {
        var request = new PersonMatchRequest(
            lastName,
            BirthDate: withBirthDate ? BirthDate : null,
            Identifiers: withIdentifier ? ["MRN-1"] : null,
            PersonIds: withPersonIds ? [1] : null);

        Assert.Null(PersonMatchEndpoints.WhyNotAMatchRequest(request));
    }

    [Fact]
    public void WhyNotAMatchRequestRefusesNothingToFindCandidatesBy()
    {
        var request = new PersonMatchRequest(" ", FirstName: "Maria", Identifiers: [" "]);

        Assert.NotNull(PersonMatchEndpoints.WhyNotAMatchRequest(request));
    }

    [Fact]
    public void WhyNotAMatchRequestRefusesTooManyPersonIds()
    {
        var ids = Enumerable.Range(1, PersonMatchEndpoints.MaxPersonIds + 1).ToList();

        Assert.NotNull(PersonMatchEndpoints.WhyNotAMatchRequest(new PersonMatchRequest(null, PersonIds: ids)));
    }

    [Fact]
    public void ToSubjectOfARequestKeysItsAddressAndDropsBlankIdentifiers()
    {
        var request = new PersonMatchRequest(
            "Rodriguez", "Maria", BirthDate, "F", [" MRN-1 ", ""], "1234 Main Street", "84101-0001");

        var subject = PersonMatchEndpoints.ToSubject(request);

        Assert.Equal("Rodriguez", subject.LastName);
        Assert.Equal(["MRN-1"], subject.Identifiers);
        Assert.Equal(["1234 main street_84101"], subject.Addresses);
    }

    [Fact]
    public void ToSubjectOfAPersonKeepsOnlyCurrentAddresses()
    {
        var subject = PersonMatchEndpoints.ToSubject(Seeded(11));

        Assert.Equal(["MRN-1"], subject.Identifiers);
        Assert.Equal(["1234 main street_84101"], subject.Addresses);
    }

    [Fact]
    public void ToRequestCarriesTheParsedPatient()
    {
        var request = PersonMatchEndpoints.ToRequest(new CreatePersonRequest("Rodriguez", "Maria", null, BirthDate, "F"));

        Assert.Equal(new PersonMatchRequest("Rodriguez", "Maria", BirthDate, "F"), request);
    }

    [Fact]
    public void RankScoresClassifiesAndOrdersBestFirst()
    {
        var exact = Seeded(11);
        var probable = Seeded(12);
        probable.Addresses.Clear();
        var none = Seeded(13);
        none.FirstName = "Ana";
        none.BirthDate = null;

        var request = new PersonMatchRequest(
            "Rodriguez", "Maria", BirthDate, "F", Street: "1234 Main Street", PostalCode: "84101");

        var ranked = PersonMatchEndpoints.Rank(request, [none, probable, exact], new PersonMatchThresholds());

        Assert.Equal([11, 12, 13], ranked.Select(candidate => candidate.Person.Id));
        Assert.Equal([100.0, 85.0, 50.0], ranked.Select(candidate => candidate.Score));
        Assert.Equal(
            [PersonMatchCandidateResponse.Exact, PersonMatchCandidateResponse.Probable, PersonMatchCandidateResponse.None],
            ranked.Select(candidate => candidate.Verdict));
        Assert.Contains("Address matches", ranked[0].Reasons);
    }

    [Fact]
    public void RankPromotesAProbableMatchOnASharedIdentifier()
    {
        var person = Seeded(11);
        person.Addresses.Clear();

        var ranked = PersonMatchEndpoints.Rank(
            new PersonMatchRequest("Rodriguez", "Maria", BirthDate, "F", ["mrn-1"]), [person], new PersonMatchThresholds());

        Assert.Equal(PersonMatchCandidateResponse.Exact, ranked[0].Verdict);
        Assert.Contains("Shares identifier MRN-1", ranked[0].Reasons);
    }

    [Fact]
    public void RankUsesTheThresholdsItIsGiven()
    {
        var person = Seeded(11);
        person.Addresses.Clear();

        var ranked = PersonMatchEndpoints.Rank(
            new PersonMatchRequest("Rodriguez", "Maria", BirthDate, "F"),
            [person],
            new PersonMatchThresholds { Exact = 85.0 });

        Assert.Equal(PersonMatchCandidateResponse.Exact, ranked[0].Verdict);
    }

    [Theory]
    [InlineData(PersonMatchKind.Exact, PersonMatchCandidateResponse.Exact)]
    [InlineData(PersonMatchKind.Fuzzy, PersonMatchCandidateResponse.Probable)]
    [InlineData(PersonMatchKind.None, PersonMatchCandidateResponse.None)]
    public void VerdictNamesEachClassification(PersonMatchKind kind, string expected)
    {
        Assert.Equal(expected, PersonMatchEndpoints.Verdict(kind));
    }

    private static Person Seeded(int id) => new()
    {
        Id = id,
        LastName = "Rodriguez",
        FirstName = "Maria",
        BirthDate = BirthDate,
        BirthGender = "F",
        Identifiers = { new PersonIdentifier { PersonId = id, Value = "MRN-1" } },
        Addresses =
        {
            new PersonAddress { PersonId = id, Street = "1234 Main Street", PostalCode = "84101" },
            new PersonAddress
            {
                PersonId = id, Street = "9 Old Road", PostalCode = "84102", Ending = new DateOnly(2020, 1, 1),
            },
        },
    };
}
