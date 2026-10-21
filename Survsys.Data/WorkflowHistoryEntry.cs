namespace Survsys.Data;

/// <summary>
/// One workflow move a case morbidity report made: who made it, when, which
/// transition it was, the state it left and the state it landed in, and the
/// note the person left with it.
/// </summary>
/// <remarks>
/// Written once per transition and never changed, as <see cref="AccessRecord"/>
/// is: the Administrative tab reads it back as the report's workflow history.
/// The transition and the two states are the codes <see cref="WorkflowRules"/>
/// writes, so a row reads the same way a report's own
/// <see cref="PersonCondition.WorkflowState"/> does. Both the transition and the
/// target are recorded because two transitions may land in one state — an
/// investigator assigned and a case reopened both put a report under
/// investigation — and the history is of what was done, not only of where it
/// ended up.
/// </remarks>
public class WorkflowHistoryEntry
{
    public int Id { get; set; }

    /// <summary>The report that moved.</summary>
    public int PersonConditionId { get; set; }

    /// <summary>The user who made the move.</summary>
    public int UserId { get; set; }

    /// <summary>When the move was made.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>The transition made, as its <see cref="WorkflowRules"/> code.</summary>
    public string Transition { get; set; } = "";

    /// <summary>The state the report left, as its code.</summary>
    public string FromState { get; set; } = "";

    /// <summary>The state the report landed in, as its code.</summary>
    public string ToState { get; set; } = "";

    /// <summary>What the person said about the move, if anything.</summary>
    public string? Note { get; set; }
}
