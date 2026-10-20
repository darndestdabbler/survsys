using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// A case morbidity report's edit lock: <c>GET /api/person-conditions/{id}/lock</c>
/// reads who holds it, <c>POST</c> takes it for the signed-in user and
/// <c>DELETE</c> releases it.
/// </summary>
/// <remarks>
/// The lock is what the report screen asks before it lets a user edit: one
/// user at a time, until they release it or <see cref="EditLockRules.Timeout"/>
/// passes. It governs the screen and nothing else — the tab endpoints do not
/// consult it.
///
/// Taking and releasing are each one conditional <c>UPDATE</c>, so two users
/// taking the same free lock at once cannot both have it: the database lets
/// one row change, and the second caller finds it held.
/// </remarks>
public static class EditLockEndpoints
{
    /// <summary>
    /// The lock as the wire carries it: the live holder, or an empty lock
    /// where <paramref name="holderId"/> is null.
    /// </summary>
    /// <remarks>Public because the unit suite proves it.</remarks>
    public static EditLockResponse LockOf(int? holderId, string? holderName, DateTimeOffset? lockedAt) =>
        holderId is null || lockedAt is null
            ? new EditLockResponse(null, null, null, null)
            : new EditLockResponse(holderId, holderName, lockedAt, lockedAt + EditLockRules.Timeout);

    /// <summary>
    /// The sentence a refusal gives for a lock somebody else holds, naming
    /// them and when it lapses.
    /// </summary>
    /// <remarks>Public because the unit suite proves it.</remarks>
    public static string HeldBy(EditLockResponse held)
    {
        ArgumentNullException.ThrowIfNull(held);

        return held.ExpiresAt is { } expires
            ? $"This case is being edited by {held.UserName ?? $"user {held.UserId}"} until "
                + $"{expires.ToUniversalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC."
            : "This case is being edited by someone else. Try again.";
    }

    public static IServiceCollection AddEditLocks(this IServiceCollection services) =>
        services.AddScoped<EditLockQueries>();

    public static IEndpointRouteBuilder MapEditLocks(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/person-conditions/{id:int}/lock", GetLock)
            .WithName("GetEditLock")
            .WithSummary("Who holds a case morbidity report's edit lock, and until when.");

        routes.MapPost("/api/person-conditions/{id:int}/lock", TakeLock)
            .WithName("TakeEditLock")
            .WithSummary("Takes a case morbidity report's edit lock for the signed-in user, where "
                + "it is free, expired or already theirs.");

        routes.MapDelete("/api/person-conditions/{id:int}/lock", ReleaseLock)
            .WithName("ReleaseEditLock")
            .WithSummary("Releases the signed-in user's edit lock on a case morbidity report.");

        return routes;
    }

    private static async Task<Results<Ok<EditLockResponse>, ProblemHttpResult>> GetLock(
        EditLockQueries locks,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (!await locks.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        return TypedResults.Ok(await locks.Read(id, DateTimeOffset.UtcNow, cancellationToken));
    }

    private static async Task<Results<Ok<EditLockResponse>, ProblemHttpResult, UnauthorizedHttpResult>> TakeLock(
        EditLockQueries locks,
        ClaimsPrincipal user,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (AccountEndpoints.UserIdOf(user) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (!await locks.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        var now = DateTimeOffset.UtcNow;

        if (await locks.TryTake(id, userId, now, cancellationToken))
        {
            return TypedResults.Ok(await locks.Read(id, now, cancellationToken));
        }

        if (await locks.IsDeleted(id, cancellationToken))
        {
            return Conflict("A deleted case cannot be edited. Restore it first.");
        }

        return Conflict(HeldBy(await locks.Read(id, now, cancellationToken)));
    }

    private static async Task<Results<NoContent, ProblemHttpResult, UnauthorizedHttpResult>> ReleaseLock(
        EditLockQueries locks,
        ClaimsPrincipal user,
        int id,
        CancellationToken cancellationToken = default)
    {
        if (AccountEndpoints.UserIdOf(user) is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        if (!await locks.HasReport(id, cancellationToken))
        {
            return NoSuchReport(id);
        }

        var now = DateTimeOffset.UtcNow;

        if (!await locks.TryRelease(id, userId, now, cancellationToken))
        {
            return Conflict(HeldBy(await locks.Read(id, now, cancellationToken)));
        }

        return TypedResults.NoContent();
    }

    private static ProblemHttpResult Conflict(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status409Conflict,
            title: "The case's edit lock is not yours to take or release.");

    private static ProblemHttpResult NoSuchReport(int id) =>
        TypedResults.Problem(
            detail: $"No case morbidity report has id {id}.",
            statusCode: StatusCodes.Status404NotFound,
            title: "There is no such case morbidity report.");
}

/// <summary>
/// The edit lock's reads and its two conditional writes, and the only thing in
/// this tier that holds the <c>DbContext</c> for them.
/// </summary>
internal sealed class EditLockQueries(SurvsysDbContext database)
{
    public Task<bool> HasReport(int id, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(report => report.Id == id, cancellationToken);

    public Task<bool> IsDeleted(int id, CancellationToken cancellationToken) =>
        database.PersonConditions.AnyAsync(
            report => report.Id == id && report.DeletedAt != null, cancellationToken);

    /// <summary>The lock on a report that exists, as it stands at <paramref name="now"/>.</summary>
    public async Task<EditLockResponse> Read(int id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var row = await (
            from report in database.PersonConditions
            where report.Id == id
            join user in database.Users on report.LockedByUserId equals (int?)user.Id into holders
            from user in holders.DefaultIfEmpty()
            select new { report.LockedByUserId, report.LockedAt, Name = user == null ? null : user.DisplayName })
            .SingleAsync(cancellationToken);

        return EditLockEndpoints.LockOf(
            EditLockRules.HolderAt(row.LockedByUserId, row.LockedAt, now), row.Name, row.LockedAt);
    }

    /// <summary>
    /// Takes the lock for the user where the report stands and the lock is
    /// free, lapsed or already theirs; false where it was not taken.
    /// </summary>
    public async Task<bool> TryTake(int id, int userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lapsed = now - EditLockRules.Timeout;

        return await database.PersonConditions
            .Where(report => report.Id == id
                && report.DeletedAt == null
                && (report.LockedByUserId == null
                    || report.LockedByUserId == userId
                    || report.LockedAt == null
                    || report.LockedAt <= lapsed))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(report => report.LockedByUserId, userId)
                    .SetProperty(report => report.LockedAt, now),
                cancellationToken) == 1;
    }

    /// <summary>
    /// Clears the lock where it is the user's, lapsed or not held at all;
    /// false where somebody else holds it.
    /// </summary>
    public async Task<bool> TryRelease(int id, int userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lapsed = now - EditLockRules.Timeout;

        return await database.PersonConditions
            .Where(report => report.Id == id
                && (report.LockedByUserId == null
                    || report.LockedByUserId == userId
                    || report.LockedAt == null
                    || report.LockedAt <= lapsed))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(report => report.LockedByUserId, (int?)null)
                    .SetProperty(report => report.LockedAt, (DateTimeOffset?)null),
                cancellationToken) == 1;
    }
}
