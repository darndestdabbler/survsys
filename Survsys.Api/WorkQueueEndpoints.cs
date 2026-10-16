using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// The work queue and read-only sharing: <c>GET /api/work-queue</c>, which
/// lists an agency's reports, and the <c>/api/person-conditions/{id}/shares</c>
/// pair that shares a report read-only with another agency and lists who it is
/// shared with.
/// </summary>
/// <remarks>
/// One slice because the two belong together: a share is what puts one agency's
/// report into another agency's queue, so the queue reads the shares the share
/// endpoint writes. The handlers are handed <see cref="WorkQueueQueries"/> and
/// not the <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature at all.
///
/// Sharing here is read-only in intent and not in enforcement: this tier has no
/// authentication and checks no calling agency, so nothing stops a write
/// against a shared report. The queue surface honours the intent by offering no
/// actions on a shared-in row, and enforced per-agency authorization is a later
/// session's.
/// </remarks>
public static class WorkQueueEndpoints
{
    /// <summary>
    /// The projection from a stored report to a work-queue row, for the agency
    /// the queue is being read for.
    /// </summary>
    /// <remarks>
    /// A factory rather than a field because <see cref="WorkQueueItemResponse.Shared"/>
    /// is read against the agency asked about: a row is shared-in when the
    /// report's agency is not that agency, and owned when it is. An
    /// <see cref="Expression"/> so EF Core translates it into the
    /// <c>SELECT</c> list, with the agency a captured constant the query is run
    /// for; a unit test compiles and invokes it to prove the fields and the
    /// flag. The workflow code is read as the assigned-to-LHD default where the
    /// column is null, empty or whitespace — the same reading
    /// <see cref="WorkflowRules.Read(string?)"/> gives — so a row never carries
    /// an absent or blank state.
    ///
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static Expression<Func<PersonCondition, WorkQueueItemResponse>> ToResponse(int agencyId) =>
        report => new WorkQueueItemResponse(
            report.Id,
            report.RecordNumber,
            report.PersonId,
            report.ConditionId,
            report.AgencyId,
            report.WorkflowState == null || report.WorkflowState.Trim() == ""
                ? WorkflowRules.InitialCode
                : report.WorkflowState,
            report.CaseStatus,
            report.EventOnsetDate,
            report.AgencyId != agencyId);

    /// <summary>
    /// Why this is not an agency a report may be shared with, or null when it
    /// is one.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="PersonConditionEndpoints.WhyNotAnAssignment"/> has:
    /// a pure function the unit suite proves, with the endpoint turning the
    /// sentence into a 400. Only an agency identifier nothing can have is
    /// refused here; whether the agency and the report exist, whether the report
    /// is already shared, and whether the agency is the one investigating it all
    /// need the database.
    ///
    /// Public because the unit suite proves it. Nothing outside this file calls
    /// it.
    /// </remarks>
    public static string? WhyNotAShare(ShareReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.AgencyId <= 0)
        {
            return AgencyEndpoints.NotAnAgency(request.AgencyId);
        }

