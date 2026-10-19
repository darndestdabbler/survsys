using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/reports/case-counts?groupBy=&amp;from=&amp;to=</c>: an ad-hoc
/// report counting case morbidity reports by one dimension the caller picks
/// from <see cref="ReportGroupings"/>, over an optional event onset range.
/// </summary>
/// <remarks>
/// Gated by <see cref="PrivilegeRules"/> to <see cref="Privileges.RunReports"/>,
/// as everything under <c>/api/reports</c> is. The counting is done in the
/// database, so a report reads one row per group rather than every case. The
/// range is inclusive at both ends, either end may be left off, and a report
/// with no event onset date falls outside any range that is given.
/// </remarks>
public static class ReportEndpoints
{
    /// <summary>
    /// Registers what the report endpoints need of the container. Kept here
    /// with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddReports(this IServiceCollection services) =>
        services.AddScoped<ReportQueries>();

    public static IEndpointRouteBuilder MapReports(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/reports/case-counts", GetCaseCounts)
            .WithName("GetCaseCounts")
            .WithSummary("Counts case morbidity reports by one grouping over an optional event onset range.");

        return routes;
    }

    /// <summary>
    /// Why these are not a report this endpoint can run — a grouping
    /// <see cref="ReportGroupings"/> does not list, or a range that ends before
    /// it starts — or null where they are one.
    /// </summary>
    public static string? WhyNotAReport(string? groupBy, DateOnly? from, DateOnly? to)
    {
        if (groupBy is null || !ReportGroupings.All.Contains(groupBy, StringComparer.Ordinal))
        {
            return $"groupBy must be one of {string.Join(", ", ReportGroupings.All)}.";
        }

        if (from > to)
        {
            return "from must not be after to.";
        }

        return null;
    }

    private static async Task<Results<Ok<IReadOnlyList<CaseCountResponse>>, ProblemHttpResult>> GetCaseCounts(
        ReportQueries reports,
        string? groupBy,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        if (WhyNotAReport(groupBy, from, to) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The report asked for is not one this endpoint can run.");
        }

        return TypedResults.Ok(await reports.CaseCounts(groupBy!, from, to, cancellationToken));
    }
}

/// <summary>
/// The report reads, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExportLogQueries</c> is: it exists only to serve
/// <see cref="ReportEndpoints"/>, and it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class ReportQueries(SurvsysDbContext database)
{
    /// <summary>
    /// The count of reports for each value of <paramref name="groupBy"/>, most
    /// first and then by value, over the event onset range given.
    /// </summary>
    public async Task<IReadOnlyList<CaseCountResponse>> CaseCounts(
        string groupBy,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var reports = database.PersonConditions.AsQueryable();

        if (from is { } start)
        {
            reports = reports.Where(report => report.EventOnsetDate >= start);
        }

        if (to is { } end)
        {
            reports = reports.Where(report => report.EventOnsetDate <= end);
        }

        // A workflow state left blank reads as the initial state, as
        // WorkflowRules.Read and the saved-view export read it.
        IQueryable<string?> groups = groupBy switch
        {
            ReportGroupings.Condition =>
                from report in reports
                join condition in database.Conditions on report.ConditionId equals condition.Id
                select (string?)condition.Name,
            ReportGroupings.Agency =>
                from report in reports
                join agency in database.Agencies on report.AgencyId equals agency.Id into agencies
                from agency in agencies.DefaultIfEmpty()
                select agency == null ? null : agency.Name,
            ReportGroupings.CaseStatus => reports.Select(report => report.CaseStatus),
            ReportGroupings.WorkflowState => reports.Select(report =>
                report.WorkflowState == null || report.WorkflowState.Trim() == ""
                    ? WorkflowRules.InitialCode
                    : report.WorkflowState),
            ReportGroupings.MmwrYear => reports.Select(report =>
                report.MmwrYear == null ? null : report.MmwrYear.Value.ToString()),
            _ => throw new ArgumentOutOfRangeException(
                nameof(groupBy), groupBy, "is not a grouping ReportGroupings lists."),
        };

        var counts = await groups
            .GroupBy(group => group)
            .Select(group => new { group.Key, Count = group.Count() })
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Key)
            .ToListAsync(cancellationToken);

        return counts.Select(row => new CaseCountResponse(row.Key, row.Count)).ToList();
    }
}
