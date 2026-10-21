using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/notification-queue</c>: the outbound CDC notification queue,
/// paged, oldest first. <c>PATCH /api/notification-queue/{id}/hold</c>: raises
/// or lowers a queued case's hold. <c>POST /api/notification-queue/process</c>:
/// builds, sends and logs every released case now. <c>DELETE</c> removes an
/// entry, <c>.../retract</c> queues a retraction, and <c>.../records</c> and
/// <c>.../mmwr-range</c> queue cases by record number or MMWR range.
/// </summary>
/// <remarks>
/// The read side of the queue the auto-queue fills: <c>CaseNotificationQueue</c>
/// upserts an entry as a case changes, and this slice shows the entries and
/// lets a reviewer hold a case back from export or release it, which is the
/// hold-and-approve review the entity left to a later session's queue screen.
///
/// The handlers are handed <see cref="NotificationQueueQueries"/> and not the
/// <c>DbContext</c>, because the architecture test lets no <c>Survsys.Data</c>
/// type into an endpoint signature at all. A row names the patient and the
/// condition rather than their ids: unlike the work queue, the notification
/// queue screen holds no catalogue to resolve them from, so the entry is joined
/// to its case here and the names are read in the one query.
/// </remarks>
public static class NotificationQueueEndpoints
{
    /// <summary>
    /// Registers what the notification-queue endpoints need of the container.
    /// Kept here with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddNotificationQueue(this IServiceCollection services) =>
        services
            .AddScoped<NotificationQueueQueries>()
            .AddScoped<CaseNotificationQueue>();

    public static IEndpointRouteBuilder MapNotificationQueue(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/notification-queue", GetNotificationQueue)
            .WithName("GetNotificationQueue")
            .WithSummary("The outbound CDC notification queue, paged and oldest first.");

        routes.MapPatch("/api/notification-queue/{personConditionId:int}/hold", SetNotificationHold)
            .WithName("SetNotificationHold")
            .WithSummary("Raises or lowers a queued case's hold on export.");

        routes.MapPost("/api/notification-queue/process", ProcessNotificationQueue)
            .WithName("ProcessNotificationQueue")
            .WithSummary("Builds, sends and logs every released queued case now, as the queue worker's "
                + "pass does.");

        routes.MapDelete("/api/notification-queue/{personConditionId:int}", RemoveFromNotificationQueue)
            .WithName("RemoveFromNotificationQueue")
            .WithSummary("Takes a case off the notification queue, so it is not exported.");

        routes.MapPost("/api/notification-queue/{personConditionId:int}/retract", RetractNotification)
            .WithName("RetractNotification")
            .WithSummary("Queues a retraction of a notification CDC accepted, sent with NOT118 = X.");

        routes.MapPost("/api/notification-queue/records", ExportRecords)
            .WithName("ExportNotificationRecords")
            .WithSummary("Queues the cases with the record numbers given for CDC notification.");

        routes.MapPost("/api/notification-queue/mmwr-range", ExportMmwrRange)
            .WithName("ExportNotificationMmwrRange")
            .WithSummary("Queues the morbidity cases of an MMWR range whose print criteria pass for CDC "
                + "notification.");

        routes.MapPost("/api/person-conditions/{id:int}/notification-queue", QueueCaseForNotification)
            .WithName("QueueCaseForNotification")
            .WithSummary("Queues a case for CDC notification by hand, as a case change does "
                + "automatically.");

        return routes;
    }

