using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>GET /api/staged-messages/statistics</c>: how many messages were received,
/// by sending facility, review state and day, over a range of days — the
/// legacy EMSA intake statistics.
/// </summary>
/// <remarks>
/// Read-only. The handler is handed <see cref="MessageStatisticsQueries"/> and
/// not the <c>DbContext</c>, because the architecture test lets no
/// <c>Survsys.Data</c> type into an endpoint signature. A message is counted
/// under the state it is in now, on the day it was received.
/// </remarks>
public static class MessageStatisticsEndpoints
{
    /// <summary>The days a range covers when none is asked for, ending today.</summary>
    public const int DefaultDays = 30;

    /// <summary>The most days one request may cover.</summary>
    public const int MaxDays = 366;

    public static IServiceCollection AddMessageStatistics(this IServiceCollection services) =>
        services.AddScoped<MessageStatisticsQueries>();

    public static IEndpointRouteBuilder MapMessageStatistics(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/staged-messages/statistics", GetStatistics)
            .WithName("GetMessageStatistics")
            .WithSummary("Counts of received messages by sending facility, review state and day.");

        return routes;
    }

    /// <summary>
    /// The range a request covers: the days asked for, with an end left out
    /// read as <paramref name="today"/> and a start left out as
    /// <see cref="DefaultDays"/> days ending at the end. Public because the
    /// unit suite proves it.
    /// </summary>
    public static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        return (from ?? end.AddDays(1 - DefaultDays), end);
    }

    /// <summary>
    /// Why a range is not one this endpoint counts over, or null when it is: it
    /// must not end before it starts, nor cover more than <see cref="MaxDays"/>
    /// days. Public because the unit suite proves it.
    /// </summary>
    public static string? WhyNotARange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            return $"to ({to:yyyy-MM-dd}) is before from ({from:yyyy-MM-dd}).";
        }

        return to.DayNumber - from.DayNumber + 1 > MaxDays
            ? $"A range covers at most {MaxDays} days."
            : null;
    }

    private static async Task<Results<Ok<IReadOnlyList<MessageStatisticsRowResponse>>, ProblemHttpResult>> GetStatistics(
        MessageStatisticsQueries statistics,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var range = Range(from, to, DateOnly.FromDateTime(DateTime.UtcNow));

        if (WhyNotARange(range.From, range.To) is { } why)
        {
            return TypedResults.Problem(
                detail: why,
                statusCode: StatusCodes.Status400BadRequest,
                title: "The range asked for is not one this endpoint can count over.");
        }

        return TypedResults.Ok(await statistics.Count(range.From, range.To, cancellationToken));
    }
}

/// <summary>
/// The intake statistics read, and the only thing in this tier that holds the
/// <c>DbContext</c> for it.
/// </summary>
internal sealed class MessageStatisticsQueries(SurvsysDbContext database)
{
    /// <summary>
    /// The counts of messages received from <paramref name="from"/> through
    /// <paramref name="to"/>, newest day first, then by sender and state.
    /// </summary>
    public async Task<IReadOnlyList<MessageStatisticsRowResponse>> Count(
        DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rows = await database.StagedMessages
            .Where(message => message.ReceivedAt >= start && message.ReceivedAt < end)
            .GroupBy(message => new { message.SendingFacility, message.ReviewStatus, message.ReceivedAt.Date })
            .Select(group => new { group.Key.SendingFacility, group.Key.ReviewStatus, group.Key.Date, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MessageStatisticsRowResponse(
                row.SendingFacility, row.ReviewStatus, DateOnly.FromDateTime(row.Date), row.Count))
            .OrderByDescending(row => row.Day)
            .ThenBy(row => row.SendingFacility, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.ReviewStatus, StringComparer.Ordinal)
            .ToList();
    }
}
