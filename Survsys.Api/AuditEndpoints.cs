using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/audit</c>: the change audit of one table, or of one row of it,
/// newest first, with who made each change and the row before and after.
/// </summary>
/// <remarks>
/// The audit is written by the <see cref="AuditInterceptor"/> on every save,
/// naming the user <see cref="HttpAuditUser"/> reads off the request. A case's
/// history is the audit of <c>person_conditions</c> at the case's id.
/// </remarks>
public static class AuditEndpoints
{
    /// <summary>
    /// The wire shape of an audit entry and the user name of who made it.
    /// Public because the unit suite proves it.
    /// </summary>
    public static AuditEntryResponse ToResponse(AuditEntry entry, string? userName)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new AuditEntryResponse(
            entry.Id,
            entry.TableName,
            entry.KeyValues,
            entry.Operation,
            entry.OccurredAt,
            entry.UserId,
            userName,
            entry.OldValues,
            entry.NewValues);
    }

    /// <summary>Why a request is not one this endpoint can answer, or null.</summary>
    public static string? WhyNotAnAuditRequest(string? table) =>
        string.IsNullOrWhiteSpace(table) ? "The table whose audit is wanted must be given." : null;

    /// <summary>
    /// Registers what the change audit needs of the container, and the user the
    /// interceptor names on every change.
    /// </summary>
    public static IServiceCollection AddAudit(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditUser, HttpAuditUser>();

        return services.AddScoped<AuditQueries>();
    }

    public static IEndpointRouteBuilder MapAudit(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/audit", GetAudit)
            .WithName("GetAudit")
            .WithSummary("The changes audited to one table, or to one row of it, newest first, paged.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<AuditEntryResponse>>, ProblemHttpResult>> GetAudit(
        AuditQueries audit,
        string? table = null,
        string? key = null,
        int page = Paging.DefaultPage,
        int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotAnAuditRequest(table) ?? Paging.WhyNotAPage(page, pageSize)) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The audit asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await audit.Page(table!, key, page, pageSize, cancellationToken));
    }
}

/// <summary>The signed-in caller of the current request, as the change audit names them.</summary>
internal sealed class HttpAuditUser(IHttpContextAccessor accessor) : IAuditUser
{
    public int? UserId =>
        accessor.HttpContext is { } http ? AccountEndpoints.UserIdOf(http.User) : null;
}

internal sealed class AuditQueries(SurvsysDbContext database)
{
    public async Task<PagedResult<AuditEntryResponse>> Page(
        string table,
        string? key,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var entries = database.AuditEntries
            .Where(entry => entry.TableName == table && (key == null || entry.KeyValues == key));

        var total = await entries.CountAsync(cancellationToken);
        var rows = await entries
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new
            {
                entry,
                UserName = database.Users
                    .Where(user => user.Id == entry.UserId)
                    .Select(user => user.UserName)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AuditEntryResponse>(
            rows.Select(row => AuditEndpoints.ToResponse(row.entry, row.UserName)).ToList(),
            page,
            pageSize,
            total);
    }
}