    private static async Task<Results<Ok<PagedResult<NotificationQueueItemResponse>>, ProblemHttpResult>> GetNotificationQueue(
        NotificationQueueQueries queue,
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

        return TypedResults.Ok(await queue.Page(page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<NotificationQueueItemResponse>, ProblemHttpResult>> SetNotificationHold(
        NotificationQueueQueries queue,
        int personConditionId,
        SetNotificationHoldRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await queue.SetHold(personConditionId, request.Hold, cancellationToken) is not { } row)
        {
            return TypedResults.Problem(
                detail: $"No case with id {personConditionId} is in the notification queue.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such notification-queue entry.");
        }

        return TypedResults.Ok(row);
    }

    private static async Task<Ok<NotificationQueueProcessResponse>> ProcessNotificationQueue(
        NotificationQueueQueries queue,
        CancellationToken cancellationToken = default) =>
        TypedResults.Ok(new NotificationQueueProcessResponse(await queue.Process(cancellationToken)));

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveFromNotificationQueue(
        NotificationQueueQueries queue,
        int personConditionId,
        CancellationToken cancellationToken = default) =>
        await queue.Remove(personConditionId, cancellationToken)
            ? TypedResults.NoContent()
            : TypedResults.Problem(
                detail: $"No case with id {personConditionId} is in the notification queue.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such notification-queue entry.");

    private static async Task<Results<Ok<NotificationQueueItemResponse>, ProblemHttpResult>> RetractNotification(
        NotificationQueueQueries queue,
        int personConditionId,
        CancellationToken cancellationToken = default)
    {
        if (!await queue.WasAccepted(personConditionId, cancellationToken))
        {
            return TypedResults.Problem(
                detail: $"CDC has accepted no notification of case {personConditionId}, so there is none "
                    + "to retract.",
                statusCode: StatusCodes.Status409Conflict,
                title: "There is no sent notification to retract.");
        }

        return TypedResults.Ok(await queue.Retract(personConditionId, cancellationToken));
    }

    private static async Task<Ok<NotificationExportResponse>> ExportRecords(
        NotificationQueueQueries queue,
        ExportRecordsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return TypedResults.Ok(await queue.QueueRecords(request.RecordNumbers, cancellationToken));
    }

    private static async Task<Results<Ok<NotificationExportResponse>, ProblemHttpResult>> ExportMmwrRange(
        NotificationQueueQueries queue,
        ExportMmwrRangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StartWeek < 1 || request.EndWeek > 53 || request.StartWeek > request.EndWeek)
        {
            return TypedResults.Problem(
                detail: $"Weeks {request.StartWeek} to {request.EndWeek} are not an MMWR range: the start "
                    + "week is from 1, the end week up to 53, and the start is not after the end.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "The MMWR range asked for is not one this endpoint can export.");
        }

        return TypedResults.Ok(await queue.QueueMmwrRange(request, cancellationToken));
    }

    private static async Task<Results<Ok<NotificationQueueItemResponse>, ProblemHttpResult>> QueueCaseForNotification(
        NotificationQueueQueries queue,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await queue.Queue(id, cancellationToken) is not { } row)
        {
            return TypedResults.Problem(
                detail: $"No case has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such case.");
        }

        return TypedResults.Ok(row);
    }
}

/// <summary>
/// The notification-queue reads, the hold write and the manual queue action,
/// and the only thing in this tier that holds the <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>MessageQueueQueries</c> is: it exists only to serve
/// <see cref="NotificationQueueEndpoints"/>, and it is the named step between
/// an endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class NotificationQueueQueries(
    SurvsysDbContext database,
    CaseNotificationQueue notifications,
    NotificationMessageBuilder builder,
    CdcTransmitter transmitter,
    IOptions<NotificationQueueOptions> options)
{
    /// <summary>
    /// Runs the queue worker's pass now, under the configured processing id and
    /// queue, and answers with how many entries it processed.
    /// </summary>
    public Task<int> Process(CancellationToken cancellationToken) =>
        NotificationQueueService.Process(
            database, builder, transmitter, options.Value, TimeProvider.System, cancellationToken);

    /// <summary>
    /// One page of the queue, oldest first by queued time, each entry joined to
    /// its case for the record number, patient name and condition.
    /// </summary>
    /// <remarks>
    /// A total order, so a case cannot appear on two pages or on neither: the
    /// queued time breaks to the person-condition id, which is unique across
    /// the queue because a case has at most one entry. The ordering is on the
    /// entry's own columns inside the query and the row is constructed last:
    /// EF Core can order a query by a column but not by a member of a row it
    /// has already constructed, which is why the join and projection are
    /// written out here rather than composed over a shared projected query.
    /// The patient's name is composed last-first, falling back to the last name
    /// alone where no first name is on file.
    /// </remarks>
    public async Task<PagedResult<NotificationQueueItemResponse>> Page(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var total = await database.NotificationQueue.CountAsync(cancellationToken);

        var items = await (
            from entry in database.NotificationQueue
            join report in database.PersonConditions on entry.PersonConditionId equals report.Id
            join person in database.Persons on report.PersonId equals person.Id
            join condition in database.Conditions on report.ConditionId equals condition.Id
            orderby entry.QueuedAt, entry.PersonConditionId
            select new NotificationQueueItemResponse(
                entry.PersonConditionId,
                report.RecordNumber,
                person.FirstName == null || person.FirstName == ""
                    ? person.LastName
                    : person.LastName + ", " + person.FirstName,
                condition.Name,
                entry.QueuedAt,
                entry.Hold,
                entry.Retract))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<NotificationQueueItemResponse>(items, page, pageSize, total);
    }

    /// <summary>
    /// Raises or lowers the hold on the case's queue entry and answers with the
    /// updated row, or null where the case has no entry.
    /// </summary>
    /// <remarks>
    /// A null answer is the case not being in the queue, which is what lets the
    /// endpoint tell a 404 from a hold it set. The updated row is read back
    /// through <see cref="Row"/>, the same join the list uses, so one entry
    /// reads the same however it was changed.
    /// </remarks>
    public async Task<NotificationQueueItemResponse?> SetHold(
        int personConditionId,
        bool hold,
        CancellationToken cancellationToken)
    {
        var entry = await database.NotificationQueue.SingleOrDefaultAsync(
            queued => queued.PersonConditionId == personConditionId, cancellationToken);

        if (entry is null)
        {
            return null;
        }

        entry.Hold = hold;
        await database.SaveChangesAsync(cancellationToken);

        return await Row(personConditionId, cancellationToken);
    }

    /// <summary>
    /// Queues a case for CDC notification by hand, through the same auto-queue
    /// path a case change takes, and answers with the queued row — or null
    /// where no case has the id.
    /// </summary>
    /// <remarks>
    /// The enqueue is <see cref="CaseNotificationQueue"/>'s, the same upsert the
    /// write path stages on a case change: a case with no entry gets an unheld
    /// one, and a case already queued has its queued time refreshed. A save of
    /// its own here, because this is the whole of the unit of work rather than a
    /// step in a case write. A case that is not there is told apart before
    /// anything is staged, so a missing case is a 404 and not a foreign-key
    /// fault.
    /// </remarks>
    public async Task<NotificationQueueItemResponse?> Queue(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        if (!await database.PersonConditions.AnyAsync(
            report => report.Id == personConditionId, cancellationToken))
        {
            return null;
        }

        await notifications.EnqueueAsync(
            new CaseChanged(personConditionId, DateTime.UtcNow), cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return await Row(personConditionId, cancellationToken);
    }

    /// <summary>
    /// Takes the case's entry off the queue, and says whether there was one.
    /// </summary>
    public async Task<bool> Remove(int personConditionId, CancellationToken cancellationToken) =>
        await database.NotificationQueue
            .Where(entry => entry.PersonConditionId == personConditionId)
            .ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Whether CDC has accepted a notification of the case.</summary>
    public Task<bool> WasAccepted(int personConditionId, CancellationToken cancellationToken) =>
        database.CdcExportLog.AnyAsync(
            log => log.PersonConditionId == personConditionId
                && log.Outcome == NotificationQueueService.AcceptedOutcome,
            cancellationToken);

    /// <summary>
    /// Queues the case as a retraction, released, and answers with its row.
    /// </summary>
    /// <remarks>
    /// The upsert <see cref="CaseNotificationQueue"/> makes, and then the entry
    /// marked a retraction and its hold lowered, so a case already held is
    /// retracted on the next pass rather than waiting on review. The case is
    /// confirmed to have an accepted notification before this is called, so it
    /// is on file.
    /// </remarks>
    public async Task<NotificationQueueItemResponse> Retract(
        int personConditionId,
        CancellationToken cancellationToken)
    {
        await notifications.EnqueueAsync(new CaseChanged(personConditionId, DateTime.UtcNow), cancellationToken);
        var entry = database.NotificationQueue.Local.Single(queued => queued.PersonConditionId == personConditionId);
        entry.Retract = true;
        entry.Hold = false;
        await database.SaveChangesAsync(cancellationToken);

        return (await Row(personConditionId, cancellationToken))!;
    }

    /// <summary>
    /// Queues each case one of <paramref name="recordNumbers"/> names, through
    /// the ungated enqueue, as an explicit export is not weighed against the
    /// print criteria; answers with how many were queued and the numbers no
    /// case has.
    /// </summary>
    public async Task<NotificationExportResponse> QueueRecords(
        IEnumerable<string> recordNumbers,
        CancellationToken cancellationToken)
    {
        var asked = recordNumbers
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Select(number => number.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var found = await database.PersonConditions
            .Where(report => asked.Contains(report.RecordNumber))
            .Select(report => new { report.Id, report.RecordNumber })
            .ToListAsync(cancellationToken);

        await Enqueue(found.Select(report => report.Id), cancellationToken);

        var known = found.Select(report => report.RecordNumber).ToHashSet(StringComparer.Ordinal);
        return new NotificationExportResponse(found.Count, asked.Where(number => !known.Contains(number)).ToList());
    }

    /// <summary>
    /// Queues the morbidity cases of the MMWR range, of the one condition where
    /// it is given, whose case status their condition's print criteria hold —
    /// the legacy date-range export — and answers with how many were queued.
    /// </summary>
    public async Task<NotificationExportResponse> QueueMmwrRange(
        ExportMmwrRangeRequest request,
        CancellationToken cancellationToken)
    {
        var ids = await database.PersonConditions
            .Where(report => report.EventType == EventTypes.Morbidity
                && report.DeletedAt == null
                && report.MmwrYear == request.MmwrYear
                && report.MmwrWeek >= request.StartWeek
                && report.MmwrWeek <= request.EndWeek
                && (request.ConditionId == null || report.ConditionId == request.ConditionId)
                && database.ConditionPrintCriteria.Any(criterion =>
                    criterion.ConditionId == report.ConditionId && criterion.CaseStatus == report.CaseStatus))
            .Select(report => report.Id)
            .ToListAsync(cancellationToken);

        await Enqueue(ids, cancellationToken);

        return new NotificationExportResponse(ids.Count, []);
    }

    /// <summary>Stages the upsert of each case's entry under one queued time, and saves them together.</summary>
    private async Task Enqueue(IEnumerable<int> personConditionIds, CancellationToken cancellationToken)
    {
        var at = DateTime.UtcNow;
        foreach (var id in personConditionIds)
        {
            await notifications.EnqueueAsync(new CaseChanged(id, at), cancellationToken);
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The one queued case read back through the join the list uses, so an
    /// entry reads the same however it was changed. The filter sits on the
    /// entry's own column before the row is constructed — the one order EF Core
    /// can translate, as <see cref="Page"/> records.
    /// </summary>
    private Task<NotificationQueueItemResponse?> Row(
        int personConditionId,
        CancellationToken cancellationToken) =>
        (
            from queued in database.NotificationQueue
            where queued.PersonConditionId == personConditionId
            join report in database.PersonConditions on queued.PersonConditionId equals report.Id
            join person in database.Persons on report.PersonId equals person.Id
            join condition in database.Conditions on report.ConditionId equals condition.Id
            select new NotificationQueueItemResponse(
                queued.PersonConditionId,
                report.RecordNumber,
                person.FirstName == null || person.FirstName == ""
                    ? person.LastName
                    : person.LastName + ", " + person.FirstName,
                condition.Name,
                queued.QueuedAt,
                queued.Hold,
                queued.Retract))
            .SingleOrDefaultAsync(cancellationToken);
}
