using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/access-audit</c>: who opened which case morbidity report, and when,
/// newest first.
/// </summary>
/// <remarks>
/// The legacy <c>access_records</c>. A record is written by
/// <c>GET /api/person-conditions/{id}</c> each time it serves a report to a
/// signed-in user, through <see cref="AccessAuditQueries.Record"/>, and never
/// changed afterwards.
/// </remarks>
public static class AccessAuditEndpoints
{
    /// <summary>
    /// The wire shape of an access record and the user name of who it names.
    /// Public because the unit suite proves it.
    /// </summary>
    public static AccessRecordResponse ToResponse(AccessRecord record, string userName)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new AccessRecordResponse(record.Id, record.UserId, userName, record.PersonConditionId, record.AccessedAt);
    }

    /// <summary>
    /// Registers what the access audit, and the report endpoint that writes
    /// it, need of the container.
    /// </summary>
    public static IServiceCollection AddAccessAudit(this IServiceCollection services) =>
        services.AddScoped<AccessAuditQueries>();

    public static IEndpointRouteBuilder MapAccessAudit(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/access-audit", GetAccessAudit)
            .WithName("GetAccessAudit")
            .WithSummary("Who opened which case morbidity report, newest first, paged, "
                + "optionally for one report.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<AccessRecordResponse>>, ProblemHttpResult>> GetAccessAudit(
        AccessAuditQueries audit,
        int? personConditionId = null,
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

        return TypedResults.Ok(await audit.Page(personConditionId, page, pageSize, cancellationToken));
    }
}

/// <summary>
/// The access audit queries, and the only thing in this tier that writes an
/// access record.
/// </summary>
internal sealed class AccessAuditQueries(SurvsysDbContext database)
{
    /// <summary>Records that the user opened the report, now.</summary>
    public async Task Record(int userId, int personConditionId, CancellationToken cancellationToken)
    {
        database.AccessRecords.Add(new AccessRecord
        {
            UserId = userId,
            PersonConditionId = personConditionId,
            AccessedAt = DateTime.UtcNow,
        });
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One page of the records, newest first, for one report or for all.</summary>
    public async Task<PagedResult<AccessRecordResponse>> Page(
        int? personConditionId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var records = database.AccessRecords
            .Where(record => personConditionId == null || record.PersonConditionId == personConditionId);

        var total = await records.CountAsync(cancellationToken);
        var rows = await records
            .OrderByDescending(record => record.AccessedAt)
            .ThenByDescending(record => record.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(database.Users, record => record.UserId, user => user.Id, (record, user) => new { record, user.UserName })
            .ToListAsync(cancellationToken);

        return new PagedResult<AccessRecordResponse>(
            rows.Select(row => AccessAuditEndpoints.ToResponse(row.record, row.UserName)).ToList(),
            page,
            pageSize,
            total);
    }
}
