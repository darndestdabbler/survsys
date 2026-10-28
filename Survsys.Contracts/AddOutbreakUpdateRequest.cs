namespace Survsys.Contracts;

/// <summary>
/// An entry to append to an outbreak's activity log: what
/// <c>POST /api/outbreaks/{id}/activity</c> takes.
/// </summary>
/// <remarks>
/// The log is append-only, so this adds an entry and never changes one. When
/// it was added is not here: the server stamps it, so the timeline's order is
/// the server's clock and not the caller's. Nor is its author: the server
/// records the signed-in user adding it.
/// </remarks>
/// <param name="Description">What the entry says, or null for none.</param>
/// <param name="Type">The kind of entry, as the legacy <c>outbreak_update.type</c> int.</param>
public record AddOutbreakUpdateRequest(string? Description = null, int Type = 0);
