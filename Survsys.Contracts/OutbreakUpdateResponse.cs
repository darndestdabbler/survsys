namespace Survsys.Contracts;

/// <summary>
/// One entry in an outbreak's activity log, as the timeline shows it: when it
/// was added, who added it, what it says and its kind.
/// </summary>
/// <param name="Id">The entry's identifier.</param>
/// <param name="CreatedAt">When the entry was added.</param>
/// <param name="UserId">Who added the entry.</param>
/// <param name="Description">What the entry says, or null where none was given.</param>
/// <param name="Type">The kind of entry, as the legacy <c>outbreak_update.type</c> int.</param>
public record OutbreakUpdateResponse(
    int Id,
    DateTimeOffset CreatedAt,
    int UserId,
    string? Description,
    int Type);
