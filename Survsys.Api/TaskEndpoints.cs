using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/tasks</c>: records a task against a
/// report, and where it repeats, its whole series.
/// <c>GET /api/person-conditions/{id}/tasks</c>: that report's tasks, by due
/// date and paged. <c>GET /api/tasks</c>: tasks across every report, which is
/// what the task list screen reads. And <c>PUT /api/tasks/{id}/status</c>: the
/// one mutation a stored task takes.
/// </summary>
/// <remarks>
/// Modelled on <see cref="ExposureEndpoints"/> and <see cref="NoteEndpoints"/>,
/// slice for slice. The entity-to-wire mapping lives here, as the other
/// slices' does and for the same reason: <c>Survsys.Data</c> may not reference
/// <c>Survsys.Contracts</c>. The handlers are handed <see cref="TaskQueries"/>
/// and not the <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// <strong>What a schedule means is not decided here.</strong> The repeating
/// interval vocabulary, what makes an offered schedule impossible, and which
/// dates one expands to are <see cref="RecurringTaskRules"/>'s, in
/// <c>Survsys.Data</c> beside the column they are about, as the case status
/// and workflow vocabularies are. This slice asks that rule and turns its
/// sentence into a 400; it restates none of it.
///
/// A task is scoped to the report in the route, as a note and an exposure are:
/// the server sets <c>person_condition_id</c> from it and the moments from the
/// clock. A series is expanded once, where it is recorded, and nothing
/// afterwards re-expands it, extends it or cancels the rest of it — there is
/// no PUT over a task's name, notes, priority, due date or schedule, and no
/// DELETE.
/// </remarks>
public static class TaskEndpoints
{
    /// <summary>The code a task still to be done is stored under.</summary>
    /// <remarks>
    /// The legacy <c>task.status</c> column is a plain <c>VARCHAR</c> with no
    /// reference table behind it, and the legacy snapshot's <c>task</c> table
    /// is empty, so there is no stored vocabulary to match: the two states the
    /// Notes tab's task section and the task list screen work in are open and
    /// complete. Two consts here and not a rule class, for the reason
    /// <see cref="NoteEndpoints.ClinicalNoteType"/> is a const: only this
    /// slice reads them.
    /// </remarks>
    public const string OpenStatus = "open";

    /// <summary>The code a task that has been done is stored under.</summary>
    public const string CompleteStatus = "complete";

    /// <summary>The projection from a stored task to its wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the lists, and a unit test
    /// compiles and invokes it to prove what it carries. One shape serves both
    /// lists, the recording and the status write. Public because the unit
    /// suite proves it.
    /// </remarks>
    public static readonly Expression<Func<CaseTask, TaskResponse>> TaskToResponse =
        task => new TaskResponse(
            task.Id,
            task.PersonConditionId,
            task.Name,
            task.Notes,
            task.Priority,
            task.DueDate,
            task.UserId,
            task.CategoryId,
            task.Status,
            task.RepeatingTaskId,
            task.RepeatingInterval,
            task.UntilDate,
            task.CreatedAt,
            task.UpdatedAt);

    /// <summary>
    /// <see cref="TaskToResponse"/> compiled, for the writes that answer with
    /// the tasks they just saved rather than reading them back through a query
    /// EF would translate.
    /// </summary>
    public static readonly Func<CaseTask, TaskResponse> Respond = TaskToResponse.Compile();

