using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/roles</c>: the roles a user may hold and the privileges each holds;
/// and <c>/api/privileges</c>, the catalogue a role chooses from.
/// </summary>
/// <remarks>
/// A privilege outside <see cref="Privileges.All"/> is a 400: a role holding a
/// name no code checks would be a promise nothing keeps. A role's privileges
/// reach its users at their next sign-in.
/// </remarks>
public static class RoleEndpoints
{
    /// <summary>
    /// The wire shape of a role and the privileges it holds, in the order the
    /// catalogue lists them. Public because the unit suite proves it.
    /// </summary>
    public static RoleResponse ToResponse(Role role, IEnumerable<string> privileges)
    {
        ArgumentNullException.ThrowIfNull(role);
        ArgumentNullException.ThrowIfNull(privileges);

        var held = privileges.ToHashSet(StringComparer.Ordinal);

        return new RoleResponse(role.Id, role.Name, Privileges.All.Where(held.Contains).ToList());
    }

    /// <summary>
    /// Why this is not a role this tier will save, as far as can be said
    /// without the database, or null when it is one.
    /// </summary>
    public static string? WhyNotARole(SaveRoleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "name is required: a role is given to a user by its name.";
        }

        if (request.Name.Length > 100)
        {
            return "name is longer than 100 characters.";
        }

        return request.Privileges?.FirstOrDefault(p => !Privileges.All.Contains(p, StringComparer.Ordinal)) is { } unknown
            ? $"privileges holds '{unknown}', which is not one of the privileges GET /api/privileges lists."
            : null;
    }

    /// <summary>
    /// Registers what the role endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddRoles(this IServiceCollection services) =>
        services.AddScoped<RoleQueries>();

    public static IEndpointRouteBuilder MapRoles(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/roles", GetRoles)
            .WithName("GetRoles")
            .WithSummary("Every role, by name, with its privileges.");

        routes.MapPost("/api/roles", CreateRole)
            .WithName("CreateRole")
            .WithSummary("Creates a role with its privileges.");

        routes.MapPut("/api/roles/{id:int}", UpdateRole)
            .WithName("UpdateRole")
            .WithSummary("Saves a role's name and privileges.");

        routes.MapGet("/api/privileges", GetPrivileges)
            .WithName("GetPrivileges")
            .WithSummary("The privileges a role may hold.");

        return routes;
    }

    private static async Task<Ok<PagedResult<RoleResponse>>> GetRoles(
        RoleQueries roles,
        CancellationToken cancellationToken = default)
    {
        var all = await roles.All(cancellationToken);

        return TypedResults.Ok(new PagedResult<RoleResponse>(all, Paging.DefaultPage, all.Count, all.Count));
    }

    private static async Task<Results<Created<RoleResponse>, ProblemHttpResult>> CreateRole(
        RoleQueries roles,
        SaveRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotARole(request) ?? await roles.WhyNotKnown(null, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        var created = await roles.Add(request, cancellationToken);

        return TypedResults.Created($"/api/roles/{created.Id}", created);
    }

    private static async Task<Results<Ok<RoleResponse>, ProblemHttpResult>> UpdateRole(
        RoleQueries roles,
        int id,
        SaveRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((WhyNotARole(request) ?? await roles.WhyNotKnown(id, request, cancellationToken)) is { } why)
        {
            return Refused(why);
        }

        if (await roles.Update(id, request, cancellationToken) is not { } role)
        {
            return TypedResults.Problem(
                detail: $"No role has id {id}.",
                statusCode: StatusCodes.Status404NotFound,
                title: "There is no such role.");
        }

        return TypedResults.Ok(role);
    }

    private static Ok<IReadOnlyList<string>> GetPrivileges() => TypedResults.Ok(Privileges.All);

    private static ProblemHttpResult Refused(string why) =>
        TypedResults.Problem(
            detail: why,
            statusCode: StatusCodes.Status400BadRequest,
            title: "The role asked for is not one this endpoint can save.");
}

/// <summary>
/// The role queries, and the only thing in this tier that writes a role.
/// </summary>
internal sealed class RoleQueries(SurvsysDbContext database)
{
    /// <summary>Every role, by name.</summary>
    public async Task<IReadOnlyList<RoleResponse>> All(CancellationToken cancellationToken)
    {
        var roles = await database.Roles.OrderBy(role => role.Name).ToListAsync(cancellationToken);
        var privileges = (await database.RolePrivileges.ToListAsync(cancellationToken)).ToLookup(rp => rp.RoleId);

        return roles.Select(role => RoleEndpoints.ToResponse(role, privileges[role.Id].Select(rp => rp.Privilege))).ToList();
    }

    /// <summary>Why the name is taken by another role, or null where it is not.</summary>
    public async Task<string?> WhyNotKnown(int? id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        return await database.Roles.AnyAsync(role => role.Name == name && role.Id != id, cancellationToken)
            ? $"name '{name}' is already another role's."
            : null;
    }

    /// <summary>Creates the role with its privileges.</summary>
    public async Task<RoleResponse> Add(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var role = new Role();
        database.Roles.Add(role);

        return await Save(role, request, cancellationToken);
    }

    /// <summary>Saves over the role, or answers null where no role has that id.</summary>
    public async Task<RoleResponse?> Update(int id, SaveRoleRequest request, CancellationToken cancellationToken) =>
        await database.Roles.SingleOrDefaultAsync(role => role.Id == id, cancellationToken) is { } role
            ? await Save(role, request, cancellationToken)
            : null;

    private async Task<RoleResponse> Save(Role role, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        role.Name = request.Name.Trim();
        await database.SaveChangesAsync(cancellationToken);

        var privileges = (request.Privileges ?? []).Distinct(StringComparer.Ordinal).ToList();
        await database.RolePrivileges.Where(rp => rp.RoleId == role.Id).ExecuteDeleteAsync(cancellationToken);
        database.RolePrivileges.AddRange(privileges.Select(p => new RolePrivilege { RoleId = role.Id, Privilege = p }));
        await database.SaveChangesAsync(cancellationToken);

        return RoleEndpoints.ToResponse(role, privileges);
    }
}