        return null;
    }

    /// <summary>
    /// Why this is not an agency the work queue may be read for, or null when
    /// it is one.
    /// </summary>
    /// <remarks>
    /// The work queue is an agency's, so an agency is required: a missing
    /// parameter arrives as zero and is named as required. Pure, as
    /// <see cref="WhyNotAShare"/> is, and the endpoint turns the sentence into a
    /// 400; an agency no agency on file has is the endpoint's to refuse, because
    /// that needs a query.
    /// </remarks>
    public static string? WhyNotAQueueAgency(int agencyId)
    {
        if (agencyId <= 0)
        {
            return "agencyId is required: the work queue is an agency's, so name the "
                + "agency to read it for with ?agencyId=, one GET /api/agencies lists.";
        }

        return null;
    }

    /// <summary>
    /// Registers what the work-queue and sharing endpoints need of the
    /// container. Kept here with the endpoints so the slice's wiring is in one
    /// place, and so that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddWorkQueue(this IServiceCollection services) =>
        services.AddScoped<WorkQueueQueries>();

    public static IEndpointRouteBuilder MapWorkQueue(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/work-queue", GetWorkQueue)
            .WithName("GetWorkQueue")
            .WithSummary("The case morbidity reports an agency investigates or has shared "
                + "with it, paged.");

        routes.MapPost("/api/person-conditions/{id:int}/shares", ShareReport)
            .WithName("SharePersonCondition")
            .WithSummary("Shares a case morbidity report read-only with another agency.");

        routes.MapGet("/api/person-conditions/{id:int}/shares", GetShares)
            .WithName("GetPersonConditionShares")
            .WithSummary("The agencies a case morbidity report is shared read-only with.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<WorkQueueItemResponse>>, ProblemHttpResult>> GetWorkQueue(
        WorkQueueQueries reports,
        AgencyQueries agencies,
        int agencyId = 0,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var whyAgency = WhyNotAQueueAgency(agencyId)
            ?? (await agencies.Find(agencyId, cancellationToken) is null
                ? AgencyEndpoints.NotAnAgency(agencyId)
                : null);

        if (whyAgency is not null)
        {
            return TypedResults.Problem(
                detail: whyAgency,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The agency the work queue is to be read for is not one this endpoint knows.");
        }

        if (Paging.WhyNotAPage(page, pageSize) is { } whyPage)
        {
            return TypedResults.Problem(
                detail: whyPage,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await reports.Queue(agencyId, page, pageSize, cancellationToken));
    }

    /// <remarks>
    /// An agency no agency on file has is a 400. Then the report
    /// itself: a report id that names no report is a 404, a report already
    /// shared with the agency or shared to the agency already investigating it
    /// is a 400 — a share adds a reader, and neither a second copy nor the
    /// owner reading its own report is one to add. The created share is named by
    /// the agency it shares with, at the shares collection.
    /// </remarks>
    private static async Task<Results<Created<AgencyResponse>, ProblemHttpResult>> ShareReport(
        WorkQueueQueries reports,
        AgencyQueries agencies,
        int id,
        ShareReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var why = WhyNotAShare(request);
        var agency = why is null ? await agencies.Find(request.AgencyId, cancellationToken) : null;

        if (agency is null)
        {
            return TypedResults.Problem(
                detail: why ?? AgencyEndpoints.NotAnAgency(request.AgencyId),
                statusCode: StatusCodes.Status400BadRequest,
                title: "The agency the report is to be shared with is not one this endpoint knows.");
        }

        switch (await reports.Share(id, request.AgencyId, cancellationToken))
        {
            case ShareResult.NoReport:
                return TypedResults.Problem(
                    detail: $"No case morbidity report has id {id}.",
                    statusCode: StatusCodes.Status404NotFound,
                    title: "There is no such case morbidity report.");

            case ShareResult.AgencyIsInvestigator:
                return TypedResults.Problem(
                    detail: $"agencyId {request.AgencyId} is the agency already investigating "
                        + "this report. Sharing adds another agency as a read-only reader; the "
                        + "investigating agency already sees it in its own queue.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "The report is already this agency's.");

            case ShareResult.AlreadyShared:
                return TypedResults.Problem(
                    detail: $"This report is already shared with agencyId {request.AgencyId}. "
                        + "A report is shared with an agency once.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "The report is already shared with this agency.");

            default:
                return TypedResults.Created(
                    $"/api/person-conditions/{id}/shares",
                    agency);
        }
    }

    private static async Task<Results<Ok<PagedResult<AgencyResponse>>, ProblemHttpResult>> GetShares(
        WorkQueueQueries reports,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await reports.SharesOf(id, cancellationToken) is not { } shares)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        return TypedResults.Ok(shares);
    }
}

/// <summary>
/// What <see cref="WorkQueueQueries.Share"/> made of a share request: the one
/// outcome the endpoint turns into a status.
/// </summary>
internal enum ShareResult
{
    /// <summary>No report has that id — a 404.</summary>
    NoReport,

    /// <summary>The agency is the one investigating the report — a 400.</summary>
    AgencyIsInvestigator,

    /// <summary>The report is already shared with that agency — a 400.</summary>
    AlreadyShared,

    /// <summary>The share was added.</summary>
    Added,
}

/// <summary>
/// The work-queue and sharing queries, and the only thing in this slice that
/// holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ConditionQueries</c> is: it exists only to serve
/// <see cref="WorkQueueEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> the architecture test requires.
/// </remarks>
internal sealed class WorkQueueQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of an agency's work queue: the reports it investigates together
    /// with the reports shared read-only to it, newest record number first.
    /// </summary>
    /// <remarks>
    /// The agency is validated before this is called. A report is in the queue
    /// when the agency investigates it or a share ties it to the agency; the
    /// <see cref="WorkQueueItemResponse.Shared"/> flag the projection sets tells
    /// the two apart. Ordered by record number descending and then by id, so a
    /// report sits on one page and the order is total.
    /// </remarks>
    public async Task<PagedResult<WorkQueueItemResponse>> Queue(
        int agencyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var inQueue = database.PersonConditions
            .Where(report => report.AgencyId == agencyId
                || database.PersonConditionShares.Any(
                    share => share.PersonConditionId == report.Id && share.AgencyId == agencyId));

        var total = await inQueue.CountAsync(cancellationToken);

        var rows = await inQueue
            .OrderByDescending(report => report.RecordNumber)
            .ThenBy(report => report.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(WorkQueueEndpoints.ToResponse(agencyId))
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkQueueItemResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// Shares a report read-only with an agency, answering what came of it.
    /// </summary>
    /// <remarks>
    /// The agency is validated before this is called; here are the three
    /// questions the database answers — the report exists, the agency is not
    /// already the investigator, and the share is not already there — and then
    /// the write. Two simultaneous shares of the same pair can both pass the
    /// check and race to insert; the unique index refuses the second, which
    /// surfaces unwrapped as a 500, as the record-number collision does.
    /// </remarks>
    public async Task<ShareResult> Share(int id, int agencyId, CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .Where(report => report.Id == id)
            .Select(report => new { report.AgencyId })
            .SingleOrDefaultAsync(cancellationToken);

        if (report is null)
        {
            return ShareResult.NoReport;
        }

        if (report.AgencyId == agencyId)
        {
            return ShareResult.AgencyIsInvestigator;
        }

        if (await database.PersonConditionShares.AnyAsync(
            share => share.PersonConditionId == id && share.AgencyId == agencyId, cancellationToken))
        {
            return ShareResult.AlreadyShared;
        }

        database.PersonConditionShares.Add(
            new PersonConditionShare { PersonConditionId = id, AgencyId = agencyId });
        await database.SaveChangesAsync(cancellationToken);

        return ShareResult.Added;
    }

    /// <summary>
    /// The agencies a report is shared with, in name order, or null where no
    /// report has that id — the answer the endpoint turns into a 404.
    /// </summary>
    /// <remarks>
    /// The shares are joined to the agencies table for their names; a share
    /// whose agency id no agency has is dropped rather than carried as a
    /// nameless row, which cannot happen for a share written through this tier —
    /// the share column has no foreign key, so it is not worth trusting a reader
    /// to. One page holds them all, as the agencies endpoint's does, because a
    /// report's shares are a handful and not a collection to page.
    /// </remarks>
    public async Task<PagedResult<AgencyResponse>?> SharesOf(int id, CancellationToken cancellationToken)
    {
        if (!await database.PersonConditions.AnyAsync(report => report.Id == id, cancellationToken))
        {
            return null;
        }

        var agencies = await database.PersonConditionShares
            .Where(share => share.PersonConditionId == id)
            .Join(database.Agencies, share => share.AgencyId, agency => agency.Id, (_, agency) => agency)
            .OrderBy(agency => agency.Name)
            .ThenBy(agency => agency.Id)
            .Select(AgencyEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<AgencyResponse>(
            agencies, Paging.DefaultPage, agencies.Count, agencies.Count);
    }
}
