namespace Survsys.Contracts;

/// <summary>
/// What an action on staged messages did: how many messages it acted on.
/// </summary>
/// <param name="Count">The number of messages moved, deleted or retried.</param>
public record StagedMessageActionResponse(int Count);
