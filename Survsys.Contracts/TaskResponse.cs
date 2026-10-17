namespace Survsys.Contracts;

/// <summary>
/// A task, as the API tier returns it: what
/// <c>GET /api/person-conditions/{id}/tasks</c> and <c>GET /api/tasks</c>
/// return one row of, what <see cref="TaskSeriesResponse"/> carries, and what
/// <c>PUT /api/tasks/{id}/status</c> answers with.
/// </summary>
/// <remarks>
/// The legacy <c>task</c> table's columns, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, as the Notes tab's Tasks section and the task list
/// screen show them —
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps the section. The report is here as well as in the route, because the
/// task list screen reads tasks across reports and shows which report each one
/// is against.
///
/// <see cref="RepeatingTaskId"/> is the first task of the series this one
/// belongs to, and null on a task that does not repeat and on the first task
/// of a series — the one the rest point at. The interval and the until date
/// are returned as recorded, on every task of the series, because a reader of
/// one task should be able to see that it is one of several and on what
/// schedule; nothing re-expands them.
/// </remarks>
/// <param name="Id">The task's identifier, as the status route names it.</param>
/// <param name="PersonConditionId">The case morbidity report this task is against, or null where none is set.</param>
/// <param name="Name">What the task is called.</param>
/// <param name="Notes">What else there is to say about the task.</param>
/// <param name="Priority">How urgent the task is, as the legacy code.</param>
/// <param name="DueDate">The day the task is due, or null where none is set.</param>
/// <param name="UserId">Whose task it is, or null where nobody is named.</param>
/// <param name="CategoryId">What kind of task it is, or null where none is recorded.</param>
/// <param name="Status">What state the task is in: the legacy <c>open</c> or <c>complete</c> code.</param>
/// <param name="RepeatingTaskId">The first task of this task's series, or null where it is not one of several.</param>
/// <param name="RepeatingInterval">How often the series repeats, or null on a task that happens once.</param>
/// <param name="UntilDate">The last day the series may reach, or null where none is set.</param>
/// <param name="CreatedAt">When the task was raised, with the offset it was raised at.</param>
/// <param name="UpdatedAt">When the task was last changed, or null until something changes it.</param>
public record TaskResponse(
    int Id,
    int? PersonConditionId,
    string? Name,
    string? Notes,
    string? Priority,
    DateOnly? DueDate,
    int? UserId,
    int? CategoryId,
    string? Status,
    int? RepeatingTaskId,
    string? RepeatingInterval,
    DateOnly? UntilDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
