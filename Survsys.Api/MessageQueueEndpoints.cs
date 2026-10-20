using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/staged-messages</c>: the inbound ELR intake queue, paged and
/// filterable by review state. <c>GET /api/staged-messages/{id}</c>: one staged
/// message whole, with the raw HL7 it arrived as. Promotion of one message to
/// a case, and the move, delete and retry a reviewer takes on one or many.
/// </summary>
/// <remarks>
/// The intake service fills this queue and message processing works what it
/// can; what is here is the reviewer's half. The entity-to-wire mapping lives
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

        routes.MapPost("/api/staged-messages/move", MoveStagedMessages)
            .WithName("MoveStagedMessages")
            .WithSummary("Moves staged messages to another review state.");

        routes.MapPost("/api/staged-messages/delete", DeleteStagedMessages)
            .WithName("DeleteStagedMessages")
            .WithSummary("Deletes staged messages that have not become a case.");

        routes.MapPost("/api/staged-messages/retry", RetryStagedMessages)
            .WithName("RetryStagedMessages")
            .WithSummary("Translates held staged messages again against the vocabulary and rules on file.");

        return routes;
    }

    /// <summary>
    /// The review states a message can be promoted to a case from: entry,
    /// pending, semi-auto and qa.
    /// </summary>
    public static readonly IReadOnlyList<string> PromotableCodes =
    [
        MessageReviewStatusRules.EntryCode,
        MessageReviewStatusRules.PendingCode,
        MessageReviewStatusRules.SemiAutoCode,
        MessageReviewStatusRules.QaCode,
    ];

    /// <summary>The most messages one action takes.</summary>
    public const int MaxActionIds = 500;

    /// <summary>
    /// Why an action request names no messages it can act on, or null when it
    /// names between one and <see cref="MaxActionIds"/>.
    /// </summary>
    /// <remarks>Public because the unit suite proves it.</remarks>
    public static string? WhyNotAnAction(StagedMessageActionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Ids switch
        {
            null or { Count: 0 } => "Choose at least one message to act on.",
            { Count: > MaxActionIds } => $"No more than {MaxActionIds} messages can be acted on at once.",
            _ => null,
        };
    }

    /// <summary>
    /// Why a message in one review state may not be moved to another by hand,
    /// or null when it may.
    /// </summary>
    /// <remarks>
    /// Any state a message is in may be left for any other the vocabulary
    /// knows, except that <see cref="MessageReviewStatus.Assigned"/> is neither
    /// left nor entered by hand: a message is assigned by becoming a case, and
    /// an assigned one already is one. Public because the unit suite proves it.
    /// </remarks>
    public static string? WhyNotMovable(string from, string? to)
    {
        ArgumentNullException.ThrowIfNull(from);

        var target = to?.Trim();
        if (string.IsNullOrEmpty(target) || !MessageReviewStatusRules.Codes.Contains(target))
        {
            return $"'{to}' is not a review state a message can be moved to.";
        }

        if (target == MessageReviewStatusRules.AssignedCode)
        {
            return "A message is assigned by becoming a case, so it cannot be moved to assigned. "
                + "Promote it to a case instead.";
        }

        return from.Trim() == MessageReviewStatusRules.AssignedCode
            ? "This message has already become a case, so it stays assigned."
            : null;
    }

    /// <summary>
    /// Why a message in this review state may not be deleted, or null when it
    /// may: any message that has not become a case. Public because the unit
    /// suite proves it.
    /// </summary>
    public static string? WhyNotDeletable(string reviewStatus)
    {
        ArgumentNullException.ThrowIfNull(reviewStatus);

        return reviewStatus.Trim() == MessageReviewStatusRules.AssignedCode
            ? "This message has become a case, so it is kept as the record of what the case came from."
            : null;
    }

    /// <summary>
    /// Why a message may not be retried, or null when it may: one held as an
    /// exception, unprocessed, locked or as a graylist exception, which parsed
    /// when it was received.
    /// </summary>
    /// <remarks>
    /// A retry translates the message again against the vocabulary and rules
    /// on file now, which is what such a message waits on; it does not parse
    /// the raw text again, so one that never parsed has nothing to translate.
    /// Public because the unit suite proves it.
    /// </remarks>
    public static string? WhyNotRetryable(string reviewStatus, bool parsed)
    {
        ArgumentNullException.ThrowIfNull(reviewStatus);

        var retryable = reviewStatus.Trim() is MessageReviewStatusRules.ExceptionCode
            or MessageReviewStatusRules.UnprocessedCode
            or MessageReviewStatusRules.LockedCode
            or MessageReviewStatusRules.GrayExceptionCode;

        if (!retryable)
        {
            return $"A message in '{reviewStatus}' is not held, so there is nothing to retry.";
        }

        return parsed
            ? null
            : "This message never parsed, so there is nothing to translate again.";
    }

    /// <summary>
    /// Why a message in this review state may not be promoted to a case, or
    /// null when it may.
    /// </summary>
    /// <remarks>
    /// The shape <see cref="PersonConditionEndpoints.WhyNotAReport"/> has: a
    /// pure function the unit suite proves over each state, with the endpoint
    /// turning the sentence into a 409 — a conflict with the message's state,
    /// not a bad request. A message in <see cref="MessageReviewStatus.Entry"/>,
    /// <see cref="MessageReviewStatus.Pending"/>,
    /// <see cref="MessageReviewStatus.SemiAuto"/> or
    /// <see cref="MessageReviewStatus.Qa"/> is waiting for exactly this and
    /// passes; one in <see cref="MessageReviewStatus.Exception"/> never
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
            var code when PromotableCodes.Contains(code) => null,
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
    /// The report is opened by <see cref="MessageCaseWriter.Open"/>, with the
    /// same <see cref="PersonConditionQueries.Add"/> a direct creation uses, so
    /// a promoted case is assigned a record number the one way record numbers
    /// are assigned; the message's lab and address are written onto it and the
    /// message assigned to it in the same transaction. A message another
    /// promotion assigned first is refused as the state check would refuse it.
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

        if (request.PersonConditionId is { } caseId)
        {
            return await UpdateCase(messages, reports, id, request.PersonId, caseId, cancellationToken);
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

        if (await messages.Promote(id, creation, DateTimeOffset.Now, cancellationToken) is not { } created)
        {
            return TypedResults.Problem(
                detail: WhyNotPromotable(await messages.StateOf(id, cancellationToken) ?? string.Empty),
                statusCode: StatusCodes.Status409Conflict,
                title: "This message is not in a state it can be promoted from.");
        }

        var notification = await messages.Notification(created.RecordNumber, created.ConditionId, cancellationToken);

        return TypedResults.Created(
            $"/api/person-conditions/{created.Id}",
            new PromoteMessageResponse(created, notification));
    }

    /// <summary>
    /// Promotes a message onto a person's existing case, through
    /// <see cref="MessageCaseWriter.Update"/>: the case must be an open one of
    /// that person's on the message's condition. A message the update leaves to a reviewer has moved to pending, and is
    /// a 409 with why.
    /// </summary>
    private static async Task<Results<Created<PromoteMessageResponse>, ProblemHttpResult>> UpdateCase(
        MessageQueueQueries messages,
        PersonConditionQueries reports,
        int id,
        int personId,
        int caseId,
        CancellationToken cancellationToken)
    {
        if (await messages.WhyNotACaseToUpdate(id, personId, caseId, cancellationToken) is { } why)
        {
            return Refused(why);
        }

        if (await messages.Update(id, personId, caseId, DateTimeOffset.Now, cancellationToken) is not { } update)
        {
            return TypedResults.Problem(
                detail: WhyNotPromotable(await messages.StateOf(id, cancellationToken) ?? string.Empty),
                statusCode: StatusCodes.Status409Conflict,
                title: "This message is not in a state it can be promoted from.");
        }

        if (update.Status != MessageReviewStatusRules.AssignedCode)
        {
            return TypedResults.Problem(
                detail: update.Reason,
                statusCode: StatusCodes.Status409Conflict,
                title: "This message could not update the case, and is pending review.");
        }

        var report = (await reports.Find(update.PersonConditionId!.Value, cancellationToken))!;
        var notification = report.Id == caseId
            ? $"Case {report.RecordNumber} was updated from this message."
            : await messages.Notification(report.RecordNumber, report.ConditionId, cancellationToken);

        return TypedResults.Created(
            $"/api/person-conditions/{report.Id}",
            new PromoteMessageResponse(report, notification));
    }

    private static Task<Results<Ok<StagedMessageActionResponse>, ProblemHttpResult>> MoveStagedMessages(
        MessageQueueQueries messages,
        StagedMessageActionRequest request,
        CancellationToken cancellationToken = default) =>
        Act(
            messages,
            request,
            state => WhyNotMovable(state.Status, request.Status),
            ids => messages.Move(ids, request.Status!.Trim(), cancellationToken),
            cancellationToken);

    private static Task<Results<Ok<StagedMessageActionResponse>, ProblemHttpResult>> DeleteStagedMessages(
        MessageQueueQueries messages,
        StagedMessageActionRequest request,
        CancellationToken cancellationToken = default) =>
        Act(
            messages,
            request,
            state => WhyNotDeletable(state.Status),
            ids => messages.Delete(ids, cancellationToken),
            cancellationToken);

    private static Task<Results<Ok<StagedMessageActionResponse>, ProblemHttpResult>> RetryStagedMessages(
        MessageQueueQueries messages,
        StagedMessageActionRequest request,
        CancellationToken cancellationToken = default) =>
        Act(
            messages,
            request,
            state => WhyNotRetryable(state.Status, state.Parsed),
            ids => messages.Retry(ids, cancellationToken),
            cancellationToken);

    /// <summary>
    /// Takes an action on every message a request names, or on none: a request
    /// naming no message or too many is a 400, one naming a message that is not
    /// there a 404, and one naming a message the action refuses a 409 that says
    /// why for each, before anything is changed.
    /// </summary>
    private static async Task<Results<Ok<StagedMessageActionResponse>, ProblemHttpResult>> Act(
        MessageQueueQueries messages,
        StagedMessageActionRequest request,
        Func<MessageState, string?> whyNot,
        Func<IReadOnlyList<int>, Task<int>> act,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (WhyNotAnAction(request) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The action asked for is not one this endpoint can take.");
        }

        var ids = request.Ids.Distinct().ToList();
        var states = await messages.States(ids, cancellationToken);

        if (ids.Where(id => !states.ContainsKey(id)).ToList() is { Count: > 0 } missing)
        {
            return TypedResults.Problem(
                detail: $"No staged message has id {string.Join(", ", missing)}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such staged message.");
        }

        var refusals = ids
            .Select(id => whyNot(states[id]) is { } reason ? $"Message {id}: {reason}" : null)
            .OfType<string>()
            .ToList();

        if (refusals.Count > 0)
        {
            return TypedResults.Problem(
                detail: string.Join(" ", refusals),
                statusCode: StatusCodes.Status409Conflict,
                title: "The action cannot be taken on every message chosen, so it was taken on none.");
        }

        return TypedResults.Ok(new StagedMessageActionResponse(await act(ids)));
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
internal sealed class MessageQueueQueries(
    SurvsysDbContext database,
    IMessageParser parser,
    PersonMatchThresholds thresholds,
    IOptions<MessageProcessingOptions> options)
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
    /// Opens the case for a message and assigns the message to it, through
    /// <see cref="MessageCaseWriter.Open"/>; null where the message was no
    /// longer promotable.
    /// </summary>
    public Task<PersonConditionResponse?> Promote(
        int id,
        CreatePersonConditionRequest creation,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        MessageCaseWriter.Open(database, id, creation, now, cancellationToken);

    /// <summary>
    /// Why a case a reviewer names is not one a message may update, or null
    /// where it is: it must be the person's, not deleted, not closed, and on
    /// the message's condition where the message has one — the cases automatic
    /// assignment chooses among.
    /// </summary>
    public async Task<string?> WhyNotACaseToUpdate(
        int id, int personId, int personConditionId, CancellationToken cancellationToken)
    {
        var conditionId = await database.StagedMessages
            .Where(message => message.Id == id)
            .Select(message => message.ConditionId)
            .SingleOrDefaultAsync(cancellationToken);

        return await database.PersonConditions.AnyAsync(
                report => report.Id == personConditionId
                    && report.PersonId == personId
                    && report.DeletedAt == null
                    && report.WorkflowState != WorkflowRules.ClosedCode
                    && (conditionId == null || report.ConditionId == conditionId),
                cancellationToken)
            ? null
            : $"personConditionId {personConditionId} is not an open case of person {personId} on this "
                + "message's condition.";
    }

    /// <summary>
    /// Updates the person's case from a message through
    /// <see cref="MessageCaseWriter.Update"/>; null where the message was no
    /// longer promotable.
    /// </summary>
    public Task<CaseUpdate?> Update(
        int id,
        int personId,
        int personConditionId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        MessageCaseWriter.Update(
            database, parser, thresholds, options.Value, id, personId, personConditionId, now, cancellationToken);

    /// <summary>
    /// The batch notification a promotion raises: a line naming the record
    /// number and the condition, which is the batch notification reduced to
    /// what this phase surfaces rather than an e-mail it sends.
    /// </summary>
    public async Task<string> Notification(
        string recordNumber,
        int conditionId,
        CancellationToken cancellationToken)
    {
        var conditionName = await database.Conditions
            .Where(condition => condition.Id == conditionId)
            .Select(condition => condition.Name)
            .SingleAsync(cancellationToken);

        return $"Case {recordNumber} was created for {conditionName}; a notification has "
            + "been raised to public health.";
    }

    /// <summary>
    /// The review state of each of the messages with these ids that exists,
    /// and whether it parsed — a parsed message has a message type, one that
    /// never parsed has none. An id no message has is absent from the answer.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, MessageState>> States(
        IReadOnlyList<int> ids, CancellationToken cancellationToken) =>
        await database.StagedMessages
            .Where(message => ids.Contains(message.Id))
            .ToDictionaryAsync(
                message => message.Id,
                message => new MessageState(message.ReviewStatus, message.MessageType != null),
                cancellationToken);

    /// <summary>
    /// Moves the messages to a review state, and answers how many it moved.
    /// </summary>
    /// <remarks>
    /// The endpoint judged each message's state before this, and message
    /// processing may assign one in between; so the update itself leaves an
    /// assigned message where it is, in the one statement that moves the rest,
    /// and the count says how many moved.
    /// </remarks>
    public Task<int> Move(IReadOnlyList<int> ids, string status, CancellationToken cancellationToken) =>
        database.StagedMessages
            .Where(message => ids.Contains(message.Id)
                && message.ReviewStatus != MessageReviewStatusRules.AssignedCode)
            .ExecuteUpdateAsync(set => set.SetProperty(message => message.ReviewStatus, status), cancellationToken);

    /// <summary>
    /// Deletes the messages, their results with them by the cascade, and
    /// answers how many it deleted.
    /// </summary>
    /// <remarks>
    /// One statement that leaves an assigned message alone, for the reason
    /// <see cref="Move"/> does: one assigned since the endpoint judged it is
    /// kept as the record of the case it became.
    /// </remarks>
    public Task<int> Delete(IReadOnlyList<int> ids, CancellationToken cancellationToken) =>
        database.StagedMessages
            .Where(message => ids.Contains(message.Id)
                && message.ReviewStatus != MessageReviewStatusRules.AssignedCode)
            .ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// Translates the messages again, as intake did when they arrived:
    /// clears what the last translation wrote, queues each for review and
    /// hands them to <see cref="ElrIntakeService.Translate"/>, which leaves
    /// each queued or holds it again with why. Answers how many it retried.
    /// </summary>
    /// <remarks>
    /// Only a message still held and parsed is read, so one that left its held
    /// state since the endpoint judged it is left as it is. A held message is
    /// not one promotion or message processing assigns, so none read here
    /// becomes assigned before it is saved.
    /// </remarks>
    public async Task<int> Retry(IReadOnlyList<int> ids, CancellationToken cancellationToken)
    {
        var messages = await database.StagedMessages
            .Where(message => ids.Contains(message.Id)
                && message.MessageType != null
                && (message.ReviewStatus == MessageReviewStatusRules.ExceptionCode
                    || message.ReviewStatus == MessageReviewStatusRules.UnprocessedCode
                    || message.ReviewStatus == MessageReviewStatusRules.LockedCode
                    || message.ReviewStatus == MessageReviewStatusRules.GrayExceptionCode))
            .Include(message => message.Results)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.ReviewStatus = MessageReviewStatusRules.EntryCode;
            message.ExceptionDetail = null;
            message.ConditionId = null;
            message.AgencyId = null;
            message.AllowNewCase = null;
            message.AllowUpdateCase = null;
            message.IsSurveillance = null;

            foreach (var result in message.Results)
            {
                result.MasterLoinc = null;
                result.ConditionId = null;
                result.OrganismId = null;
                result.Interpretation = null;
            }
        }

        await ElrIntakeService.Translate(database, messages, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);

        return messages.Count;
    }
}

/// <summary>
/// A staged message's review-state code, and whether it parsed: what an action
/// on the queue judges a message by.
/// </summary>
internal sealed record MessageState(string Status, bool Parsed);
