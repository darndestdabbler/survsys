namespace Survsys.Contracts;

/// <summary>
/// The status to set on a task: what <c>PUT /api/tasks/{id}/status</c> takes.
/// </summary>
/// <remarks>
/// One writable field, where the task id is the route's, as
/// <see cref="UpdateCaseStatusRequest"/> takes one with the report id in the
/// route. It is the one mutation a stored task takes: the name, the notes, the
/// priority, the due date and the schedule are set when the task is recorded
/// and not changed afterwards.
///
/// The value is the legacy status code — <c>open</c> or <c>complete</c> — and
/// the API tier validates it against the two, refusing a code it does not
/// know. There is no clearing it to null: a task whose status is being set is
/// being worked, and the screen that sets it offers the two.
/// </remarks>
/// <param name="Status">The legacy status code to set.</param>
public record UpdateTaskStatusRequest(string? Status);
