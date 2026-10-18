using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// The workflow state machine: what each code reads back to, that a report with
/// no code is at the assigned-to-LHD default, what the rule refuses to read or
/// to parse, that a state round-trips through its code, and which moves the
/// transition table allows and refuses.
/// </summary>
/// <remarks>
/// Pure throughout, so every case here is the rule itself and not a database or
/// a host. The graph is the legacy one: the forward chain
/// <c>assigned_to_lhd</c> to <c>accepted</c> to <c>under_investigation</c> to
/// <c>closed</c>, and around it the route, assignment, decline, reopen and
/// state-review transitions, with every (state, transition) pair the table does
/// not name refused.
/// </remarks>
public class WorkflowRulesTests
{
    [Theory]
    [InlineData(WorkflowRules.AssignedToLhdCode, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowRules.AcceptedCode, WorkflowState.Accepted)]
    [InlineData(WorkflowRules.UnderInvestigationCode, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowRules.ClosedCode, WorkflowState.Closed)]
    [InlineData(WorkflowRules.NotInvestigatedCode, WorkflowState.NotInvestigated)]
    [InlineData(WorkflowRules.ApprovedByStateCode, WorkflowState.ApprovedByState)]
    [InlineData(WorkflowRules.RejectedByStateCode, WorkflowState.RejectedByState)]
    public void ReadNamesTheStateEachCodeIsStoredUnder(string code, WorkflowState expected)
    {
        Assert.Equal(expected, WorkflowRules.Read(code));
    }

    /// <summary>
    /// A report with no code is at the assigned-to-LHD default. Empty,
    /// whitespace and absent all read as it, because the legacy opens a case at
    /// the Local Health Department and a report written before this column
    /// existed carries no code.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ReadIsAssignedToLhdForAnEmptyOrAbsentCode(string? storedCode)
    {
        Assert.Equal(WorkflowState.AssignedToLhd, WorkflowRules.Read(storedCode));
    }

    /// <summary>
    /// Surrounding whitespace is trimmed before a non-empty code is matched, as
    /// the legacy data can carry a padded code.
    /// </summary>
    [Theory]
    [InlineData(" accepted ", WorkflowState.Accepted)]
    [InlineData("\tunder_investigation\n", WorkflowState.UnderInvestigation)]
    public void ReadTrimsSurroundingWhitespace(string code, WorkflowState expected)
    {
        Assert.Equal(expected, WorkflowRules.Read(code));
    }

    /// <summary>
    /// Each shape the reader refuses: a legacy state this chain does not model,
    /// a code in the wrong case, and a value that is not a code at all. The
    /// message names the value, because the row holding it is what someone will
    /// have to go and look at.
    /// </summary>
    [Theory]
    [InlineData("approved_by_lhd")]
    [InlineData("reopened")]
    [InlineData("Closed")]
    [InlineData("ACCEPTED")]
    [InlineData("done")]
    public void ReadRefusesACodeTheVocabularyDoesNotKnow(string storedCode)
    {
        var refused = Assert.Throws<InvalidOperationException>(
            () => WorkflowRules.Read(storedCode));

        Assert.Contains(storedCode, refused.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WorkflowRules.AssignedToLhdCode, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowRules.AcceptedCode, WorkflowState.Accepted)]
    [InlineData(WorkflowRules.UnderInvestigationCode, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowRules.ClosedCode, WorkflowState.Closed)]
    [InlineData(WorkflowRules.NotInvestigatedCode, WorkflowState.NotInvestigated)]
    [InlineData(WorkflowRules.ApprovedByStateCode, WorkflowState.ApprovedByState)]
    [InlineData(WorkflowRules.RejectedByStateCode, WorkflowState.RejectedByState)]
    [InlineData(" closed ", WorkflowState.Closed)]
    public void TryParseReadsAKnownTargetCode(string offeredCode, WorkflowState expected)
    {
        Assert.True(WorkflowRules.TryParse(offeredCode, out var state));
        Assert.Equal(expected, state);
    }

    /// <summary>
    /// A transition target refuses what <see cref="WorkflowRules.Read"/> would
    /// accept as a default: an empty or absent code is not a state to move to,
    /// and neither is a code the vocabulary does not know.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("approved_by_lhd")]
    [InlineData("Closed")]
    public void TryParseRefusesAnEmptyOrUnknownTargetCode(string? offeredCode)
    {
        Assert.False(WorkflowRules.TryParse(offeredCode, out _));
    }

