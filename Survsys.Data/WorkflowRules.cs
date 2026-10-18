namespace Survsys.Data;

/// <summary>
/// A case morbidity report's workflow state: where the report sits in the
/// lifecycle a public health investigation moves it through, from the moment it
/// reaches a Local Health Department to the moment it is closed.
/// </summary>
/// <remarks>
/// Seven states: the forward chain <c>assigned_to_lhd</c> to <c>accepted</c> to
/// <c>under_investigation</c> to <c>closed</c> that every investigation walks,
/// and the three the legacy graph branches to — <c>not_investigated</c> when a
/// department declines a case, and <c>approved_by_state</c> or
/// <c>rejected_by_state</c> when the state reviews a closed one. The legacy
/// EpiTrax graph is driven from a configurable workflow-definition table; here
/// the graph is code, in the transition table <see cref="WorkflowRules"/>
/// holds, and the state a report holds is a code against this rule rather than
/// a row of a table this schema does not have.
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

    /// <summary>Closed by the investigating department: the move out of <see cref="UnderInvestigation"/>.</summary>
    Closed,

    /// <summary>
    /// Not investigated: the department declined the case before an
    /// investigation began. Reopening is the one move out.
    /// </summary>
    NotInvestigated,

    /// <summary>
    /// Approved by the state: the state's review accepted a closed
    /// investigation. Reopening is the one move out.
    /// </summary>
    ApprovedByState,

    /// <summary>
    /// Rejected by the state: the state's review sent a closed investigation
    /// back to the department, which investigates again or routes it on.
    /// </summary>
    RejectedByState,
}

/// <summary>
/// A move a case morbidity report makes between workflow states: the legacy
/// Workflow Options, each named for what the person does rather than for the
/// state it lands in, because two moves may land in the same state (an
/// investigator assigned and a case reopened both put a report under
/// investigation) and the history records which was made.
/// </summary>
public enum WorkflowTransition
{
    /// <summary>The department accepts the case into its queue.</summary>
    Accept,

    /// <summary>The department begins, or resumes, the investigation.</summary>
    Investigate,

    /// <summary>The department closes the investigation.</summary>
    Close,

    /// <summary>The case is routed to another agency, which starts it at <see cref="WorkflowState.AssignedToLhd"/> again.</summary>
    Route,

    /// <summary>An investigator is named on the case, which puts it under investigation.</summary>
    AssignInvestigator,

    /// <summary>The department declines to investigate the case.</summary>
    DoNotInvestigate,

    /// <summary>A finished or declined case is reopened for investigation.</summary>
    Reopen,

    /// <summary>The state approves a closed investigation.</summary>
    Approve,

    /// <summary>The state rejects a closed investigation and sends it back.</summary>
    Reject,
}

