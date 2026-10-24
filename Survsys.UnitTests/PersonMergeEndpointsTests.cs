using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The merge slice's pure parts: which requests it can answer, which two
/// people can be merged, and how people on file are paired as duplicates.
/// </summary>
public class PersonMergeEndpointsTests
{
    private static readonly DateOnly BirthDate = new(1985, 6, 15);

    [Fact]
    public void WhyNotAMergeRequestAcceptsTwoIds()
    {
        Assert.Null(PersonMergeEndpoints.WhyNotAMergeRequest(new PersonMergeRequest(1, 2)));
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(-1, 2)]
    public void WhyNotAMergeRequestRefusesAMissingId(int keepId, int mergeId)
    {
        Assert.NotNull(PersonMergeEndpoints.WhyNotAMergeRequest(new PersonMergeRequest(keepId, mergeId)));
    }

    [Fact]
    public void WhyNotMergeableAcceptsTwoLivePeople()
    {
        Assert.Null(PersonMergeEndpoints.WhyNotMergeable(Person(1, "Rodriguez"), Person(2, "Rodriguez")));
    }

    [Fact]
    public void WhyNotMergeableRefusesTheSamePerson()
    {
        var person = Person(1, "Rodriguez");

        Assert.NotNull(PersonMergeEndpoints.WhyNotMergeable(person, person));
    }

    [Fact]
    public void WhyNotMergeableRefusesATombstoneEitherSide()
    {
        var live = Person(1, "Rodriguez");
        var tombstone = Person(2, "Rodriguez");
        tombstone.MergedIntoPersonId = 3;

        Assert.NotNull(PersonMergeEndpoints.WhyNotMergeable(live, tombstone));
        Assert.NotNull(PersonMergeEndpoints.WhyNotMergeable(tombstone, live));
    }

    [Fact]
    public void PairsKeepsMatchesBestFirstAndLeavesOthersOut()
    {
        var first = Person(1, "Rodriguez", "Maria", BirthDate);
        var second = Person(2, "rodriguez", "Maria", BirthDate);
        var exact = Person(3, "Nguyen", "An", BirthDate.AddYears(1), "MRN-7");
        var exactTwin = Person(4, "Nguyen", "An", BirthDate.AddYears(1), "MRN-7");
        var namesake = Person(5, "Rodriguez", "Carlos", new DateOnly(1950, 1, 1));

        var pairs = PersonMergeEndpoints.Pairs([namesake, second, first, exactTwin, exact], new PersonMatchThresholds());

        Assert.Collection(
            pairs,
            pair =>
            {
                Assert.Equal((3, 4), (pair.First.Id, pair.Second.Id));
                Assert.Equal(PersonMatchCandidateResponse.Exact, pair.Verdict);
            },
            pair =>
            {
                Assert.Equal((1, 2), (pair.First.Id, pair.Second.Id));
                Assert.Equal(PersonMatchCandidateResponse.Probable, pair.Verdict);
                Assert.NotEmpty(pair.Reasons);
            });
    }

    [Fact]
    public void PairsScoresAPairSharingSeveralKeysOnce()
    {
        var first = Person(1, "Rodriguez", "Maria", BirthDate, "MRN-1");
        var second = Person(2, "Rodriguez", "Maria", BirthDate, "MRN-1");

        var pair = Assert.Single(PersonMergeEndpoints.Pairs([first, second], new PersonMatchThresholds()));

        Assert.Equal(PersonMatchCandidateResponse.Exact, pair.Verdict);
    }

    private static Person Person(
        int id, string lastName, string? firstName = null, DateOnly? birthDate = null, string? identifier = null)
    {
        var person = new Person { Id = id, LastName = lastName, FirstName = firstName, BirthDate = birthDate, BirthGender = "F" };
        if (identifier is not null)
        {
            person.Identifiers.Add(new PersonIdentifier { PersonId = id, Value = identifier });
        }

        return person;
    }
}
