namespace Survsys.Contracts;

/// <summary>
/// A workflow move to make on a case morbidity report: what
/// <c>PUT /api/person-conditions/{id}/workflow</c> takes.
/// </summary>
/// <remarks>
/// The move is named by its transition and not by the state it lands in,
/// because two transitions may land in one state and the history records which
/// was made. The codes are the legacy workflow's: <c>accept</c>,
/// <c>investigate</c>, <c>close</c>, <c>route</c>, <c>assign_investigator</c>,
/// <c>do_not_investigate</c>, <c>reopen</c>, <c>approve</c> and <c>reject</c>.
/// The API tier decides whether the report's state allows the move and whether
/// the caller holds the privilege it needs.
/// </remarks>
/// <param name="Transition">The move to make, as its code.</param>
/// <param name="Note">What the person wants recorded with the move, or null for nothing.</param>
/// <param name="AgencyId">The agency a <c>route</c> sends the report to; required for a route and ignored otherwise.</param>
/// <param name="InvestigatorId">The user an <c>assign_investigator</c> names; required for an assignment and ignored otherwise.</param>
public record WorkflowTransitionRequest(
    string? Transition,
    string? Note = null,
    int? AgencyId = null,
    int? InvestigatorId = null);