    /// <summary>
    /// Why the task asked to be recorded is not one this endpoint can record,
    /// as far as can be said without the database, or null when nothing is
    /// wrong with it.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="ExposureEndpoints.WhyNotAnExposure"/> has: a pure
    /// function the unit suite proves, with the endpoint turning the sentence
    /// into a 400 that names the field. Whether the report exists is not here,
    /// because that needs a query. What is here is what is wrong with the task
    /// on its face: a name with nothing in it, because a task list of unnamed
    /// rows is of no use to whoever has to work it; a status outside the two
    /// the vocabulary knows, where one is given at all; and whatever
    /// <see cref="RecurringTaskRules.WhyNotASchedule"/> refuses about the
    /// schedule, passed through in that rule's own words rather than restated
    /// here.
    ///
    /// Public because the unit suite proves it; nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotATask(CreateTaskRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is missing, and a task is named by what has to be done: give the "
                + "task a name.";
        }

        if (request.Status is not null && WhyNotAStatus(request.Status) is { } whyNotAStatus)
        {
            return whyNotAStatus;
        }

        return RecurringTaskRules.WhyNotASchedule(
            request.DueDate, request.RepeatingInterval, request.UntilDate);
    }

    /// <summary>
    /// Why an offered status is not one a task may be set to, or null when it
    /// is.
    /// </summary>
    /// <remarks>
    /// The rule <see cref="SetStatus"/> turns into a 400 and
    /// <see cref="WhyNotATask"/> asks of a status given when a task is
    /// recorded, so the one vocabulary answers both. Pure, and public because
    /// the unit suite proves it.
    /// </remarks>
    public static string? WhyNotAStatus(string? offeredCode) =>
        offeredCode is OpenStatus or CompleteStatus
            ? null
            : $"status '{offeredCode}' is not a state this tier works a task in. Give "
                + $"{OpenStatus} or {CompleteStatus}.";

    /// <summary>
    /// Registers what the task endpoints need of the container. Kept here with
    /// the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddTasks(this IServiceCollection services) =>
        services.AddScoped<TaskQueries>();

    public static IEndpointRouteBuilder MapTasks(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/tasks", CreateTask)
            .WithName("CreateTask")
            .WithSummary("Records a task against a case morbidity report, with its series where it repeats.");

        routes.MapGet("/api/person-conditions/{id:int}/tasks", GetReportTasks)
            .WithName("GetReportTasks")
            .WithSummary("A case morbidity report's tasks, by due date and paged.");

        routes.MapGet("/api/tasks", GetTasks)
            .WithName("GetTasks")
            .WithSummary("Tasks across every report, by due date and paged, optionally of one status.");

        routes.MapPut("/api/tasks/{id:int}/status", SetStatus)
            .WithName("SetTaskStatus")
            .WithSummary("Sets a task's status.");

        return routes;
    }

    private static async Task<Results<Created<TaskSeriesResponse>, ProblemHttpResult>> CreateTask(
        TaskQueries tasks,
        int id,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotATask(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The task asked for is not one this endpoint can record.");
        }

        if (!await tasks.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        var recorded = await tasks.Add(id, request, cancellationToken);

        return TypedResults.Created($"/api/tasks/{recorded.Tasks[0].Id}", recorded);
    }

    private static async Task<Results<Ok<PagedResult<TaskResponse>>, ProblemHttpResult>> GetReportTasks(
        TaskQueries tasks,
        int id,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return BadPage(why);
        }

        if (!await tasks.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(await tasks.Page(id, null, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<PagedResult<TaskResponse>>, ProblemHttpResult>> GetTasks(
        TaskQueries tasks,
        string? status = null,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return BadPage(why);
        }

        if (status is not null && WhyNotAStatus(status) is { } whyNotAStatus)
        {
            return TypedResults.Problem(
                detail: whyNotAStatus,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The tasks asked for are not ones this endpoint can list.");
        }

        return TypedResults.Ok(await tasks.Page(null, status, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<TaskResponse>, ProblemHttpResult>> SetStatus(
        TaskQueries tasks,
        int id,
        UpdateTaskStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (WhyNotAStatus(request.Status) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The status asked for is not one a task may be set to.");
        }

        if (await tasks.SetStatus(id, request.Status, cancellationToken) is not { } set)
        {
            return TypedResults.Problem(
                detail: $"No task has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such task.");
        }

        return TypedResults.Ok(set);
    }

    private static ProblemHttpResult BadPage(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The page asked for is not one this endpoint can return.");

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// The task queries and the recording, and the only thing in this tier that
/// holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExposureQueries</c> is: it exists only to serve
/// <see cref="TaskEndpoints"/>, and it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class TaskQueries(SurvsysDbContext database)
{
    /// <summary>
    /// Whether a report has that id — false is the answer the endpoint turns
    /// into a 404, because the report is the resource the route names.
    /// </summary>
    public Task<bool> HasReport(int reportId, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == reportId, cancellationToken);

    /// <summary>
    /// Records the task against the report — and, where it repeats, one task
    /// per date of its series — and answers with what was written.
    /// </summary>
    /// <remarks>
    /// The schedule is expanded by <see cref="RecurringTaskRules.DueDates"/>,
    /// which <see cref="TaskEndpoints.WhyNotATask"/> has already established
    /// can be expanded. The first task is saved alone so the database assigns
    /// the id the rest of the series points at through
    /// <c>repeating_task_id</c>; the others are saved together. Both saves are
    /// inside one transaction, so a series either all lands or none does —
    /// a half-written series would be a schedule nobody asked for and nothing
    /// re-expands.
    ///
    /// Every task of the series carries the interval and the until date as
    /// recorded, so a reader of one task can see it is one of several and on
    /// what schedule.
    /// </remarks>
    public async Task<TaskSeriesResponse> Add(
        int reportId,
        CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        var dates = Dates(request);

        var raised = DateTimeOffset.UtcNow;

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var first = Of(reportId, request, dates[0], null, raised);

        database.Tasks.Add(first);
        await database.SaveChangesAsync(cancellationToken);

        var series = new List<CaseTask> { first };

        foreach (var date in dates.Skip(1))
        {
            var repeat = Of(reportId, request, date, first.Id, raised);

            database.Tasks.Add(repeat);
            series.Add(repeat);
        }

        if (series.Count > 1)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new TaskSeriesResponse([.. series.Select(TaskEndpoints.Respond)]);
    }

    /// <summary>
    /// One page of tasks — of one report where a report is named, and of every
    /// report where none is, which is what the task list screen reads — of one
    /// status where one is named.
    /// </summary>
    /// <remarks>
    /// By due date, with the undated last, which is the order a list of work
    /// is read in; then by id, so the order is total and a task cannot appear
    /// on two pages or on neither. One method and not two, because the two
    /// lists differ only in whether the report is filtered on: a second copy
    /// of the ordering would be a second ordering the moment one was changed.
    /// </remarks>
    public async Task<PagedResult<TaskResponse>> Page(
        int? reportId,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var tasks = database.Tasks.AsQueryable();

        if (reportId is { } report)
        {
            tasks = tasks.Where(task => task.PersonConditionId == report);
        }

        if (status is not null)
        {
            tasks = tasks.Where(task => task.Status == status);
        }

        var total = await tasks.CountAsync(cancellationToken);

        var rows = await tasks
            .OrderBy(task => task.DueDate == null)
            .ThenBy(task => task.DueDate)
            .ThenBy(task => task.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(TaskEndpoints.TaskToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// Sets a task's status and the moment it was changed, and answers with
    /// the task as it now stands, or null where no task has that id.
    /// </summary>
    public async Task<TaskResponse?> SetStatus(
        int taskId,
        string? status,
        CancellationToken cancellationToken)
    {
        var task = await database.Tasks
            .SingleOrDefaultAsync(candidate => candidate.Id == taskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        task.Status = status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await database.SaveChangesAsync(cancellationToken);

        return TaskEndpoints.Respond(task);
    }

    /// <summary>
    /// The dates the recording writes a task on: the series where the task
    /// repeats, the due date alone where it does not, and one undated task
    /// where no due date was given.
    /// </summary>
    /// <remarks>
    /// Nullable dates, because a task may be raised with no due date at all —
    /// the schedule rule refuses a repeating interval without one, so an
    /// undated task is never a series.
    /// </remarks>
    private static IReadOnlyList<DateOnly?> Dates(CreateTaskRequest request)
    {
        if (request.DueDate is not { } firstDue)
        {
            return [null];
        }

        var interval = RecurringTaskRules.Read(request.RepeatingInterval);

        return
        [
            .. RecurringTaskRules.DueDates(firstDue, interval, request.UntilDate)
                .Select(date => (DateOnly?)date),
        ];
    }

    /// <summary>
    /// One task of the recording, due on the given date and pointing at the
    /// first of its series where it is not the first.
    /// </summary>
    /// <remarks>
    /// The report is the route's and the moment is the clock's, as a note's
    /// are; every task of a series is raised at the same moment, so the
    /// caller passes it in rather than each task reading the clock again.
    /// </remarks>
    private static CaseTask Of(
        int reportId,
        CreateTaskRequest request,
        DateOnly? dueDate,
        int? repeatingTaskId,
        DateTimeOffset raised) =>
        new()
        {
            PersonConditionId = reportId,
            Name = request.Name,
            Notes = request.Notes,
            Priority = request.Priority,
            DueDate = dueDate,
            UserId = request.UserId,
            CategoryId = request.CategoryId,
            Status = request.Status,
            RepeatingTaskId = repeatingTaskId,
            RepeatingInterval = request.RepeatingInterval,
            UntilDate = request.UntilDate,
            CreatedAt = raised,
        };
}
