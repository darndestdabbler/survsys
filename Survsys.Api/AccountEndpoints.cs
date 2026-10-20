using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// <c>/api/sign-in</c>, which exchanges a user name and password for a bearer
/// token, and <c>/api/me</c>, who that token says the caller is.
/// </summary>
/// <remarks>
/// The token is ASP.NET Core's own bearer token, carrying the claims
/// <see cref="ClaimsFor"/> builds: the user's id and names, and the privileges
/// their roles held at sign-in. <see cref="PrivilegeRules"/> reads those
/// privileges on every later request, so a request costs no query to
/// authorise, and a change to a user's roles is felt at their next sign-in.
///
/// A wrong password, an unknown user name and a deactivated user are refused
/// in the same words, so the answer does not say which user names exist.
/// </remarks>
public static class AccountEndpoints
{
    /// <summary>The claim type a signed-in caller's display name travels as.</summary>
    public const string DisplayNameClaim = "display_name";

    /// <summary>
    /// The one password hasher: the framework's, whose hash carries its own
    /// algorithm and salt.
    /// </summary>
    internal static readonly PasswordHasher<User> Hasher = new();

    /// <summary>
    /// The principal a user signs in as: their id, user name, display name and
    /// one claim per privilege.
    /// </summary>
    public static ClaimsPrincipal ClaimsFor(User user, IEnumerable<string> privileges)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(privileges);

        Claim[] identity =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(DisplayNameClaim, user.DisplayName),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(
            identity.Concat(privileges.Distinct().Select(p => new Claim(PrivilegeRules.PrivilegeClaim, p))),
            BearerTokenDefaults.AuthenticationScheme));
    }

    /// <summary>
    /// Who a principal <see cref="ClaimsFor"/> built says the caller is, or
    /// null for a caller who is not signed in.
    /// </summary>
    public static CurrentUserResponse? CurrentUser(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (UserIdOf(principal) is not { } id)
        {
            return null;
        }

        return new CurrentUserResponse(
            id,
            principal.FindFirstValue(ClaimTypes.Name) ?? "",
            principal.FindFirstValue(DisplayNameClaim) ?? "",
            principal.FindAll(PrivilegeRules.PrivilegeClaim).Select(claim => claim.Value).ToList());
    }

    /// <summary>The signed-in caller's user id, or null for a caller who is not signed in.</summary>
    public static int? UserIdOf(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.Identity?.IsAuthenticated == true
            && int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                ? id
                : null;
    }

    /// <summary>
    /// Registers what the account endpoints need of the container.
    /// </summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services) =>
        services.AddScoped<AccountQueries>();

    public static IEndpointRouteBuilder MapAccounts(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/sign-in", SignIn)
            .AllowAnonymous()
            .WithName("SignIn")
            .WithSummary("Exchanges a user name and password for a bearer token.");

        routes.MapGet("/api/me", Me)
            .WithName("GetCurrentUser")
            .WithSummary("Who the caller is signed in as, and their privileges.");

        return routes;
    }

    private static async Task<Results<SignInHttpResult, ProblemHttpResult>> SignIn(
        AccountQueries accounts,
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await accounts.SignIn(request, cancellationToken) is not { } principal)
        {
            return TypedResults.Problem(
                detail: "The user name or password is not right, or the user may not sign in.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Signing in was refused.");
        }

        return TypedResults.SignIn(principal, authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
    }

    private static Results<Ok<CurrentUserResponse>, UnauthorizedHttpResult> Me(ClaimsPrincipal user) =>
        CurrentUser(user) is { } current ? TypedResults.Ok(current) : TypedResults.Unauthorized();
}

/// <summary>
/// The account queries, and the only thing in this tier that reads a password
/// hash.
/// </summary>
internal sealed class AccountQueries(SurvsysDbContext database)
{
    /// <summary>
    /// The principal the user signs in as, or null where the user name, the
    /// password or the user's being active says no.
    /// </summary>
    public async Task<ClaimsPrincipal?> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(
            user => user.UserName == request.UserName && user.Active,
            cancellationToken);

        if (user is null || string.IsNullOrEmpty(request.Password))
        {
            return null;
        }

        var verified = AccountEndpoints.Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verified == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verified == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = AccountEndpoints.Hasher.HashPassword(user, request.Password);
            await database.SaveChangesAsync(cancellationToken);
        }

        var privileges = await database.UserRoles
            .Where(userRole => userRole.UserId == user.Id)
            .Join(database.RolePrivileges, userRole => userRole.RoleId, rp => rp.RoleId, (_, rp) => rp.Privilege)
            .Distinct()
            .ToListAsync(cancellationToken);

        return AccountEndpoints.ClaimsFor(user, privileges);
    }

    /// <summary>
    /// Makes sure the configured administrator exists, holding the
    /// Administrator role, so a fresh database has someone who can sign in.
    /// </summary>
    /// <remarks>
    /// A user already on file under that name is left as it is: the
    /// configuration seeds a first administrator and never overwrites a
    /// password somebody has since changed. Nothing is done where either value
    /// is not configured.
    /// </remarks>
    public async Task EnsureAdministrator(string? userName, string? password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password)
            || await database.Users.AnyAsync(user => user.UserName == userName, cancellationToken))
        {
            return;
        }

        var administrator = new User { UserName = userName, DisplayName = userName };
        administrator.PasswordHash = AccountEndpoints.Hasher.HashPassword(administrator, password);
        database.Users.Add(administrator);
        await database.SaveChangesAsync(cancellationToken);

        database.UserRoles.Add(new UserRole { UserId = administrator.Id, RoleId = Role.AdministratorId });
        await database.SaveChangesAsync(cancellationToken);
    }
}
