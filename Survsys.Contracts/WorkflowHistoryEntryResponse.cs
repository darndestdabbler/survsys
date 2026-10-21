namespace Survsys.Contracts;

/// <summary>
/// One workflow move a case morbidity report made, as its history lists them:
/// who, when, which transition, from which state to which, and the note.
/// </summary>
/// <param name="OccurredAt">When the move was made.</param>
/// <param name="UserId">The user who made it.</param>
/// <param name="UserDisplayName">That user's display name, so the history reads without a second ask.</param>
/// <param name="Transition">The move made, as its code.</param>
/// <param name="FromState">The state the report left, as its code.</param>
/// <param name="ToState">The state the report landed in, as its code.</param>
/// <param name="Note">What the person recorded with the move, or null for nothing.</param>
public record WorkflowHistoryEntryResponse(
    DateTimeOffset OccurredAt,
    int UserId,
    string UserDisplayName,
    string Transition,
    string FromState,
    string ToState,
    string? Note);
