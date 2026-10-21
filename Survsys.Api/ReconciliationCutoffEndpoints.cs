using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET</c> and <c>PUT /api/reconciliation-cutoff</c>: the MMWR boundary of
/// CDC reconciliation. <c>GET /api/reconciliation-cutoff/totals</c>: the
/// verification totals it bounds.
/// </summary>
/// <remarks>
/// The legacy <c>reconciliationCutoff</c> request and the verification query
/// <c>../nedss/docs/business-logic/NMI_EXPORT_PIPELINE.md</c> records: the
/// cases sent to CDC, whose status their condition's print criteria hold,
/// counted by condition and MMWR year where the cutoff counts their MMWR
/// position. A case is sent once an export of it is logged accepted. The
/// totals are read here and nowhere written out.
/// </remarks>
public static class ReconciliationCutoffEndpoints
{
    /// <summary>
    /// Registers what the reconciliation-cutoff endpoints need of the container.
    /// Kept here with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddReconciliationCutoff(this IServiceCollection services) =>
        services.AddScoped<ReconciliationCutoffQueries>();

    public static IEndpointRouteBuilder MapReconciliationCutoff(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/reconciliation-cutoff", GetCutoff)
            .WithName("GetReconciliationCutoff")
            .WithSummary("The stored MMWR boundary of CDC reconciliation.");

        routes.MapPut("/api/reconciliation-cutoff", SetCutoff)
            .WithName("SetReconciliationCutoff")
            .WithSummary("Stores the MMWR boundary of CDC reconciliation.");

        routes.MapGet("/api/reconciliation-cutoff/totals", GetTotals)
            .WithName("GetReconciliationTotals")
            .WithSummary("The cases CDC accepted that the cutoff counts, by condition and MMWR year.");

        return routes;
    }

    private static async Task<Results<Ok<ReconciliationCutoffResponse>, ProblemHttpResult>> GetCutoff(
        ReconciliationCutoffQueries cutoffs,
        CancellationToken cancellationToken = default)
    {
        if (await cutoffs.Find(cancellationToken) is not { } cutoff)
        {
            return TypedResults.Problem(
                detail: "No reconciliation cutoff has been set.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no reconciliation cutoff.");
        }

        return TypedResults.Ok(cutoff);
    }

    private static async Task<Results<Ok<ReconciliationCutoffResponse>, ProblemHttpResult>> SetCutoff(
        ReconciliationCutoffQueries cutoffs,
        ReconciliationCutoffRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.MmwrWeek is < 1 or > 53)
        {
            return TypedResults.Problem(
                detail: $"Week {request.MmwrWeek} is not an MMWR week: an MMWR week is from 1 to 53.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The reconciliation cutoff asked for is not one this endpoint can store.");
        }

        return TypedResults.Ok(await cutoffs.Set(request, cancellationToken));
    }

    private static async Task<Ok<IReadOnlyList<ReconciliationTotalResponse>>> GetTotals(
        ReconciliationCutoffQueries cutoffs,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(await cutoffs.Totals(cancellationToken));
}

/// <summary>
/// The reconciliation-cutoff reads and write, and the only thing in this tier
/// that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>NotificationQueueQueries</c> is: it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class ReconciliationCutoffQueries(SurvsysDbContext database)
{
    /// <summary>The stored cutoff, or null where none is set.</summary>
    public Task<ReconciliationCutoffResponse?> Find(CancellationToken cancellationToken) =>
        database.ReconciliationCutoffs
            .OrderBy(cutoff => cutoff.Id)
            .Select(cutoff => new ReconciliationCutoffResponse(cutoff.MmwrYear, cutoff.MmwrWeek))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Stores the cutoff over the one row, adding it where there is none.</summary>
    public async Task<ReconciliationCutoffResponse> Set(
        ReconciliationCutoffRequest request,
        CancellationToken cancellationToken)
    {
        var stored = await database.ReconciliationCutoffs.OrderBy(cutoff => cutoff.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (stored is null)
        {
            stored = new ReconciliationCutoffSetting();
            database.ReconciliationCutoffs.Add(stored);
        }

        stored.MmwrYear = request.MmwrYear;
        stored.MmwrWeek = request.MmwrWeek;
        await database.SaveChangesAsync(cancellationToken);

        return new ReconciliationCutoffResponse(stored.MmwrYear, stored.MmwrWeek);
    }

    /// <summary>
    /// The verification totals: by condition and MMWR year, the morbidity cases
    /// with an accepted export whose status their print criteria hold and whose
    /// MMWR position the stored cutoff counts — every such case where no cutoff
    /// is set — ordered by CDC code, then condition, then year.
    /// </summary>
    /// <remarks>
    /// The candidate cases are read and then weighed in memory, because the
    /// boundary rule is <see cref="ReconciliationCutoff.Counts"/>'s and not a
    /// query's to restate.
    /// </remarks>
    public async Task<IReadOnlyList<ReconciliationTotalResponse>> Totals(CancellationToken cancellationToken)
    {
        var cutoff = (await database.ReconciliationCutoffs.OrderBy(stored => stored.Id)
            .FirstOrDefaultAsync(cancellationToken))?.ToCutoff();

        var cases = await (
            from report in database.PersonConditions
            join condition in database.Conditions on report.ConditionId equals condition.Id
            where report.EventType == EventTypes.Morbidity
                && report.DeletedAt == null
                && report.MmwrYear != null
                && report.MmwrWeek != null
                && database.CdcExportLog.Any(log =>
                    log.PersonConditionId == report.Id && log.Outcome == NotificationQueueService.AcceptedOutcome)
                && database.ConditionPrintCriteria.Any(criterion =>
                    criterion.ConditionId == report.ConditionId && criterion.CaseStatus == report.CaseStatus)
            select new { condition.Name, condition.CdcCode, Year = report.MmwrYear!.Value, Week = report.MmwrWeek!.Value })
            .ToListAsync(cancellationToken);

        return cases
            .Where(item => cutoff is not { } boundary || boundary.Counts(new MmwrWeek(item.Week, item.Year)))
            .GroupBy(item => (item.Name, item.CdcCode, item.Year))
            .Select(group => new ReconciliationTotalResponse(group.Key.Name, group.Key.CdcCode, group.Key.Year, group.Count()))
            .OrderBy(total => total.CdcCode, StringComparer.Ordinal)
            .ThenBy(total => total.Condition, StringComparer.Ordinal)
            .ThenBy(total => total.MmwrYear)
            .ToList();
    }
}
