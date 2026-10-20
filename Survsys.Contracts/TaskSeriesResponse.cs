namespace Survsys.Contracts;

/// <summary>
/// The tasks one recording wrote: what
/// <c>POST /api/person-conditions/{id}/tasks</c> answers with.
/// </summary>
/// <remarks>
/// One element for a task that happens once, and the whole dated series for a
/// task that repeats — the first task and one per date the schedule expands
/// to, in date order, each later one pointing at the first through
/// <see cref="TaskResponse.RepeatingTaskId"/>. One shape either way, so a
/// caller reads what it recorded without having to know in advance whether it
/// asked for one task or several.
///
/// The series is written in one transaction, so the tasks here either all
/// landed or none did; nothing afterwards extends it, re-expands it or cancels
/// the rest of it.
/// </remarks>
/// <param name="Tasks">The tasks that were recorded, in date order.</param>
public record TaskSeriesResponse(IReadOnlyList<TaskResponse> Tasks);
