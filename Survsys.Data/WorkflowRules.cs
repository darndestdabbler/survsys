namespace Survsys.Data;

/// <summary>
/// A case morbidity report's workflow state: where the report sits in the
/// lifecycle a public health investigation moves it through, from the moment it
/// reaches a Local Health Department to the moment it is closed.
/// </summary>
/// <remarks>
/// The four states are the ones the session plan names, as the forward chain
/// <c>assigned_to_lhd</c> to <c>accepted</c> to <c>under_investigation</c> to
/// <c>closed</c>. The legacy EpiTrax workflow graph is larger — it has
/// <c>approved_by_lhd</c>, reopened and awaiting-review states and a
/// configurable workflow-definition table — and those are not here, for the
/// reason the case status vocabulary keeps to five: this session models the
/// chain the plan names and no more, and the state a report holds is a code
/// against this rule rather than a row of a table this schema does not have.
/// </remarks>
public enum WorkflowState
{
    /// <summary>
    /// Assigned to a Local Health Department, the state a case starts at. The
    /// legacy default is <c>assigned_to_lhd</c> — a case begins at the LHD
    /// level — so this is the state a report is opened at and the state an
    /// empty or absent code reads as.
    /// </summary>
    AssignedToLhd,

    /// <summary>Accepted by the LHD: the move out of <see cref="AssignedToLhd"/>.</summary>
    Accepted,

    /// <summary>Under investigation: the move out of <see cref="Accepted"/>.</summary>
    UnderInvestigation,

    /// <summary>Closed, the end of the chain: the move out of <see cref="UnderInvestigation"/>.</summary>
    Closed,
}

/// <summary>
/// The workflow vocabulary and the state machine over it: reading a stored code
/// into a state, parsing a caller's offered target code, writing a state back
/// to its code, and the transition table that says whether a move from one
/// state to another is allowed.
/// </summary>
/// <remarks>
/// Pure, and public and static in the shape <see cref="CaseStatusRules"/> has,
/// because the unit suite proves it without starting a host. It takes no
/// <c>DbContext</c> and reads no labs, condition or clock: a workflow code is a
/// string a caller has in hand, and what is here is the reading of it and the
/// rule for moving between states. The disease-specific and classification-
/// gated transitions the legacy drives from a report's labs and contacts — the
/// auto-close of a contact at <c>approved_by_lhd</c>, say — are not here; a
/// move is whatever an endpoint asks for and this table allows, and nothing
/// more.
/// </remarks>
public static class WorkflowRules
{
    /// <summary>The code <see cref="WorkflowState.AssignedToLhd"/> is stored under.</summary>
    public const string AssignedToLhdCode = "assigned_to_lhd";

    /// <summary>The code <see cref="WorkflowState.Accepted"/> is stored under.</summary>
    public const string AcceptedCode = "accepted";

    /// <summary>The code <see cref="WorkflowState.UnderInvestigation"/> is stored under.</summary>
    public const string UnderInvestigationCode = "under_investigation";

    /// <summary>The code <see cref="WorkflowState.Closed"/> is stored under.</summary>
    public const string ClosedCode = "closed";

    /// <summary>
    /// The state a report is opened at, <see cref="WorkflowState.AssignedToLhd"/>,
    /// as the legacy starts a case at the Local Health Department.
    /// </summary>
    public const WorkflowState Initial = WorkflowState.AssignedToLhd;

    /// <summary>
    /// The code <see cref="Initial"/> is stored under, for the caller that
    /// opens a report and writes its starting state.
    /// </summary>
    public const string InitialCode = AssignedToLhdCode;

