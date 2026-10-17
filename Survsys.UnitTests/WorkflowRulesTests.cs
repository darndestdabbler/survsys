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
/// a host. The chain is the one the session plan names —
/// <c>assigned_to_lhd</c> to <c>accepted</c> to <c>under_investigation</c> to
/// <c>closed</c> — forward only, with every other ordered pair of the four
/// states refused.
/// </remarks>
public class WorkflowRulesTests
{
    [Theory]
    [InlineData(WorkflowRules.AssignedToLhdCode, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowRules.AcceptedCode, WorkflowState.Accepted)]
    [InlineData(WorkflowRules.UnderInvestigationCode, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowRules.ClosedCode, WorkflowState.Closed)]
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
    /// The three forward moves of the chain are the ones allowed.
    /// </summary>
    [Theory]
    [InlineData(WorkflowState.AssignedToLhd, WorkflowState.Accepted)]
    [InlineData(WorkflowState.Accepted, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowState.UnderInvestigation, WorkflowState.Closed)]
    public void IsAllowedPermitsEachForwardMove(WorkflowState from, WorkflowState to)
    {
        Assert.True(WorkflowRules.IsAllowed(from, to));
    }

    /// <summary>
    /// Every one of the other thirteen ordered pairs of the four states is
    /// refused: each state to itself, every backward move, and every skip over
    /// a state.
    /// </summary>
    [Theory]
    // Same-state, all four.
    [InlineData(WorkflowState.AssignedToLhd, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowState.Accepted, WorkflowState.Accepted)]
    [InlineData(WorkflowState.UnderInvestigation, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowState.Closed, WorkflowState.Closed)]
    // Forward skips.
    [InlineData(WorkflowState.AssignedToLhd, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowState.AssignedToLhd, WorkflowState.Closed)]
    [InlineData(WorkflowState.Accepted, WorkflowState.Closed)]
    // Backward moves, every ordered pair that goes back down the chain.
    [InlineData(WorkflowState.Accepted, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowState.UnderInvestigation, WorkflowState.Accepted)]
    [InlineData(WorkflowState.UnderInvestigation, WorkflowState.AssignedToLhd)]
    [InlineData(WorkflowState.Closed, WorkflowState.UnderInvestigation)]
    [InlineData(WorkflowState.Closed, WorkflowState.Accepted)]
    [InlineData(WorkflowState.Closed, WorkflowState.AssignedToLhd)]
    public void IsAllowedRefusesEveryOtherMove(WorkflowState from, WorkflowState to)
    {
        Assert.False(WorkflowRules.IsAllowed(from, to));
    }
}
