using Survsys.Contracts;

namespace Survsys.Web;

/// <summary>
/// The application tier's reader and writer of tasks:
/// <c>GET</c> and <c>POST /api/person-conditions/{id}/tasks</c> for one
/// report's, <c>GET /api/tasks</c> for the task list screen's, and
/// <c>PUT /api/tasks/{id}/status</c> for the one mutation a task takes.
/// </summary>
/// <remarks>
/// It is the only thing in this tier that speaks HTTP about tasks, and the
/// failure handling is <see cref="ApiAsk"/>'s, as every client's is.
///
/// Three prefixes and not four: a report's tasks and the whole system's are
/// the same list read under two scopes, so a user whose list could not be
/// loaded reads the same sentence either way. The other two — a task that
/// could not be recorded and one whose status would not take — are their own
/// sentences, as <see cref="NotesClient"/>'s three are.
///
/// The record answers with the whole series, because a repeating task is
/// several tasks and the screen that asked for one shows what it actually
/// wrote.
/// </remarks>
public sealed class TasksClient(HttpClient http)
{
    /// <summary>How a failure to list tasks starts.</summary>
    public const string ListFailurePrefix = "The tasks could not be loaded";

    /// <summary>How a failure to record a new task starts.</summary>
    public const string RecordFailurePrefix = "The task could not be recorded";

    /// <summary>How a failure to set a task's status starts.</summary>
    public const string StatusFailurePrefix = "The task could not be changed";

    private static readonly ApiAsk Listing = new(ListFailurePrefix, "a page of tasks");

    private static readonly ApiAsk Recording = new(RecordFailurePrefix, "the tasks recorded");

    private static readonly ApiAsk Setting = new(StatusFailurePrefix, "the task as it now stands");

    /// <summary>
    /// One page of a report's tasks, by due date, of one status where one is
    /// named, or why there is none.
    /// </summary>
    public Task<Asked<PagedResult<TaskResponse>>> Page(
        int reportId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<TaskResponse>>(
            http,
            $"api/person-conditions/{reportId}/tasks{Query(status, page, pageSize)}",
            cancellationToken);

    /// <summary>
    /// One page of tasks across every report, by due date, of one status where
    /// one is named, or why there is none.
    /// </summary>
    /// <remarks>
    /// What the task list screen reads. The status is the legacy "Show all
    /// tasks" filter: named, the API tier returns that status alone; left out,
    /// it returns every task whatever its status.
    /// </remarks>
    public Task<Asked<PagedResult<TaskResponse>>> All(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        Listing.Get<PagedResult<TaskResponse>>(
            http,
            $"api/tasks{Query(status, page, pageSize)}",
            cancellationToken);

    /// <summary>
    /// Records a task against a report and answers with what was written —
    /// the one task, or the whole series where it repeats — or with why
    /// nothing was.
    /// </summary>
    public Task<Asked<TaskSeriesResponse>> Record(
        int reportId,
        CreateTaskRequest task,
        CancellationToken cancellationToken = default) =>
        Recording.Post<CreateTaskRequest, TaskSeriesResponse>(
            http, $"api/person-conditions/{reportId}/tasks", task, cancellationToken);

    /// <summary>
    /// Sets a task's status and answers with the task as it now stands.
    /// </summary>
    /// <remarks>
    /// The only change this tier makes to a stored task: there is no method
    /// here that moves a due date or edits a name, because there is no
    /// endpoint that does.
    /// </remarks>
    public Task<Asked<TaskResponse>> SetStatus(
        int taskId,
        string status,
        CancellationToken cancellationToken = default) =>
        Setting.Put<UpdateTaskStatusRequest, TaskResponse>(
            http,
            $"api/tasks/{taskId}/status",
            new UpdateTaskStatusRequest(status),
            cancellationToken);

    /// <summary>
    /// The query string both lists are read with: the page always, and the
    /// status only where one is named — an absent status is every task, and
    /// sending an empty one would ask for the tasks whose status is the empty
    /// string.
    /// </summary>
    private static string Query(string? status, int page, int pageSize) =>
        string.IsNullOrWhiteSpace(status)
            ? $"?page={page}&pageSize={pageSize}"
            : $"?status={Uri.EscapeDataString(status)}&page={page}&pageSize={pageSize}";
}