    /// <summary>
    /// Each state is stored under the code it reads back from: write-then-read
    /// is the identity.
    /// </summary>
    [Theory]
    [InlineData(WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowState.Accepted)]
    [InlineData(WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowState.Closed)]
    [InlineData(WorkflowState.NotInvestigated)]
    [InlineData(WorkflowState.ApprovedByState)]
    [InlineData(WorkflowState.RejectedByState)]
    public void CodeOfRoundTripsThroughRead(WorkflowState state)
    {
        Assert.Equal(state, WorkflowRules.Read(WorkflowRules.CodeOf(state)));
    }

    /// <summary>
    /// The initial state and its code are the assigned-to-LHD start a report is
    /// opened at.
    /// </summary>
    [Fact]
    public void TheInitialStateIsAssignedToLhd()
    {
        Assert.Equal(WorkflowState.AssignedToLhd, WorkflowRules.Initial);
        Assert.Equal(WorkflowRules.AssignedToLhdCode, WorkflowRules.InitialCode);
    }

    /// <summary>
    /// The transition table, stated here as the session plan states it: each
    /// transition and the states it is allowed from. Every pair the table does
    /// not name is refused, so the theory below walks all 63.
    /// </summary>
    private static readonly IReadOnlyDictionary<WorkflowTransition, WorkflowState[]> Table =
        new Dictionary<WorkflowTransition, WorkflowState[]>
        {
            [WorkflowTransition.Accept] = [WorkflowState.AssignedToLhd],
            [WorkflowTransition.Investigate] = [WorkflowState.Accepted, WorkflowState.RejectedByState],
            [WorkflowTransition.Close] = [WorkflowState.UnderInvestigation],
            [WorkflowTransition.Route] =
                [WorkflowState.AssignedToLhd, WorkflowState.Accepted, WorkflowState.UnderInvestigation, WorkflowState.RejectedByState],
            [WorkflowTransition.AssignInvestigator] = [WorkflowState.Accepted, WorkflowState.UnderInvestigation],
            [WorkflowTransition.DoNotInvestigate] = [WorkflowState.AssignedToLhd, WorkflowState.Accepted],
            [WorkflowTransition.Reopen] = [WorkflowState.Closed, WorkflowState.NotInvestigated, WorkflowState.ApprovedByState],
            [WorkflowTransition.Approve] = [WorkflowState.Closed],
            [WorkflowTransition.Reject] = [WorkflowState.Closed],
        };

    /// <summary>Every (state, transition) pair: seven states by nine transitions.</summary>
    public static TheoryData<WorkflowState, WorkflowTransition> EveryPair()
    {
        var pairs = new TheoryData<WorkflowState, WorkflowTransition>();

        foreach (var state in Enum.GetValues<WorkflowState>())
        {
            foreach (var transition in Enum.GetValues<WorkflowTransition>())
            {
                pairs.Add(state, transition);
            }
        }

        return pairs;
    }

    /// <summary>
    /// Each of the 63 pairs is allowed exactly when the table names the state
    /// under the transition, and refused otherwise.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void IsAllowedMatchesTheTransitionTableForEveryPair(WorkflowState from, WorkflowTransition transition)
    {
        Assert.Equal(Table[transition].Contains(from), WorkflowRules.IsAllowed(from, transition));
    }

    /// <summary>The table names 17 allowed pairs; the rest of the 63 are refused.</summary>
    [Fact]
    public void IsAllowedPermitsSeventeenOfTheSixtyThreePairs()
    {
        var allowed = Enum.GetValues<WorkflowState>()
            .SelectMany(state => Enum.GetValues<WorkflowTransition>()
                .Where(transition => WorkflowRules.IsAllowed(state, transition)))
            .Count();

        Assert.Equal(17, allowed);
    }

    [Theory]
    [InlineData(WorkflowTransition.Accept, WorkflowState.Accepted)]
    [InlineData(WorkflowTransition.Investigate, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowTransition.Close, WorkflowState.Closed)]
    [InlineData(WorkflowTransition.Route, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowTransition.AssignInvestigator, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowTransition.DoNotInvestigate, WorkflowState.NotInvestigated)]
    [InlineData(WorkflowTransition.Reopen, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowTransition.Approve, WorkflowState.ApprovedByState)]
    [InlineData(WorkflowTransition.Reject, WorkflowState.RejectedByState)]
    public void TargetOfNamesTheStateEachTransitionLandsIn(WorkflowTransition transition, WorkflowState expected)
    {
        Assert.Equal(expected, WorkflowRules.TargetOf(transition));
    }

