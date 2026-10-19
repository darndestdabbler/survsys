namespace Survsys.Contracts;

/// <summary>
/// A task to record against a case morbidity report, with the schedule it
/// repeats on where it repeats: what
/// <c>POST /api/person-conditions/{id}/tasks</c> takes.
/// </summary>
/// <remarks>
/// The writable columns of the legacy <c>task</c> table, as declared in
/// <c>legacy/nedss-jpa/src/main/resources/db/migration/V0001__create.sql</c>
/// under <c>../nedss</c>, less the identity the server assigns, as the Notes
/// tab's "+ Add Task" records one —
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps the section.
///
/// <strong>The report, the series and the moments are not here.</strong> A
/// task is recorded against the report in the route, so the server sets
/// <c>person_condition_id</c> from it rather than taking it on the wire;
/// <c>repeating_task_id</c> is the server's, because the series is expanded
/// where it is recorded and each later task is pointed at the first; and
/// <c>created_at</c> and <c>updated_at</c> are the server's clock.
///
/// <see cref="RepeatingInterval"/> and <see cref="UntilDate"/> are the whole
/// of the schedule: left out, the task happens once. Given, they are expanded
/// into one task per date when this request is recorded and never re-expanded
/// afterwards — the API tier validates them against the repeating-task
/// schedule rule, which refuses an interval it does not know, a repeat with
/// no due date to count from, an until date before the due date, and a series
/// longer than the rule's cap.
/// </remarks>
/// <param name="Name">What the task is called. A task with no name is refused.</param>
/// <param name="Notes">What else there is to say about the task.</param>
/// <param name="Priority">How urgent the task is, as the legacy code.</param>
/// <param name="DueDate">
/// The day the task is due, and the day a series is counted from.
/// </param>
/// <param name="UserId">
/// Whose task it is, or null where nobody is named. A bare id: there is no
/// user resource in this release.
/// </param>
/// <param name="CategoryId">
/// What kind of task it is, as the legacy <c>category_id</c>; a bare id, with
/// no category resource in this release to list.
/// </param>
/// <param name="Status">
/// What state the task is in — the legacy <c>open</c> or <c>complete</c> code
/// — or null to leave it unset. The API tier validates it against the two.
/// </param>
/// <param name="RepeatingInterval">
/// How often the task repeats — the legacy <c>daily</c>, <c>weekly</c> or
/// <c>monthly</c> code — or null for a task that happens once.
/// </param>
/// <param name="UntilDate">
/// The last day the series may reach, or null to record the first task alone.
/// </param>
public record CreateTaskRequest(
    string? Name = null,
    string? Notes = null,
    string? Priority = null,
    DateOnly? DueDate = null,
    int? UserId = null,
    int? CategoryId = null,
    string? Status = null,
    string? RepeatingInterval = null,
    DateOnly? UntilDate = null);