/// <summary>
/// The workflow vocabulary and the state machine over it: reading a stored code
/// into a state, parsing a caller's offered state or transition code, writing
/// either back to its code, and the transition table that says which moves a
/// state allows and where each one lands.
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

    /// <summary>The code <see cref="WorkflowState.NotInvestigated"/> is stored under.</summary>
    public const string NotInvestigatedCode = "not_investigated";

    /// <summary>The code <see cref="WorkflowState.ApprovedByState"/> is stored under.</summary>
    public const string ApprovedByStateCode = "approved_by_state";

    /// <summary>The code <see cref="WorkflowState.RejectedByState"/> is stored under.</summary>
    public const string RejectedByStateCode = "rejected_by_state";

    /// <summary>The code <see cref="WorkflowTransition.Accept"/> travels as.</summary>
    public const string AcceptCode = "accept";

    /// <summary>The code <see cref="WorkflowTransition.Investigate"/> travels as.</summary>
    public const string InvestigateCode = "investigate";

    /// <summary>The code <see cref="WorkflowTransition.Close"/> travels as.</summary>
    public const string CloseCode = "close";

    /// <summary>The code <see cref="WorkflowTransition.Route"/> travels as.</summary>
    public const string RouteCode = "route";

    /// <summary>The code <see cref="WorkflowTransition.AssignInvestigator"/> travels as.</summary>
    public const string AssignInvestigatorCode = "assign_investigator";

    /// <summary>The code <see cref="WorkflowTransition.DoNotInvestigate"/> travels as.</summary>
    public const string DoNotInvestigateCode = "do_not_investigate";

    /// <summary>The code <see cref="WorkflowTransition.Reopen"/> travels as.</summary>
    public const string ReopenCode = "reopen";

    /// <summary>The code <see cref="WorkflowTransition.Approve"/> travels as.</summary>
    public const string ApproveCode = "approve";

    /// <summary>The code <see cref="WorkflowTransition.Reject"/> travels as.</summary>
    public const string RejectCode = "reject";

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
    /// The code is neither empty nor one of the seven this vocabulary knows. A
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
            + $"{UnderInvestigationCode}, {ClosedCode}, {NotInvestigatedCode}, "
            + $"{ApprovedByStateCode} or {RejectedByStateCode}, or empty for a report read as "
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
        WorkflowState.NotInvestigated => NotInvestigatedCode,
        WorkflowState.ApprovedByState => ApprovedByStateCode,
        WorkflowState.RejectedByState => RejectedByStateCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(state), state, "is not a workflow state this rule knows."),
    };

    /// <summary>
    /// Reads a caller's offered transition code into a transition, answering
    /// whether it is one — a known code yields its transition, an empty or
    /// unknown code yields none — as <see cref="TryParse"/> does for a state.
    /// </summary>
    public static bool TryParseTransition(string? offeredCode, out WorkflowTransition transition)
    {
        if (!string.IsNullOrWhiteSpace(offeredCode) && MatchTransition(offeredCode) is { } matched)
        {
            transition = matched;
            return true;
        }

        transition = default;
        return false;
    }

    /// <summary>The code a transition travels as, for the caller that has one in hand and writes it.</summary>
    public static string CodeOf(WorkflowTransition transition) => transition switch
    {
        WorkflowTransition.Accept => AcceptCode,
        WorkflowTransition.Investigate => InvestigateCode,
        WorkflowTransition.Close => CloseCode,
        WorkflowTransition.Route => RouteCode,
        WorkflowTransition.AssignInvestigator => AssignInvestigatorCode,
        WorkflowTransition.DoNotInvestigate => DoNotInvestigateCode,
        WorkflowTransition.Reopen => ReopenCode,
        WorkflowTransition.Approve => ApproveCode,
        WorkflowTransition.Reject => RejectCode,
        _ => throw new ArgumentOutOfRangeException(
            nameof(transition), transition, "is not a workflow transition this rule knows."),
    };

    /// <summary>
    /// The state a transition lands a report in, whatever state it left from.
    /// </summary>
    /// <remarks>
    /// Three transitions land under investigation — the department investigating
    /// a case it accepted or had sent back, an investigator being assigned, and
    /// a finished or declined case being reopened — which is why a move is named
    /// by its transition and not by its target: the target alone would not say
    /// which was made.
    /// </remarks>
    public static WorkflowState TargetOf(WorkflowTransition transition) => transition switch
    {
        WorkflowTransition.Accept => WorkflowState.Accepted,
        WorkflowTransition.Investigate => WorkflowState.UnderInvestigation,
        WorkflowTransition.Close => WorkflowState.Closed,
        WorkflowTransition.Route => WorkflowState.AssignedToLhd,
        WorkflowTransition.AssignInvestigator => WorkflowState.UnderInvestigation,
        WorkflowTransition.DoNotInvestigate => WorkflowState.NotInvestigated,
        WorkflowTransition.Reopen => WorkflowState.UnderInvestigation,
        WorkflowTransition.Approve => WorkflowState.ApprovedByState,
        WorkflowTransition.Reject => WorkflowState.RejectedByState,
        _ => throw new ArgumentOutOfRangeException(
            nameof(transition), transition, "is not a workflow transition this rule knows."),
    };

    /// <summary>
    /// Whether a report in one state may make a transition: the transition
    /// table of the state machine.
    /// </summary>
    /// <remarks>
    /// The forward chain is accept from assigned-to-LHD, investigate from
    /// accepted, close from under investigation. Around it: a case is routed to
    /// another agency from any state a department is still working it in
    /// (assigned, accepted, under investigation or sent back by the state); an
    /// investigator is assigned once the case is accepted, and reassigned while
    /// it is under investigation; a department declines a case before it begins
    /// investigating, from assigned or accepted; the state approves or rejects
    /// a closed case, and a rejected case is investigated again; and a closed,
    /// declined or approved case is reopened. Every other pair is refused,
    /// including every transition out of a state it would not change — there is
    /// no accept of an accepted case, and a route is the one move that lands a
    /// report where it already is, by handing it to another agency.
    /// </remarks>
    public static bool IsAllowed(WorkflowState from, WorkflowTransition transition) => (transition, from) switch
    {
        (WorkflowTransition.Accept, WorkflowState.AssignedToLhd) => true,
        (WorkflowTransition.Investigate, WorkflowState.Accepted or WorkflowState.RejectedByState) => true,
        (WorkflowTransition.Close, WorkflowState.UnderInvestigation) => true,
        (WorkflowTransition.Route, WorkflowState.AssignedToLhd or WorkflowState.Accepted
            or WorkflowState.UnderInvestigation or WorkflowState.RejectedByState) => true,
        (WorkflowTransition.AssignInvestigator, WorkflowState.Accepted or WorkflowState.UnderInvestigation) => true,
        (WorkflowTransition.DoNotInvestigate, WorkflowState.AssignedToLhd or WorkflowState.Accepted) => true,
        (WorkflowTransition.Reopen, WorkflowState.Closed or WorkflowState.NotInvestigated
            or WorkflowState.ApprovedByState) => true,
        (WorkflowTransition.Approve, WorkflowState.Closed) => true,
        (WorkflowTransition.Reject, WorkflowState.Closed) => true,
        _ => false,
    };

    /// <summary>
    /// The transitions a state allows, in the order <see cref="WorkflowTransition"/>
    /// declares them: what a Workflow Options menu offers before the caller's
    /// privileges narrow it.
    /// </summary>
    public static IReadOnlyList<WorkflowTransition> AllowedFrom(WorkflowState state) =>
        Enum.GetValues<WorkflowTransition>().Where(transition => IsAllowed(state, transition)).ToArray();

    /// <summary>
    /// The state a code names, or null where a non-empty code is not one of the
    /// seven. The common core of <see cref="Read"/> and <see cref="TryParse"/>,
    /// which differ only in what they do with an empty code and an unknown one.
    /// </summary>
    private static WorkflowState? Match(string? code) => code?.Trim() switch
    {
        AssignedToLhdCode => WorkflowState.AssignedToLhd,
        AcceptedCode => WorkflowState.Accepted,
        UnderInvestigationCode => WorkflowState.UnderInvestigation,
        ClosedCode => WorkflowState.Closed,
        NotInvestigatedCode => WorkflowState.NotInvestigated,
        ApprovedByStateCode => WorkflowState.ApprovedByState,
        RejectedByStateCode => WorkflowState.RejectedByState,
        _ => null,
    };

    /// <summary>The transition a code names, or null where it is not one of the nine; trimmed and exact, as <see cref="Match"/> is.</summary>
    private static WorkflowTransition? MatchTransition(string? code) => code?.Trim() switch
    {
        AcceptCode => WorkflowTransition.Accept,
        InvestigateCode => WorkflowTransition.Investigate,
        CloseCode => WorkflowTransition.Close,
        RouteCode => WorkflowTransition.Route,
        AssignInvestigatorCode => WorkflowTransition.AssignInvestigator,
        DoNotInvestigateCode => WorkflowTransition.DoNotInvestigate,
        ReopenCode => WorkflowTransition.Reopen,
        ApproveCode => WorkflowTransition.Approve,
        RejectCode => WorkflowTransition.Reject,
        _ => null,
    };
}