    /// <summary>
    /// The transitions a state allows, in declaration order: what a Workflow
    /// Options menu offers before privileges narrow it. A state with one move
    /// out and a state with four are both checked, as is the order.
    /// </summary>
    [Fact]
    public void AllowedFromListsAStatesTransitionsInDeclarationOrder()
    {
        Assert.Equal(
            new[] { WorkflowTransition.Accept, WorkflowTransition.Route, WorkflowTransition.DoNotInvestigate },
            WorkflowRules.AllowedFrom(WorkflowState.AssignedToLhd));
        Assert.Equal(
            new[]
            {
                WorkflowTransition.Investigate, WorkflowTransition.Route, WorkflowTransition.AssignInvestigator,
                WorkflowTransition.DoNotInvestigate,
            },
            WorkflowRules.AllowedFrom(WorkflowState.Accepted));
        Assert.Equal(
            new[] { WorkflowTransition.Close, WorkflowTransition.Route, WorkflowTransition.AssignInvestigator },
            WorkflowRules.AllowedFrom(WorkflowState.UnderInvestigation));
        Assert.Equal(
            new[] { WorkflowTransition.Reopen, WorkflowTransition.Approve, WorkflowTransition.Reject },
            WorkflowRules.AllowedFrom(WorkflowState.Closed));
        Assert.Equal(new[] { WorkflowTransition.Reopen }, WorkflowRules.AllowedFrom(WorkflowState.NotInvestigated));
        Assert.Equal(new[] { WorkflowTransition.Reopen }, WorkflowRules.AllowedFrom(WorkflowState.ApprovedByState));
        Assert.Equal(
            new[] { WorkflowTransition.Investigate, WorkflowTransition.Route },
            WorkflowRules.AllowedFrom(WorkflowState.RejectedByState));
    }

    /// <summary>AllowedFrom agrees with IsAllowed for every state, so the two cannot drift apart.</summary>
    [Theory]
    [MemberData(nameof(EveryPair))]
    public void AllowedFromAgreesWithIsAllowed(WorkflowState from, WorkflowTransition transition)
    {
        Assert.Equal(
            WorkflowRules.IsAllowed(from, transition),
            WorkflowRules.AllowedFrom(from).Contains(transition));
    }

    [Theory]
    [InlineData(WorkflowRules.AcceptCode, WorkflowTransition.Accept)]
    [InlineData(WorkflowRules.InvestigateCode, WorkflowTransition.Investigate)]
    [InlineData(WorkflowRules.CloseCode, WorkflowTransition.Close)]
    [InlineData(WorkflowRules.RouteCode, WorkflowTransition.Route)]
    [InlineData(WorkflowRules.AssignInvestigatorCode, WorkflowTransition.AssignInvestigator)]
    [InlineData(WorkflowRules.DoNotInvestigateCode, WorkflowTransition.DoNotInvestigate)]
    [InlineData(WorkflowRules.ReopenCode, WorkflowTransition.Reopen)]
    [InlineData(WorkflowRules.ApproveCode, WorkflowTransition.Approve)]
    [InlineData(WorkflowRules.RejectCode, WorkflowTransition.Reject)]
    [InlineData(" reopen ", WorkflowTransition.Reopen)]
    public void TryParseTransitionReadsAKnownTransitionCode(string offeredCode, WorkflowTransition expected)
    {
        Assert.True(WorkflowRules.TryParseTransition(offeredCode, out var transition));
        Assert.Equal(expected, transition);
    }

    /// <summary>
    /// An empty or absent code is not a transition, nor is a state code offered
    /// where a transition is asked for, nor a code in the wrong case.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(WorkflowRules.AcceptedCode)]
    [InlineData(WorkflowRules.ClosedCode)]
    [InlineData("Accept")]
    [InlineData("approved_by_lhd")]
    public void TryParseTransitionRefusesAnEmptyOrUnknownCode(string? offeredCode)
    {
        Assert.False(WorkflowRules.TryParseTransition(offeredCode, out _));
    }

    /// <summary>Each transition travels as the code it parses back from: write-then-parse is the identity.</summary>
    [Theory]
    [InlineData(WorkflowTransition.Accept)]
    [InlineData(WorkflowTransition.Investigate)]
    [InlineData(WorkflowTransition.Close)]
    [InlineData(WorkflowTransition.Route)]
    [InlineData(WorkflowTransition.AssignInvestigator)]
    [InlineData(WorkflowTransition.DoNotInvestigate)]
    [InlineData(WorkflowTransition.Reopen)]
    [InlineData(WorkflowTransition.Approve)]
    [InlineData(WorkflowTransition.Reject)]
    public void CodeOfATransitionRoundTripsThroughTryParseTransition(WorkflowTransition transition)
    {
        Assert.True(WorkflowRules.TryParseTransition(WorkflowRules.CodeOf(transition), out var parsed));
        Assert.Equal(transition, parsed);
    }
}
