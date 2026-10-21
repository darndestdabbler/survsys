using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/saved-views</c>: the caller's own saved views of the case
/// morbidity reports, and each view's reports exported as CSV.
/// </summary>
/// <remarks>
/// A view belongs to the user who saved it. Another user's view is answered
/// as a view that does not exist, a 404, so its name and filters are not
/// told to someone it does not belong to. A column outside
/// <see cref="ExportColumns.All"/> is a 400: an export can only write a field
/// it knows how to read.
/// </remarks>
public static class SavedViewEndpoints
{
    /// <summary>
    /// The wire shape of a saved view. Public because the unit suite proves it.
    /// </summary>
    public static SavedViewResponse ToResponse(SavedView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new SavedViewResponse(
            view.Id,
            view.Name,
            ColumnsOf(view),
            view.ConditionId,
            view.AgencyId,
            view.WorkflowState,
            view.CaseStatus,
            view.OnsetFrom,
            view.OnsetTo);
    }

    /// <summary>
    /// Why this is not a saved view this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    public static string? WhyNotASavedView(SaveSavedViewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a saved view is chosen by its name.";
        }

        if (request.Name.Length > 100)
        {
            return "name is longer than 100 characters.";
        }

        if (request.Columns is not { Count: > 0 } columns)
        {
            return "columns is required: an export needs at least one column.";
        }

        if (columns.FirstOrDefault(c => !ExportColumns.All.Contains(c, StringComparer.Ordinal)) is { } unknown)
        {
            return $"columns holds '{unknown}', which is not one of the columns ExportColumns lists.";
        }

        if (columns.GroupBy(c => c, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1) is { } repeated)
        {
            return $"columns holds '{repeated.Key}' more than once.";
        }

        if (request.WorkflowState is not null && !WorkflowRules.TryParse(request.WorkflowState, out _))
        {
            return $"workflowState '{request.WorkflowState}' is not a workflow state code.";
        }

        if (request.CaseStatus is not null
            && (string.IsNullOrWhiteSpace(request.CaseStatus) || !CaseStatusRules.IsAssignable(request.CaseStatus)))
        {
            return $"caseStatus '{request.CaseStatus}' is not a case status code.";
        }

        if (request.OnsetFrom > request.OnsetTo)
        {
            return "onsetFrom is after onsetTo, so the view would select nothing.";
        }

        return null;
    }

    /// <summary>A view's columns, in order, from the comma-separated column it is stored as.</summary>
    internal static IReadOnlyList<string> ColumnsOf(SavedView view) =>
        view.Columns.Split(',', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    /// Registers what the saved-view endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddSavedViews(this IServiceCollection services) =>
        services.AddScoped<SavedViewQueries>();

    public static IEndpointRouteBuilder MapSavedViews(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/saved-views", GetSavedViews)
            .WithName("GetSavedViews")
            .WithSummary("The caller's saved views, by name.");

        routes.MapPost("/api/saved-views", CreateSavedView)
            .WithName("CreateSavedView")
            .WithSummary("Saves a view for the caller.");

        routes.MapPut("/api/saved-views/{id:int}", UpdateSavedView)
            .WithName("UpdateSavedView")
            .WithSummary("Saves over one of the caller's views.");

        routes.MapDelete("/api/saved-views/{id:int}", DeleteSavedView)
            .WithName("DeleteSavedView")
            .WithSummary("Deletes one of the caller's views.");

        routes.MapGet("/api/saved-views/{id:int}/export", ExportSavedView)
            .WithName("ExportSavedView")
            .WithSummary("The reports one of the caller's views selects, as CSV.");

        return routes;
    }

    private static async Task<Ok<PagedResult<SavedViewResponse>>> GetSavedViews(
        SavedViewQueries views,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var all = await views.All(Caller(user), cancellationToken);

        return TypedResults.Ok(new PagedResult<SavedViewResponse>(all, Paging.DefaultPage, all.Count, all.Count));
    }

    private static async Task<Results<Created<SavedViewResponse>, ProblemHttpResult>> CreateSavedView(
        SavedViewQueries views,
        ClaimsPrincipal user,
        SaveSavedViewRequest request,
        CancellationToken cancellationToken = default)
    {
        var caller = Caller(user);

        if ((WhyNotASavedView(request) ?? await views.WhyNotKnown(caller, null, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        var created = await views.Add(caller, request, cancellationToken);

        return TypedResults.Created($"/api/saved-views/{created.Id}", created);
    }

    private static async Task<Results<Ok<SavedViewResponse>, ProblemHttpResult>> UpdateSavedView(
        SavedViewQueries views,
        ClaimsPrincipal user,
        int id,
        SaveSavedViewRequest request,
        CancellationToken cancellationToken = default)
    {
        var caller = Caller(user);

        if ((WhyNotASavedView(request) ?? await views.WhyNotKnown(caller, id, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        return await views.Update(caller, id, request, cancellationToken) is { } view
            ? TypedResults.Ok(view)
            : NotFound(id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteSavedView(
        SavedViewQueries views,
        ClaimsPrincipal user,
        int id,
        CancellationToken cancellationToken = default) =>
        await views.Delete(Caller(user), id, cancellationToken)
            ? TypedResults.NoContent()
            : NotFound(id);

    private static async Task<Results<ContentHttpResult, ProblemHttpResult>> ExportSavedView(
        SavedViewQueries views,
        AccessAuditQueries audit,
        ClaimsPrincipal user,
        int id,
        CancellationToken cancellationToken = default)
    {
        var caller = Caller(user);

        if (await views.Export(caller, id, cancellationToken) is not { } csv)
        {
            return NotFound(id);
        }

        await audit.Record(caller, null, "Export", $"saved view {id}", cancellationToken);

        return TypedResults.Text(csv, "text/csv; charset=utf-8");
    }

    /// <summary>
    /// The signed-in caller's id. The fallback policy has already refused a
    /// caller who is not signed in, so there is always one here.
    /// </summary>
    private static int Caller(ClaimsPrincipal user) =>
        AccountEndpoints.UserIdOf(user)
            ?? throw new InvalidOperationException("A saved-view request reached its endpoint without a signed-in user.");

    private static ProblemHttpResult NotFound(int id) =>
        TypedResults.Problem(
            detail: $"You have no saved view with id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such saved view.");

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The saved view asked for is not one this endpoint can save.");
}

/// <summary>
/// The saved-view queries, and the only thing in this tier that writes a
/// saved view. Every query is held to the caller's own views.
/// </summary>
internal sealed class SavedViewQueries(SurvsysDbContext database)
{
    /// <summary>The user's views, by name.</summary>
    public async Task<IReadOnlyList<SavedViewResponse>> All(int userId, CancellationToken cancellationToken) =>
        (await database.SavedViews
            .Where(view => view.UserId == userId)
            .OrderBy(view => view.Name)
            .ToListAsync(cancellationToken))
        .Select(SavedViewEndpoints.ToResponse)
        .ToList();

    /// <summary>Why the name is taken by another of the user's views, or null where it is not.</summary>
    public async Task<string?> WhyNotKnown(int userId, int? id, SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        return await database.SavedViews.AnyAsync(
                view => view.UserId == userId && view.Name == name && view.Id != id,
                cancellationToken)
            ? $"name '{name}' is already another of your saved views."
            : null;
    }

    /// <summary>Saves a new view for the user.</summary>
    public async Task<SavedViewResponse> Add(int userId, SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        var view = new SavedView { UserId = userId };
        database.SavedViews.Add(view);

        return await Save(view, request, cancellationToken);
    }

    /// <summary>Saves over the user's view, or answers null where the user has no view with that id.</summary>
    public async Task<SavedViewResponse?> Update(int userId, int id, SaveSavedViewRequest request, CancellationToken cancellationToken) =>
        await Find(userId, id, cancellationToken) is { } view
            ? await Save(view, request, cancellationToken)
            : null;

    /// <summary>Deletes the user's view, answering whether there was one.</summary>
    public async Task<bool> Delete(int userId, int id, CancellationToken cancellationToken) =>
        await database.SavedViews
            .Where(view => view.UserId == userId && view.Id == id)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>
    /// The CSV of the reports the user's view selects, in record-number order,
    /// or null where the user has no view with that id.
    /// </summary>
    public async Task<string?> Export(int userId, int id, CancellationToken cancellationToken)
    {
        if (await Find(userId, id, cancellationToken) is not { } view)
        {
            return null;
        }

        var reports = database.PersonConditions.AsQueryable();

        if (view.ConditionId is { } conditionId)
        {
            reports = reports.Where(report => report.ConditionId == conditionId);
        }

        if (view.AgencyId is { } agencyId)
        {
            reports = reports.Where(report => report.AgencyId == agencyId);
        }

        // A report with no stored workflow state is in the initial state, as
        // WorkflowRules.Read reads it.
        if (view.WorkflowState is { } state)
        {
            reports = state == WorkflowRules.InitialCode
                ? reports.Where(report => report.WorkflowState == null || report.WorkflowState.Trim() == "" || report.WorkflowState == state)
                : reports.Where(report => report.WorkflowState == state);
        }

        if (view.CaseStatus is { } status)
        {
            reports = reports.Where(report => report.CaseStatus == status);
        }

        if (view.OnsetFrom is { } from)
        {
            reports = reports.Where(report => report.EventOnsetDate >= from);
        }

        if (view.OnsetTo is { } to)
        {
            reports = reports.Where(report => report.EventOnsetDate <= to);
        }

        var rows = await (
            from report in reports
            join person in database.Persons on report.PersonId equals person.Id
            join condition in database.Conditions on report.ConditionId equals condition.Id
            join agency in database.Agencies on report.AgencyId equals agency.Id into agencies
            from agency in agencies.DefaultIfEmpty()
            orderby report.RecordNumber
            select new CaseExportRow(
                report.RecordNumber,
                person.LastName,
                person.FirstName,
                person.BirthDate,
                condition.Name,
                agency == null ? null : agency.Name,
                report.WorkflowState == null || report.WorkflowState.Trim() == ""
                    ? WorkflowRules.InitialCode
                    : report.WorkflowState,
                report.CaseStatus,
                report.EventOnsetDate,
                report.FirstReportedPhDate,
                report.MmwrWeek,
                report.MmwrYear,
                report.CreatedAt))
            .ToListAsync(cancellationToken);

        var columns = SavedViewEndpoints.ColumnsOf(view);

        return CaseExport.Csv(columns, rows.Select(row => CaseExport.Values(row, columns)));
    }

    private Task<SavedView?> Find(int userId, int id, CancellationToken cancellationToken) =>
        database.SavedViews.SingleOrDefaultAsync(view => view.UserId == userId && view.Id == id, cancellationToken);

    private async Task<SavedViewResponse> Save(SavedView view, SaveSavedViewRequest request, CancellationToken cancellationToken)
    {
        view.Name = request.Name.Trim();
        view.Columns = string.Join(',', request.Columns ?? []);
        view.ConditionId = request.ConditionId;
        view.AgencyId = request.AgencyId;
        view.WorkflowState = request.WorkflowState;
        view.CaseStatus = request.CaseStatus;
        view.OnsetFrom = request.OnsetFrom;
        view.OnsetTo = request.OnsetTo;
        await database.SaveChangesAsync(cancellationToken);

        return SavedViewEndpoints.ToResponse(view);
    }
}
