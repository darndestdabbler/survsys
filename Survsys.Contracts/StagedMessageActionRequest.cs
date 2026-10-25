namespace Survsys.Contracts;

/// <summary>
/// An action on one or more staged messages: what
/// <c>POST /api/staged-messages/move</c>, <c>/delete</c> and <c>/retry</c>
/// take.
/// </summary>
/// <remarks>
/// One shape for the three, because each acts on a list of messages and only
/// a move needs more. The action is all or nothing: one message the action
/// may not be taken on refuses the whole request, and nothing is changed.
/// </remarks>
/// <param name="Ids">The messages to act on.</param>
/// <param name="Status">
/// For a move, the review-state code the messages move to; ignored otherwise.
/// </param>
public record StagedMessageActionRequest(IReadOnlyList<int> Ids, string? Status = null);