    /// <summary>
    /// The state a stored code names.
    /// </summary>
    /// <remarks>
    /// An empty, whitespace or absent code is <see cref="WorkflowState.AssignedToLhd"/>,
    /// the state the legacy opens a case at, for the reason
    /// <see cref="CaseStatusRules.Read"/> reads an empty status as Unknown: a
    /// report written before this column existed has no code, and the default
    /// start is the state it was in. Surrounding whitespace is trimmed before a
    /// non-empty code is matched, and the match is exact — the codes are legacy
    /// codes and not display text.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// The code is neither empty nor one of the four this vocabulary knows. A
    /// stored value like that was written by something that did not go through
    /// this rule, as <see cref="CaseStatusRules.Read"/> refuses a stored status
    /// it cannot read and for the same reason.
    /// </exception>
    public static WorkflowState Read(string? storedCode)
    {
        if (string.IsNullOrWhiteSpace(storedCode))
        {
            return WorkflowState.AssignedToLhd;
        }

        if (Match(storedCode) is { } state)
        {
            return state;
        }

        throw new InvalidOperationException(
            $"'{storedCode}' is not a workflow state code this tier knows. A stored "
            + $"workflow state is one of {AssignedToLhdCode}, {AcceptedCode}, "
            + $"{UnderInvestigationCode} or {ClosedCode}, or empty for a report read as "
            + $"{AssignedToLhdCode}. A row holding anything else was written by something "
            + "that did not go through this vocabulary.");
    }

    /// <summary>
    /// Reads a caller's offered target code into a state, answering whether it
    /// is one — a known code yields its state, an empty or unknown code yields
    /// none.
    /// </summary>
    /// <remarks>
    /// Where <see cref="Read"/> treats an empty code as the default start and
    /// throws on an unknown one, this refuses both: a transition target is a
    /// state a caller is asking to move to, and neither an empty string nor a
    /// code the vocabulary does not know is such a state. The endpoint turns a
    /// false here into a 400, as it turns a false from
    /// <see cref="CaseStatusRules.IsAssignable"/> into one.
    /// </remarks>
    public static bool TryParse(string? offeredCode, out WorkflowState state)
    {
        if (!string.IsNullOrWhiteSpace(offeredCode) && Match(offeredCode) is { } matched)
        {
            state = matched;
            return true;
        }

        state = default;
        return false;
    }

    /// <summary>
    /// The code a state is stored under, for the caller that has a state in
    /// hand and writes it.
    /// </summary>
    public static string CodeOf(WorkflowState state) => state switch
    {
        WorkflowState.AssignedToLhd => AssignedToLhdCode,
        WorkflowState.Accepted => AcceptedCode,
        WorkflowState.UnderInvestigation => UnderInvestigationCode,
        WorkflowState.Closed => ClosedCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state, "is not a workflow state this rule knows."),
    };

    /// <summary>
    /// Whether a report may move from one state to another: the transition
    /// table of the state machine.
    /// </summary>
    /// <remarks>
    /// The chain is forward only — <see cref="WorkflowState.AssignedToLhd"/> to
    /// <see cref="WorkflowState.Accepted"/> to
    /// <see cref="WorkflowState.UnderInvestigation"/> to
    /// <see cref="WorkflowState.Closed"/> — so the three forward moves are
    /// allowed and every other ordered pair of the four states is refused:
    /// every backward move, every skip over a state, and every move from a
    /// state to itself. Reopening a closed case and the legacy's branching
    /// transitions are not in this chain, so they are among the refused.
    /// </remarks>
    public static bool IsAllowed(WorkflowState from, WorkflowState to) => (from, to) switch
    {
        (WorkflowState.AssignedToLhd, WorkflowState.Accepted) => true,
        (WorkflowState.Accepted, WorkflowState.UnderInvestigation) => true,
        (WorkflowState.UnderInvestigation, WorkflowState.Closed) => true,
        _ => false,
    };

    /// <summary>
    /// The state a code names, or null where a non-empty code is not one of the
    /// four. The common core of <see cref="Read"/> and <see cref="TryParse"/>,
    /// which differ only in what they do with an empty code and an unknown one.
    /// </summary>
    private static WorkflowState? Match(string? code) => code?.Trim() switch
    {
        AssignedToLhdCode => WorkflowState.AssignedToLhd,
        AcceptedCode => WorkflowState.Accepted,
        UnderInvestigationCode => WorkflowState.UnderInvestigation,
        ClosedCode => WorkflowState.Closed,
        _ => null,
    };
}
