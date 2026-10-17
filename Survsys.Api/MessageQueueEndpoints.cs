using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/staged-messages</c>: the inbound ELR intake queue, paged and
/// filterable by review state. <c>GET /api/staged-messages/{id}</c>: one staged
/// message whole, with the raw HL7 it arrived as.
/// </summary>
/// <remarks>
/// Read-only: the intake service fills this queue and a later session's review
/// screen works it, so this slice only reads. The entity-to-wire mapping lives
/// here, as the other slices' does and for the same reason — <c>Survsys.Data</c>
/// may not reference <c>Survsys.Contracts</c> — and the handlers are handed
/// <see cref="MessageQueueQueries"/> and not the <c>DbContext</c>, because the
/// architecture test lets no <c>Survsys.Data</c> type into an endpoint
/// signature at all.
///
/// A message reads the same in the list and on its own: the list returns the
/// <see cref="StagedMessageResponse"/> summary, and the detail wraps that same
/// summary around the raw text the list leaves out because it is large.
/// </remarks>
public static class MessageQueueEndpoints
{
    /// <summary>The projection from a stored message to its queue-summary wire shape.</summary>
    /// <remarks>
    /// An <see cref="Expression"/>, as the other slices' projections are: EF
    /// Core translates it into the <c>SELECT</c> of the list and the detail's
    /// summary, and a unit test compiles and invokes it to prove what it
    /// carries. Public because the unit suite proves it. The raw message is not
    /// here, because the summary does not carry it.
    /// </remarks>
    public static readonly Expression<Func<StagedMessage, StagedMessageResponse>> MessageToResponse =
        message => new StagedMessageResponse(
            message.Id,
            message.Source,
            message.ReceivedAt,
            message.ReviewStatus,
            message.QaFlagged,
            message.ExceptionDetail);

    /// <summary>
    /// Registers what the message-queue endpoints need of the container. Kept
    /// here with the endpoints so the slice's wiring is in one place.
    /// </summary>
    public static IServiceCollection AddMessageQueue(this IServiceCollection services) =>
        services.AddScoped<MessageQueueQueries>();

    public static IEndpointRouteBuilder MapMessageQueue(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/staged-messages", GetStagedMessages)
            .WithName("GetStagedMessages")
            .WithSummary("The inbound ELR intake queue, paged and filterable by review state.");

        routes.MapGet("/api/staged-messages/{id:int}", GetStagedMessage)
            .WithName("GetStagedMessage")
            .WithSummary("One staged message, with the raw HL7 it arrived as.");

        routes.MapPost("/api/staged-messages/{id:int}/case", PromoteStagedMessage)
            .WithName("PromoteStagedMessage")
            .WithSummary("Promotes a reviewed staged message to a case morbidity report and "
                + "raises the batch notification the promotion owes.");

        return routes;
    }

    /// <summary>
    /// Why a message in this review state may not be promoted to a case, or
    /// null when it may.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="PersonConditionEndpoints.WhyNotAReport"/> has: a
    /// pure function the unit suite proves over each state, with the endpoint
    /// turning the sentence into a 409 — a conflict with the message's state,
    /// not a bad request. A message in <see cref="MessageReviewStatus.Entry"/>
    /// or <see cref="MessageReviewStatus.Pending"/> is waiting for exactly this
    /// and passes; one in <see cref="MessageReviewStatus.Exception"/> never
    /// parsed, so there is nothing to promote; and one already in
    /// <see cref="MessageReviewStatus.Assigned"/> is already a case, so
    /// promoting it again would open a second report for the same message.
    ///
    /// The vocabulary is <c>MessageReviewStatusRules</c>'s, so a code it does
    /// not know is refused rather than treated as promotable — the state read
    /// off a row is always one it wrote, and anything else is a message that
    /// something outside the vocabulary put there.
    ///
    /// Public because the unit suite proves it. Nothing outside this file
    /// calls it.
    /// </remarks>
    public static string? WhyNotPromotable(string reviewStatus)
    {
        ArgumentNullException.ThrowIfNull(reviewStatus);

        return reviewStatus.Trim() switch
        {
            MessageReviewStatusRules.EntryCode or MessageReviewStatusRules.PendingCode => null,
            MessageReviewStatusRules.ExceptionCode =>
                "This message is held as an exception: it never parsed, so there is nothing "
                + "to promote. Resolve the exception before a case can be opened from it.",
            MessageReviewStatusRules.AssignedCode =>
                "This message has already been promoted to a case, so it cannot be promoted "
                + "again. Open the case it became rather than creating a second report.",
            _ =>
                $"'{reviewStatus}' is not a review state a message can be promoted from.",
        };
    }

