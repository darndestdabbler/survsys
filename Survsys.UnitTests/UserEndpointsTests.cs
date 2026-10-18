using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the user endpoints decide without the database: the wire shape and
/// what a user to save must be.
/// </summary>
public class UserEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheUserAndTheirRolesInOrderAndNeverThePasswordHash()
    {
        var user = new User { Id = 4, UserName = "ada", DisplayName = "Ada Lovelace", PasswordHash = "secret", Active = false };

        var response = UserEndpoints.ToResponse(user, [3, 1]);

        Assert.Equal(4, response.Id);
        Assert.Equal("ada", response.UserName);
        Assert.Equal("Ada Lovelace", response.DisplayName);
        Assert.False(response.Active);
        Assert.Equal([1, 3], response.RoleIds);
        Assert.DoesNotContain("secret", response.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The investigator shape carries the identifier and the display name and
    /// nothing else of the user: not the user name, not the flag, and never the
    /// password hash. Compiled and invoked here, as the query translates it.
    /// </summary>
    [Fact]
    public void ToInvestigatorCarriesTheIdAndDisplayNameAndNothingElse()
    {
        var user = new User { Id = 4, UserName = "ada", DisplayName = "Ada Lovelace", PasswordHash = "secret", Active = true };

        var investigator = UserEndpoints.ToInvestigator.Compile()(user);

        Assert.Equal(new InvestigatorResponse(4, "Ada Lovelace"), investigator);
        Assert.DoesNotContain("UserName", investigator.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret", investigator.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserAcceptsANewUserWithAPasswordAndRoles()
    {
        Assert.Null(UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", "Ada Lovelace", true, [1, 2], "pw"), creating: true));
    }

    [Fact]
    public void WhyNotAUserAcceptsAnExistingUserWithNoPasswordAndNoRoles()
    {
        Assert.Null(UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", "Ada Lovelace"), creating: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void WhyNotAUserRefusesAUserWithNoUserName(string userName)
    {
        Assert.Contains("userName is required",
            UserEndpoints.WhyNotAUser(new SaveUserRequest(userName, "Ada", Password: "pw"), creating: true),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesAUserNameLongerThanTheColumn()
    {
        Assert.Contains("userName is longer than 100",
            UserEndpoints.WhyNotAUser(new SaveUserRequest(new string('a', 101), "Ada"), creating: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesAUserWithNoDisplayName()
    {
        Assert.Contains("displayName is required",
            UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", " "), creating: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesADisplayNameLongerThanTheColumn()
    {
        Assert.Contains("displayName is longer than 200",
            UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", new string('a', 201)), creating: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesANewUserWithNoPassword()
    {
        Assert.Contains("password is required",
            UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", "Ada"), creating: true),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesAnEmptyNewPassword()
    {
        Assert.Contains("password is empty",
            UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", "Ada", Password: ""), creating: false),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAUserRefusesARoleIdThatIsNotOne()
    {
        Assert.Contains("roleIds holds 0",
            UserEndpoints.WhyNotAUser(new SaveUserRequest("ada", "Ada", true, [1, 0]), creating: false),
            StringComparison.Ordinal);
    }
}
