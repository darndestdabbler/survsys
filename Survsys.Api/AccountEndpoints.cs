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
/// A wrong password, an unknown user name, a deactivated user and a user
/// locked out after too many wrong passwords are refused in the same words, so
/// the answer does not say which user names exist.
/// </remarks>
public static class AccountEndpoints
{
    /// <summary>The claim type a signed-in caller's display name travels as.</summary>
    public const string DisplayNameClaim = "display_name";

    /// <summary>The claim type each agency a signed-in caller works for travels as.</summary>
    public const string AgencyClaim = "agency";

    /// <summary>
    /// The one password hasher: the framework's, whose hash carries its own
    /// algorithm and salt.
    /// </summary>
    internal static readonly PasswordHasher<User> Hasher = new();

    /// <summary>
    /// The principal a user signs in as: their id, user name, display name,
    /// one claim per privilege and one per agency they work for.
    /// </summary>
    public static ClaimsPrincipal ClaimsFor(User user, IEnumerable<string> privileges, IEnumerable<int>? agencyIds = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(privileges);

        var agencies = (agencyIds ?? []).Distinct().Select(id =>
            new Claim(AgencyClaim, id.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Claim[] identity =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.UserName),
            new(DisplayNameClaim, user.DisplayName),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(
            identity.Concat(privileges.Distinct().Select(p => new Claim(PrivilegeRules.PrivilegeClaim, p))).Concat(agencies),
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

    /// <summary>The agencies a principal <see cref="ClaimsFor"/> built says the caller works for.</summary>
    public static IReadOnlyList<int> AgencyIdsOf(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return principal.FindAll(AgencyClaim)
            .Select(claim => int.TryParse(claim.Value, out var id) ? id : (int?)null)
            .OfType<int>()
            .ToList();
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
        AccessAuditQueries audit,
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

        if (UserIdOf(principal) is { } userId)
        {
            await audit.Record(userId, null, "SignIn", null, cancellationToken);
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
    /// <summary>The wrong passwords in a row that lock a user out.</summary>
    public const int MaximumFailedSignIns = 5;

    /// <summary>How long a lockout lasts.</summary>
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The principal the user signs in as, or null where the user name, the
    /// password, the user's being active or their being locked out says no.
    /// </summary>
    public async Task<ClaimsPrincipal?> SignIn(SignInRequest request, CancellationToken cancellationToken)
    {
        var user = await database.Users.SingleOrDefaultAsync(
            user => user.UserName == request.UserName && user.Active,
            cancellationToken);

        if (user is null || string.IsNullOrEmpty(request.Password)
            || user.LockedOutUntil > DateTimeOffset.UtcNow)
        {
            return null;
        }

        var verified = AccountEndpoints.Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        var now = DateTimeOffset.UtcNow;
        var row = database.Users.Where(candidate => candidate.Id == user.Id);

        if (verified == PasswordVerificationResult.Failed)
        {
            // Too many wrong passwords in a row lock the user out for a while,
            // so a password cannot be guessed at the speed of a script. One
            // UPDATE counts the failure and locks on the last, so failures
            // arriving together are each counted; one landing after another
            // began a lockout is not, so it cannot count toward the next.
            var lockedUntil = now + Lockout;
            await row
                .Where(candidate => candidate.LockedOutUntil == null || candidate.LockedOutUntil <= now)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(
                        candidate => candidate.FailedSignInCount,
                        candidate => candidate.FailedSignInCount + 1 >= MaximumFailedSignIns ? 0 : candidate.FailedSignInCount + 1)
                    .SetProperty(
                        candidate => candidate.LockedOutUntil,
                        candidate => candidate.FailedSignInCount + 1 >= MaximumFailedSignIns ? lockedUntil : candidate.LockedOutUntil),
                cancellationToken);

            return null;
        }

        // The count is cleared only where no lockout began since the user was
        // read; a lockout a parallel failure began refuses this sign-in too.
        var cleared = await row
            .Where(candidate => candidate.LockedOutUntil == null || candidate.LockedOutUntil <= now)
            .ExecuteUpdateAsync(set => set
                    .SetProperty(candidate => candidate.FailedSignInCount, 0)
                    .SetProperty(candidate => candidate.LockedOutUntil, (DateTimeOffset?)null),
                cancellationToken);

        if (cleared == 0)
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

        var agencyIds = await database.UserAgencies
            .Where(userAgency => userAgency.UserId == user.Id)
            .Select(userAgency => userAgency.AgencyId)
            .ToListAsync(cancellationToken);

        return AccountEndpoints.ClaimsFor(user, privileges, agencyIds);
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
