namespace Survsys.Contracts;

/// <summary>
/// What a promotion produced: the case morbidity report it opened, and the
/// batch notification the promotion raised — the shape
/// <c>POST /api/staged-messages/{id}/case</c> returns.
/// </summary>
/// <remarks>
/// The report is a <see cref="PersonConditionResponse"/>, the same shape a
/// report opened directly comes back as, so a promoted case reads the same as
/// any other and the reviewer has its record number and a way to open it.
///
/// The notification is the batch notification the promotion raised, reduced to
/// what this phase needs: a line a reviewer reads, naming the record number
/// and the condition, standing in for the legacy EMSA <c>batch_notifications</c>
/// e-mail without sending or storing one. The outbound CDC notification — the
/// rule sets, the generated message and its transmission — is Phase 6's, and
/// this line is not it; it is the reviewer being told, at the moment of
/// promotion, that a notification is owed for the case they just opened.
/// </remarks>
/// <param name="Report">The case morbidity report the promotion opened.</param>
/// <param name="Notification">
/// The batch notification the promotion raised, as a line for the reviewer.
/// </param>
public record PromoteMessageResponse(
    PersonConditionResponse Report,
    string Notification);
