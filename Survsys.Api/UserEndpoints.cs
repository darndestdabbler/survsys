using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/users</c>: the users who may sign in, the roles each holds, and
/// whether they may sign in at all.
/// </summary>
/// <remarks>
/// A user is deactivated rather than deleted, because the access audit names
/// them. A duplicate user name and an unknown role are a 400, asked before the
/// write rather than left to an index or a foreign key to refuse as a 500.
/// </remarks>
public static class UserEndpoints
{
    /// <summary>
    /// The wire shape of a user and the roles they hold. Public because the
    /// unit suite proves it.
    /// </summary>
    public static UserResponse ToResponse(User user, IEnumerable<int> roleIds)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roleIds);

        return new UserResponse(user.Id, user.UserName, user.DisplayName, user.Active, roleIds.Order().ToList());
    }

    /// <summary>
    /// Why this is not a user this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    /// <param name="request">The user to save.</param>
    /// <param name="creating">Whether the user is new, and so needs a password.</param>
    public static string? WhyNotAUser(SaveUserRequest request, bool creating)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.UserName))
        {
            return "userName is required: it is what the user signs in with.";
        }

        if (request.UserName.Length > 100)
        {
            return "userName is longer than 100 characters.";
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return "displayName is required: it is the name screens show for the user.";
        }

        if (request.DisplayName.Length > 200)
        {
            return "displayName is longer than 200 characters.";
        }

        if (creating && string.IsNullOrEmpty(request.Password))
        {
            return "password is required for a new user: without one nobody can sign in as them.";
        }

        if (request.Password is { Length: 0 })
        {
            return "password is empty: leave it out to keep the user's password.";
        }

        return request.RoleIds?.Where(id => id <= 0).Select(id => (int?)id).FirstOrDefault() is { } bad
            ? $"roleIds holds {bad}, which is not a role this tier knows."
            : null;
    }

    /// <summary>
    /// Registers what the user endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddUsers(this IServiceCollection services) =>
        services.AddScoped<UserQueries>();

    public static IEndpointRouteBuilder MapUsers(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users", GetUsers)
            .WithName("GetUsers")
            .WithSummary("Every user, by user name.");

        routes.MapPost("/api/users", CreateUser)
            .WithName("CreateUser")
            .WithSummary("Creates a user with a password and roles.");

        routes.MapPut("/api/users/{id:int}", UpdateUser)
            .WithName("UpdateUser")
            .WithSummary("Saves a user's names, roles and active flag, and a new password where one is given.");

        return routes;
    }

    private static async Task<Ok<PagedResult<UserResponse>>> GetUsers(
        UserQueries users,
        CancellationToken cancellationToken = default)
    {
        var all = await users.All(cancellationToken);

        return TypedResults.Ok(new PagedResult<UserResponse>(all, Paging.DefaultPage, all.Count, all.Count));
    }

    private static async Task<Results<Created<UserResponse>, ProblemHttpResult>> CreateUser(
        UserQueries users,
        SaveUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotAUser(request, creating: true) ?? await users.WhyNotKnown(null, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        var created = await users.Add(request, cancellationToken);

        return TypedResults.Created($"/api/users/{created.Id}", created);
    }

    private static async Task<Results<Ok<UserResponse>, ProblemHttpResult>> UpdateUser(
        UserQueries users,
        int id,
        SaveUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotAUser(request, creating: false) ?? await users.WhyNotKnown(id, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        if (await users.Update(id, request, cancellationToken) is not { } user)
        {
            return TypedResults.Problem(
                detail: $"No user has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such user.");
        }

        return TypedResults.Ok(user);
    }

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The user asked for is not one this endpoint can save.");
}

/// <summary>
/// The user queries, and the only thing in this tier that writes a user.
/// </summary>
internal sealed class UserQueries(SurvsysDbContext database)
{
    /// <summary>Every user, by user name.</summary>
    public async Task<IReadOnlyList<UserResponse>> All(CancellationToken cancellationToken)
    {
        var users = await database.Users.OrderBy(user => user.UserName).ToListAsync(cancellationToken);
        var roles = (await database.UserRoles.ToListAsync(cancellationToken)).ToLookup(userRole => userRole.UserId);

        return users.Select(user => UserEndpoints.ToResponse(user, roles[user.Id].Select(r => r.RoleId))).ToList();
    }

    /// <summary>
    /// Why the user name is taken by another user or a role named is not on
    /// file, or null where neither is so.
    /// </summary>
    public async Task<string?> WhyNotKnown(int? id, SaveUserRequest request, CancellationToken cancellationToken)
    {
        var userName = request.UserName.Trim();

        if (await database.Users.AnyAsync(user => user.UserName == userName && user.Id != id, cancellationToken))
        {
            return $"userName '{userName}' is already another user's.";
        }

        var roleIds = request.RoleIds ?? [];
        var known = await database.Roles.Where(role => roleIds.Contains(role.Id)).Select(role => role.Id).ToListAsync(cancellationToken);

        return roleIds.Where(roleId => !known.Contains(roleId)).Select(roleId => (int?)roleId).FirstOrDefault() is { } unknown
            ? $"roleIds holds {unknown}, which is not one of the roles GET /api/roles lists."
            : null;
    }

    /// <summary>Creates the user with their password and roles.</summary>
    public async Task<UserResponse> Add(SaveUserRequest request, CancellationToken cancellationToken)
    {
        var user = new User();
        database.Users.Add(user);

        return await Save(user, request, cancellationToken);
    }

    /// <summary>Saves over the user, or answers null where no user has that id.</summary>
    public async Task<UserResponse?> Update(int id, SaveUserRequest request, CancellationToken cancellationToken) =>
        await database.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken) is { } user
            ? await Save(user, request, cancellationToken)
            : null;

    private async Task<UserResponse> Save(User user, SaveUserRequest request, CancellationToken cancellationToken)
    {
        user.UserName = request.UserName.Trim();
        user.DisplayName = request.DisplayName.Trim();
        user.Active = request.Active;

        if (request.Password is { Length: > 0 } password)
        {
            user.PasswordHash = AccountEndpoints.Hasher.HashPassword(user, password);
        }

        await database.SaveChangesAsync(cancellationToken);

        var roleIds = (request.RoleIds ?? []).Distinct().ToList();
        await database.UserRoles.Where(userRole => userRole.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
        database.UserRoles.AddRange(roleIds.Select(roleId => new UserRole { UserId = user.Id, RoleId = roleId }));
        await database.SaveChangesAsync(cancellationToken);

        return UserEndpoints.ToResponse(user, roleIds);
    }
}
