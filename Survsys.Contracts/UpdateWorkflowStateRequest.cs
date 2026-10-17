namespace Survsys.Contracts;

/// <summary>
/// The workflow state to move a report to: what
/// <c>PUT /api/person-conditions/{id}/workflow</c> takes.
/// </summary>
/// <remarks>
/// One field, where the report id is the route's. The value is the legacy code
/// naming the target state — one of assigned to LHD, accepted, under
/// investigation or closed — and the API tier validates it against the
/// transition table: a code the vocabulary does not know, or a move the chain
/// does not allow from the report's current state, is refused. The moves are
/// the forward chain only; nothing here reopens a closed case.
/// </remarks>
/// <param name="WorkflowState">
/// The legacy code of the state to move the report to.
/// </param>
public record UpdateWorkflowStateRequest(string? WorkflowState);
