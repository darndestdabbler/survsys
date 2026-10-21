using System.Security.Claims;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What a user signs in as, and who that says the caller is.
/// </summary>
public class AccountEndpointsTests
{
    private static readonly User Ada = new() { Id = 7, UserName = "ada", DisplayName = "Ada Lovelace" };

    [Fact]
    public void ClaimsForIsAnAuthenticatedPrincipalCarryingTheUserAndEachPrivilegeOnce()
    {
        var principal = AccountEndpoints.ClaimsFor(
            Ada, [Privileges.ViewCases, Privileges.EditCases, Privileges.ViewCases]);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("7", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("ada", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Ada Lovelace", principal.FindFirstValue(AccountEndpoints.DisplayNameClaim));
        Assert.Equal(
            [Privileges.ViewCases, Privileges.EditCases],
            principal.FindAll(PrivilegeRules.PrivilegeClaim).Select(claim => claim.Value));
    }

    [Fact]
    public void CurrentUserReadsBackWhatClaimsForWrote()
    {
        var current = AccountEndpoints.CurrentUser(AccountEndpoints.ClaimsFor(Ada, [Privileges.ManageUsers]));

        Assert.NotNull(current);
        Assert.Equal(7, current.Id);
        Assert.Equal("ada", current.UserName);
        Assert.Equal("Ada Lovelace", current.DisplayName);
        Assert.Equal([Privileges.ManageUsers], current.Privileges);
    }

    [Fact]
    public void AgencyIdsOfReadsBackEachAgencyClaimsForWroteOnce()
    {
        Assert.Equal([3, 5], AccountEndpoints.AgencyIdsOf(AccountEndpoints.ClaimsFor(Ada, [], [3, 5, 3])));
    }

    [Fact]
    public void AgencyIdsOfAUserOfNoAgencyIsEmpty()
    {
        Assert.Empty(AccountEndpoints.AgencyIdsOf(AccountEndpoints.ClaimsFor(Ada, [Privileges.ViewCases])));
    }

    [Fact]
    public void CurrentUserOfACallerNotSignedInIsNull()
    {
        Assert.Null(AccountEndpoints.CurrentUser(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    [Fact]
    public void UserIdOfASignedInCallerIsTheirId()
    {
        Assert.Equal(7, AccountEndpoints.UserIdOf(AccountEndpoints.ClaimsFor(Ada, [])));
    }

    [Fact]
    public void UserIdOfACallerNotSignedInIsNull()
    {
        Assert.Null(AccountEndpoints.UserIdOf(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
