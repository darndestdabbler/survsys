using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/conditions</c>: the conditions reference table, paged.
/// </summary>
/// <remarks>
/// The entity-to-wire mapping lives here because <c>Survsys.Data</c> may not
/// reference <c>Survsys.Contracts</c> — persistence must not know the wire
/// shapes — which leaves the translation to the tier that owns the wire.
///
/// The handler is handed <see cref="ConditionQueries"/> and not the
/// <c>DbContext</c>, because the architecture test in <c>Survsys.UnitTests</c>
/// lets no <c>Survsys.Data</c> type into an endpoint signature at all. That is
/// stricter than "no entity on the wire" and deliberately so: the database
/// stays one step behind a type this tier owns, and the query that reaches it
/// is a thing with a name rather than a lambda in a route registration.
/// </remarks>
public static class ConditionEndpoints
{
    /// <summary>The page returned when a caller asks for no particular one.</summary>
    public const int DefaultPage = 1;

    /// <summary>The page size used when a caller asks for no particular one.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>
    /// The largest page this endpoint will return. A caller that wants more
    /// rows asks for another page; it may not ask for an unbounded one.
    /// </summary>
    public const int MaxPageSize = 200;

    /// <summary>
    /// The projection from the stored row to the wire shape — four of the
    /// table's eight columns.
    /// </summary>
    /// <remarks>
    /// An <see cref="Expression"/> rather than a method, so that it is the one
    /// mapping in both directions it has to work: EF Core translates it into
    /// the <c>SELECT</c> list, so the three lead-in prose columns are never
    /// read out of the database at all, and a unit test compiles and invokes
    /// the same expression to prove what it carries. A method would have made
    /// the test exercise something the query did not use.
    ///
    /// Public because the unit suite proves it. Nothing else calls it.
    /// </remarks>
    public static readonly Expression<Func<Condition, ConditionResponse>> ToResponse =
        condition => new ConditionResponse(
            condition.Id,
            condition.Name,
            condition.CdcCode,
            condition.Active);

    /// <summary>
    /// Registers what the conditions endpoint needs of the container. Kept
    /// here with the endpoint so the slice's wiring is in one place, and so
    /// that <c>Program.cs</c> need not name an internal type.
    /// </summary>
    public static IServiceCollection AddConditions(this IServiceCollection services) =>
        services.AddScoped<ConditionQueries>();

    public static IEndpointRouteBuilder MapConditions(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/conditions", GetConditions)
            .WithName("GetConditions")
            .WithSummary("The conditions a case can be reported for, paged and ordered by name.");

        return routes;
    }

    /// <summary>
    /// Why the requested page is not a page this endpoint can return, or null
    /// when it is.
    /// </summary>
    /// <remarks>
    /// Pure, and public because the unit suite proves the bounds without
    /// starting a host. The last clause is the one that is easy to forget:
    /// <c>Skip</c> takes an <c>int</c>, so a page number large enough to
    /// overflow <c>(page - 1) * pageSize</c> would otherwise reach the query
    /// as a negative skip rather than as a bad request.
    /// </remarks>
    public static string? WhyNotAPage(int page, int pageSize)
    {
        if (page < 1)
        {
            return $"page must be 1 or greater, and {page} was asked for.";
        }

        if (pageSize < 1)
        {
            return $"pageSize must be 1 or greater, and {pageSize} was asked for.";
        }

        if (pageSize > MaxPageSize)
        {
            return $"pageSize must be {MaxPageSize} or fewer, and {pageSize} was asked for.";
        }

        if ((long)(page - 1) * pageSize > int.MaxValue)
        {
            return $"page {page} at a pageSize of {pageSize} is past the last row this "
                + "endpoint can address.";
        }

        return null;
    }

    private static async Task<Results<Ok<PagedResult<ConditionResponse>>, ProblemHttpResult>> GetConditions(
        ConditionQueries conditions,
        int page = DefaultPage,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAPage(page, pageSize) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The page asked for is not one this endpoint can return.");
        }

        return TypedResults.Ok(await conditions.Page(page, pageSize, cancellationToken));
    }
}

/// <summary>
/// The conditions query, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, because it exists only to
/// serve <see cref="ConditionEndpoints"/>: it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires, and it keeps the ordering and the paging arithmetic in one place
/// instead of in a route registration.
///
/// Nothing here translates a database failure into an HTTP concern. An EF Core
/// or SQL Server exception surfaces unwrapped and the exception handler turns
/// it into a 500 with Problem Details and a correlation id.
/// </remarks>
internal sealed class ConditionQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of conditions, ordered by name and then by id — by id as well
    /// because names are not unique in the legacy data, and an order that is
    /// not total lets a row appear on two pages or on neither.
    /// </summary>
    public async Task<PagedResult<ConditionResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.Conditions.CountAsync(cancellationToken);

        var rows = await database.Conditions
            .OrderBy(condition => condition.Name)
            .ThenBy(condition => condition.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ConditionEndpoints.ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<ConditionResponse>(rows, page, pageSize, total);
    }
}
