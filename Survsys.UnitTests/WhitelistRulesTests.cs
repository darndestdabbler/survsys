using Survsys.Api;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which of a person's open cases a message updates: every rule type of
/// <see cref="WhitelistRules.Match"/>, the Not a Case pruning, and the rule
/// <see cref="WhitelistRules.For"/> chooses for a condition.
/// </summary>
public class WhitelistRulesTests
{
    private static readonly DateOnly Collected = new(2026, 6, 1);

    private static WhitelistCase Case(int id, int daysBefore, bool notACase = false) =>
        new(id, Collected.AddDays(-daysBefore), notACase);

    private static IEnumerable<int> Ids(WhitelistMatch match) => match.Matched.Select(report => report.Id);

    [Fact]
    public void TimeOnsetMatchesACaseWhoseEventDatePlusTheDaysIsAfterTheMessage()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.TimeOnset, Days = 90 },
            Collected,
            [Case(1, 89), Case(2, 90)]);

        Assert.Equal([1], Ids(match));
        Assert.Null(match.Exception);
    }

    [Fact]
    public void BeforeAfterMatchesAMessageWithinTheWindowAroundTheEventDate()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.BeforeAfter, DaysBefore = 10, DaysAfter = 30 },
            Collected,
            [Case(1, 30), Case(2, 31), Case(3, -10), Case(4, -11)]);

        Assert.Equal([3, 1], Ids(match));
    }

    [Fact]
    public void NewestKeepsTheLatestMatch()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.Newest },
            Collected,
            [Case(1, 400), Case(2, 5), Case(3, 100)]);

        Assert.Equal([2], Ids(match));
    }

    [Fact]
    public void NeverNewMatchesEveryCase()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.NeverNew }, Collected, [Case(1, 400), Case(2, 5)]);

        Assert.Equal(2, match.Matched.Count);
    }

    [Fact]
    public void AlwaysNewMatchesNothing()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.AlwaysNew }, Collected, [Case(1, 1)]);

        Assert.Empty(match.Matched);
        Assert.Null(match.Exception);
    }

    [Fact]
    public void AnExceptionRuleIsAReviewers()
    {
        var match = WhitelistRules.Match(
            new WhitelistRule { Type = WhitelistRuleType.Exception }, Collected, [Case(1, 1)]);

        Assert.NotNull(match.Exception);
    }

    [Fact]
    public void AMessageWithNoDateIsAReviewers()
    {
        var match = WhitelistRules.Match(new WhitelistRule(), null, [Case(1, 1)]);

        Assert.Contains("collection date", match.Exception, StringComparison.Ordinal);
    }

    [Fact]
    public void NotACaseIsPrunedBesideAnotherMatch()
    {
        var match = WhitelistRules.Match(new WhitelistRule(), Collected, [Case(1, 5, notACase: true), Case(2, 10)]);

        Assert.Equal([2], Ids(match));
    }

    [Fact]
    public void NotACaseIsKeptWhenItIsTheOnlyKindMatchedOrTheRuleIgnoresCaseStatus()
    {
        Assert.Equal(
            2,
            WhitelistRules.Match(
                new WhitelistRule(), Collected, [Case(1, 5, notACase: true), Case(2, 10, notACase: true)]).Matched.Count);
        Assert.Equal(
            2,
            WhitelistRules.Match(
                new WhitelistRule { IgnoreCaseStatus = true }, Collected, [Case(1, 5, notACase: true), Case(2, 10)]).Matched.Count);
    }

    [Fact]
    public void ForReadsAConditionsOwnRuleByNameElseTheDefault()
    {
        var own = new WhitelistRule { Type = WhitelistRuleType.Newest };
        var options = new MessageProcessingOptions { WhitelistByCondition = { ["Salmonellosis"] = own } };

        Assert.Same(own, WhitelistRules.For(options, "salmonellosis"));
        Assert.Same(options.Whitelist, WhitelistRules.For(options, "Pertussis"));
        Assert.Equal(WhitelistRuleType.TimeOnset, options.Whitelist.Type);
    }
}
