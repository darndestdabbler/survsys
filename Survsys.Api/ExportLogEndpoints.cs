using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/export-log</c>: the CDC export log, paged and newest first.
/// <c>GET /api/export-log/{id}</c>: one export whole, with the message that was
/// generated for it.
/// </summary>
/// <remarks>
/// Read-only: the export log is an append-only history a reviewer reads, so
/// this slice only reads. The entity-to-wire mapping lives here, as the other
/// slices' does and for the same reason — <c>Survsys.Data</c> may not
/// reference <c>Survsys.Contracts</c> — and the handlers are handed
/// <see cref="ExportLogQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature at all.
///
/// An export reads the same in the list and on its own: the list returns the
/// <see cref="CdcExportLogItemResponse"/> summary, and the detail wraps that
/// same summary around the generated message the list leaves out because it is
/// unbounded.
/// </remarks>
public static class ExportLogEndpoints
{
    /// <summary>The projection from a stored log row to its list-summary wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list. The generated
    /// message is not here, because the summary does not carry it. Public
    /// because the detail read composes the same fields and a reader expects
    /// one shape.
    /// </remarks>
    public static readonly Expression<Func<CdcExportLogEntry, CdcExportLogItemResponse>> ItemToResponse =
        entry => new CdcExportLogItemResponse(
            entry.Id,
            entry.RecordNumber,
            entry.GuideId,
            entry.QueuedAt,
            entry.SentAt,
            entry.Outcome);

    /// <summary>
    /// Registers what the export-log endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddExportLog(this IServiceCollection services) =>
        services.AddScoped<ExportLogQueries>();

    public static IEndpointRouteBuilder MapExportLog(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/export-log", GetExportLog)
            .WithName("GetExportLog")
            .WithSummary("The CDC export log, paged and newest first.");

        routes.MapGet("/api/export-log/{id:int}", GetExportLogEntry)
            .WithName("GetExportLogEntry")
            .WithSummary("One CDC export, with the message that was generated for it.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<CdcExportLogItemResponse>>, ProblemHttpResult>> GetExportLog(
        ExportLogQueries log,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (Paging.WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await log.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<CdcExportLogDetailResponse>, ProblemHttpResult>> GetExportLogEntry(
        ExportLogQueries log,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await log.Detail(id, cancellationToken) is not { } detail)
        {
            return TypedResults.Problem(
                detail: $"No export has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such export.");
        }

        return TypedResults.Ok(detail);
    }
}

/// <summary>
/// The export-log reads, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>MessageQueueQueries</c> is: it exists only to serve
/// <see cref="ExportLogEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class ExportLogQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of the log, newest first by queued time — a total order, so an
    /// export cannot appear on two pages or on neither: the queued time breaks
    /// to the id.
    /// </summary>
    public async Task<PagedResult<CdcExportLogItemResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.CdcExportLog.CountAsync(cancellationToken);

        var rows = await database.CdcExportLog
            .OrderByDescending(entry => entry.QueuedAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ExportLogEndpoints.ItemToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<CdcExportLogItemResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One export whole — its log summary and the message generated for it — or
    /// null where no export has that id.
    /// </summary>
    /// <remarks>
    /// The summary and the message are read in the one query: the filter sits
    /// on the id before the projection, so EF Core translates it, and the
    /// generated message is carried alongside the summary rather than read in a
    /// second trip.
    /// </remarks>
    public Task<CdcExportLogDetailResponse?> Detail(int id, CancellationToken cancellationToken) =>
        database.CdcExportLog
            .Where(entry => entry.Id == id)
            .Select(entry => new CdcExportLogDetailResponse(
                new CdcExportLogItemResponse(
                    entry.Id,
                    entry.RecordNumber,
                    entry.GuideId,
                    entry.QueuedAt,
                    entry.SentAt,
                    entry.Outcome),
                entry.GeneratedMessage))
            .SingleOrDefaultAsync(cancellationToken);
}
