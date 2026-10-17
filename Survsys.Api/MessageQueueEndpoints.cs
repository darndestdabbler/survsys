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

        return routes;
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
}