    private static async Task<Results<Ok<PagedResult<StagedMessageResponse>>, ProblemHttpResult>> GetStagedMessages(
        MessageQueueQueries messages,
        string? status = null,
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

        return TypedResults.Ok(await messages.Page(status, page, pageSize, cancellationToken));
    }

    private static async Task<Results<Ok<StagedMessageDetailResponse>, ProblemHttpResult>> GetStagedMessage(
        MessageQueueQueries messages,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (await messages.Detail(id, cancellationToken) is not { } detail)
        {
            return TypedResults.Problem(
                detail: $"No staged message has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such staged message.");
        }

        return TypedResults.Ok(detail);
    }

    /// <summary>
    /// Promotes a reviewed staged message to a case morbidity report: the
    /// human-reviewed half of the legacy assignment, where a reviewer's
    /// confirmed decision — not the message on its own — opens the case.
    /// </summary>
    /// <remarks>
    /// The decision is validated the way a report opened directly is, by
    /// reusing <see cref="PersonConditionEndpoints.WhyNotAReport"/>, the agency
    /// lookup and <see cref="PersonConditionQueries.WhyNotOnFile"/>, so a
    /// promotion refuses exactly what a direct creation refuses and in the same
    /// words. What is promotion's own is the message: a message that is not
    /// there is a 404, and one whose state <see cref="WhyNotPromotable"/>
    /// refuses is a 409 against that state.
    ///
    /// The report is opened with the same <see cref="PersonConditionQueries.Add"/>
    /// a direct creation uses, so a promoted case is assigned a record number
    /// the one way record numbers are assigned. It is then that the message is
    /// marked promoted and the notification read back: the report write and the
    /// status transition are two saves, not one, which leaves the same narrow
    /// window the record-number race already does — a report could be opened
    /// and the message left unmarked if the second save fails. The window is
    /// accepted here for the reason it is there: a visible duplicate a reviewer
    /// can see beats a case silently lost, and the alternative, marking first,
    /// would lose the case rather than duplicate it.
    /// </remarks>
    private static async Task<Results<Created<PromoteMessageResponse>, ProblemHttpResult>> PromoteStagedMessage(
        MessageQueueQueries messages,
        PersonConditionQueries reports,
        AgencyQueries agencies,
        int id,
        PromoteMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await messages.StateOf(id, cancellationToken) is not { } state)
        {
            return TypedResults.Problem(
                detail: $"No staged message has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such staged message.");
        }

        if (WhyNotPromotable(state) is { } whyNot)
        {
            return TypedResults.Problem(
                detail: whyNot,
                statusCode: StatusCodes.Status409Conflict,
                title: "This message is not in a state it can be promoted from.");
        }

        var creation = new CreatePersonConditionRequest(
            request.PersonId,
            request.ConditionId,
            request.AgencyId,
            request.FirstReportedPhDate);

        if (PersonConditionEndpoints.WhyNotAReport(creation) is { } why)
        {
            return Refused(why);
        }

        if (request.AgencyId is { } agencyId && await agencies.Find(agencyId, cancellationToken) is null)
        {
            return Refused(AgencyEndpoints.NotAnAgency(agencyId));
        }

        if (await reports.WhyNotOnFile(creation, cancellationToken) is { } missing)
        {
            return Refused(missing);
        }

        var created = await reports.Add(creation, DateTimeOffset.Now, cancellationToken);
        var notification = await messages.MarkPromoted(
            id, created.RecordNumber, created.ConditionId, cancellationToken);

        return TypedResults.Created(
            $"/api/person-conditions/{created.Id}",
            new PromoteMessageResponse(created, notification));
    }

    /// <summary>
    /// A promotion decision the endpoint will not act on, as a 400 — the shape
    /// a case morbidity report's creation refuses one in, so a reviewer reads
    /// the same sentence whether they promoted or keyed it in.
    /// </summary>
    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The case this message would be promoted to is not one this endpoint can open.");
}

