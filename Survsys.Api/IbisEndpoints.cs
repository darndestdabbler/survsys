using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>POST /api/person-conditions/{id}/ibis</c>: sends one case to IBIS, the
/// external epidemiology reporting system, and records it as sent when IBIS
/// accepts it.
/// </summary>
/// <remarks>
/// A case write, so <see cref="PrivilegeRules"/> holds it to
/// <see cref="Privileges.EditCases"/> without a word here. The transmission is
/// <see cref="IbisTransmitter"/>'s; this slice reads what IBIS is sent, and
/// writes the legacy <c>sent_to_ibis</c> and <c>ibis_updated_at</c> only on an
/// accepted transmission, so a case IBIS never took is never marked as taken.
/// A timeout is a <c>504</c> and a rejection or a fault a <c>502</c>, as
/// Problem Details carrying what the transmitter said: in each the case is
/// left as it was.
/// </remarks>
public static class IbisEndpoints
{
    /// <summary>
    /// Registers what the IBIS endpoint needs of the container. Kept here with
    /// the endpoint so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddIbis(this IServiceCollection services) =>
        services.AddScoped<IbisQueries>();

    public static IEndpointRouteBuilder MapIbis(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/person-conditions/{id:int}/ibis", SendToIbis)
            .WithName("SendToIbis")
            .WithSummary("Sends a case to IBIS, recording it as sent when IBIS accepts it.");

        return routes;
    }

    private static async Task<Results<Ok<IbisTransmissionResponse>, ProblemHttpResult>> SendToIbis(
        IbisQueries cases,
        IbisTransmitter transmitter,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await cases.Record(id, cancellationToken) is not { } record)
        {
            return TypedResults.Problem(
                detail: $"No case morbidity report has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case morbidity report.");
        }

        var result = await transmitter.Transmit(record, cancellationToken);

        switch (result.Status)
        {
            case IbisTransmissionStatus.Accepted:
                var today = DateOnly.FromDateTime(DateTimeOffset.Now.Date);
                await cases.MarkSent(id, today, cancellationToken);
                return TypedResults.Ok(new IbisTransmissionResponse(
                    result.Status.ToString(), result.Detail, result.AttemptedAt, true, today));

            case IbisTransmissionStatus.TimedOut:
                return TypedResults.Problem(
                    detail: result.Detail,
                    statusCode: StatusCodes.Status504GatewayTimeout,
                    title: "IBIS did not answer in time.");

            default:
                return TypedResults.Problem(
                    detail: result.Detail,
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "IBIS did not accept the case.");
        }
    }
}

/// <summary>
/// The IBIS reads and writes, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>ExportLogQueries</c> is: it exists only to serve
/// <see cref="IbisEndpoints"/>, and it is the named step between an endpoint
/// signature and <c>Survsys.Data</c> that the architecture test requires.
/// </remarks>
internal sealed class IbisQueries(SurvsysDbContext database)
{
    /// <summary>What IBIS is sent about one case, or null where no case has that id.</summary>
    public Task<IbisCaseRecord?> Record(int id, CancellationToken cancellationToken) =>
        (from report in database.PersonConditions
         where report.Id == id
         join condition in database.Conditions on report.ConditionId equals condition.Id
         join agency in database.Agencies on report.AgencyId equals agency.Id into agencies
         from agency in agencies.DefaultIfEmpty()
         select new IbisCaseRecord(
             report.RecordNumber,
             condition.Name,
             agency == null ? null : agency.Name,
             report.CaseStatus,
             report.EventOnsetDate,
             report.MmwrWeek,
             report.MmwrYear))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>Records the case as accepted by IBIS on <paramref name="on"/>.</summary>
    /// <remarks>
    /// Through the change tracker rather than a bulk update, so the save passes
    /// through the audit interceptor as every other change to a case does.
    /// </remarks>
    public async Task MarkSent(int id, DateOnly on, CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions.SingleAsync(report => report.Id == id, cancellationToken);
        report.SentToIbis = true;
        report.IbisUpdatedAt = on;
        await database.SaveChangesAsync(cancellationToken);
    }
}
