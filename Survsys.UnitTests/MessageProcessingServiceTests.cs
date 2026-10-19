using Survsys.Api;
using Survsys.Data;
using Survsys.Messaging;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Where automatic assignment sends a queued message: every outcome of
/// <see cref="MessageProcessingService.Decide"/>, over a message in memory and
/// the people its patient was scored against. The passes that read and write
/// the database are the integration suite's.
/// </summary>
public class MessageProcessingServiceTests
{
    private static StagedMessage Message(bool? allowNewCase = true, string? state = "CT", int? agencyId = 1) => new()
    {
        ReviewStatus = MessageReviewStatusRules.EntryCode,
        ConditionId = 42,
        AllowNewCase = allowNewCase,
        AgencyId = agencyId,
        PatientLastName = "Rodriguez",
        PatientFirstName = "Maria",
        PatientState = state,
        PatientZip = "06101",
    };

    [Fact]
    public void DecideAssignsTheOneExactMatch()
    {
        var decision = MessageProcessingService.Decide(
            Message(),
            [new AssignmentCandidate(7, PersonMatchKind.Exact, HasOpenCase: false)],
            "CT");

        Assert.Equal(MessageReviewStatusRules.AssignedCode, decision.Status);
        Assert.Equal(7, decision.PersonId);
    }

    /// <summary>
    /// A probable match beside the one exact match does not shake it: the
    /// exact match is the same person.
    /// </summary>
    [Fact]
    public void DecideAssignsTheOneExactMatchBesideAProbableOne()
    {
        var decision = MessageProcessingService.Decide(
            Message(),
            [
                new AssignmentCandidate(7, PersonMatchKind.Exact, HasOpenCase: false),
                new AssignmentCandidate(8, PersonMatchKind.Fuzzy, HasOpenCase: false),
            ],
            "CT");

        Assert.Equal(MessageReviewStatusRules.AssignedCode, decision.Status);
        Assert.Equal(7, decision.PersonId);
    }

    [Fact]
    public void DecideAssignsANewPersonWhenNoOneMatches()
    {
        var decision = MessageProcessingService.Decide(Message(), [], "CT");

        Assert.Equal(MessageReviewStatusRules.AssignedCode, decision.Status);
        Assert.Null(decision.PersonId);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public void DecideSendsAPatientWithNoNameAndNoMatchToPending()
    {
        var message = Message();
        message.PatientLastName = null;

        var decision = MessageProcessingService.Decide(message, [], "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
        Assert.NotNull(decision.Reason);
    }

    [Fact]
    public void DecideSendsAnExactMatchWithAnOpenCaseToPending()
    {
        var decision = MessageProcessingService.Decide(
            Message(),
            [new AssignmentCandidate(7, PersonMatchKind.Exact, HasOpenCase: true)],
            "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
        Assert.Contains("open case", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DecideSendsSeveralExactMatchesToPending()
    {
        var decision = MessageProcessingService.Decide(
            Message(),
            [
                new AssignmentCandidate(7, PersonMatchKind.Exact, HasOpenCase: false),
                new AssignmentCandidate(8, PersonMatchKind.Exact, HasOpenCase: false),
            ],
            "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
        Assert.Null(decision.PersonId);
    }

    [Fact]
    public void DecideSendsOnlyProbableMatchesToPending()
    {
        var decision = MessageProcessingService.Decide(
            Message(),
            [new AssignmentCandidate(8, PersonMatchKind.Fuzzy, HasOpenCase: false)],
            "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
    }

    [Fact]
    public void DecideSendsAMessageWithNoCaseManagementDecisionToPending()
    {
        var decision = MessageProcessingService.Decide(Message(allowNewCase: null), [], "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
    }

    [Fact]
    public void DecideSendsAMessageWithNoConditionToPending()
    {
        var message = Message();
        message.ConditionId = null;

        var decision = MessageProcessingService.Decide(message, [], "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
    }

    /// <summary>
    /// A rule that opens no case graylists the message, ahead of the
    /// out-of-state and agency checks, as the legacy chain orders them.
    /// </summary>
    [Fact]
    public void DecideGraylistsAMessageItsRuleOpensNoCaseFor()
    {
        var decision = MessageProcessingService.Decide(Message(allowNewCase: false, state: "NY", agencyId: null), [], "CT");

        Assert.Equal(MessageReviewStatusRules.GrayCode, decision.Status);
    }

    [Theory]
    [InlineData("NY")]
    [InlineData(" ma ")]
    public void DecideSendsAPatientOutsideTheHomeStateOutOfState(string state)
    {
        var decision = MessageProcessingService.Decide(
            Message(state: state),
            [new AssignmentCandidate(7, PersonMatchKind.Exact, HasOpenCase: false)],
            "CT");

        Assert.Equal(MessageReviewStatusRules.OutOfStateCode, decision.Status);
    }

    /// <summary>
    /// The home state compares without regard to case or padding, and an
    /// empty home state or patient state sends no one out of state.
    /// </summary>
    [Theory]
    [InlineData("ct", "CT")]
    [InlineData("NY", "")]
    [InlineData("NY", null)]
    [InlineData(null, "CT")]
    public void DecideKeepsAPatientInStateOrWhereEitherStateIsUnknown(string? state, string? homeState)
    {
        var decision = MessageProcessingService.Decide(Message(state: state), [], homeState);

        Assert.Equal(MessageReviewStatusRules.AssignedCode, decision.Status);
    }

    [Fact]
    public void DecideSendsAMessageWithNoAgencyToPending()
    {
        var decision = MessageProcessingService.Decide(Message(agencyId: null), [], "CT");

        Assert.Equal(MessageReviewStatusRules.PendingCode, decision.Status);
        Assert.Contains("agency", decision.Reason, StringComparison.Ordinal);
    }
}