/// <summary>
/// The message-queue reads, and the only thing in this tier that holds the
/// <c>DbContext</c> for them.
/// </summary>
/// <remarks>
/// Internal, and in this file rather than its own, for the reason
/// <c>LabQueries</c> is: it exists only to serve
/// <see cref="MessageQueueEndpoints"/>, and it is the named step between an
/// endpoint signature and <c>Survsys.Data</c> that the architecture test
/// requires.
/// </remarks>
internal sealed class MessageQueueQueries(SurvsysDbContext database)
{
    /// <summary>
    /// One page of the queue, oldest first by id — a total order, so a message
    /// cannot appear on two pages or on neither — narrowed to one review state
    /// when a status is given.
    /// </summary>
    /// <remarks>
    /// The filter is an exact match on the stored review-state code; a status
    /// no message is in returns an empty page rather than an error, because the
    /// queue being empty for a state is an answer and not a bad request.
    /// </remarks>
    public async Task<PagedResult<StagedMessageResponse>> Page(
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var messages = database.StagedMessages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var code = status.Trim();
            messages = messages.Where(message => message.ReviewStatus == code);
        }

        var total = await messages.CountAsync(cancellationToken);

        var rows = await messages
            .OrderBy(message => message.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MessageQueueEndpoints.MessageToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResult<StagedMessageResponse>(rows, page, pageSize, total);
    }

    /// <summary>
    /// One message whole — its queue summary and the raw text it arrived as —
    /// or null where no message has that id.
    /// </summary>
    /// <remarks>
    /// The summary is read through the same projection the list uses, so one
    /// message reads the same either way; the raw text is read alongside it and
    /// wrapped around the summary. Two reads of one row rather than a projection
    /// that compiles the summary expression in the query, which EF Core cannot
    /// translate.
    /// </remarks>
    public async Task<StagedMessageDetailResponse?> Detail(int id, CancellationToken cancellationToken)
    {
        var summary = await database.StagedMessages
            .Where(message => message.Id == id)
            .Select(MessageQueueEndpoints.MessageToResponse)
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return null;
        }

        var raw = await database.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.RawMessage)
            .SingleAsync(cancellationToken);

        return new StagedMessageDetailResponse(summary, raw);
    }

    /// <summary>
    /// The review-state code a message sits in, or null where no message has
    /// that id.
    /// </summary>
    /// <remarks>
    /// A message's review status is never null, so a null answer is the
    /// message not being there — which is what lets the endpoint tell a 404
    /// from a state it must then judge. Only the code is read, because that is
    /// all the promotion decision turns on; the message is not loaded whole
    /// for a field.
    /// </remarks>
    public async Task<string?> StateOf(int id, CancellationToken cancellationToken) =>
        await database.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.ReviewStatus)
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Marks a promoted message as assigned to the case it became, and answers
    /// with the batch notification the promotion raises.
    /// </summary>
    /// <remarks>
    /// Called after the report is opened, so the message is known to exist and
    /// the condition is known to be on file — the creation would have refused
    /// otherwise. The transition to <see cref="MessageReviewStatus.Assigned"/>
    /// is the message leaving the review queue; it is a save of its own, the
    /// second of the promotion's two, for the reason the endpoint records. The
    /// notification is read after it: a line naming the record number and the
    /// condition, which is the batch notification reduced to what this phase
    /// surfaces rather than an e-mail it sends.
    /// </remarks>
    public async Task<string> MarkPromoted(
        int id,
        string recordNumber,
        int conditionId,
        CancellationToken cancellationToken)
    {
        var message = await database.StagedMessages.SingleAsync(
            staged => staged.Id == id, cancellationToken);

        message.ReviewStatus = MessageReviewStatusRules.AssignedCode;
        await database.SaveChangesAsync(cancellationToken);

        var conditionName = await database.Conditions
            .Where(condition => condition.Id == conditionId)
            .Select(condition => condition.Name)
            .SingleAsync(cancellationToken);

        return $"Case {recordNumber} was created for {conditionName}; a notification has "
            + "been raised to public health.";
    }
}
