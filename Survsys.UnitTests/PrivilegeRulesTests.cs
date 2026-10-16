using Survsys.Api;
using Survsys.Contracts;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// Which privilege a request needs, and whether a caller holding some
/// privileges may make it.
/// </summary>
public class PrivilegeRulesTests
{
    [Theory]
    [InlineData("GET", "/api/conditions")]
    [InlineData("GET", "/api/agencies/by-zip/84108")]
    [InlineData("GET", "/api/facilities/3")]
    [InlineData("GET", "/api/core-elements/overrides")]
    [InlineData("GET", "/api/me")]
    public void RequiredForAReferenceDataReadOrWhoAmIIsNothingBeyondSigningIn(string method, string path)
    {
        Assert.Null(PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("POST", "/api/conditions")]
    [InlineData("PUT", "/api/agencies/2")]
    [InlineData("PUT", "/api/core-elements/1")]
    [InlineData("DELETE", "/api/core-elements/overrides/4")]
    public void RequiredForAReferenceDataWriteIsManageReferenceData(string method, string path)
    {
        Assert.Equal(Privileges.ManageReferenceData, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("PUT", "/api/users/3")]
    [InlineData("POST", "/api/roles")]
    [InlineData("GET", "/api/privileges")]
    [InlineData("GET", "/api/access-audit")]
    public void RequiredForUsersRolesAndTheAccessAuditIsManageUsers(string method, string path)
    {
        Assert.Equal(Privileges.ManageUsers, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/persons")]
    [InlineData("GET", "/api/person-conditions/7")]
    [InlineData("GET", "/api/person-conditions/7/hidden-elements")]
    [InlineData("GET", "/api/work-queue")]
    [InlineData("HEAD", "/api/attachments/2/content")]
    public void RequiredForACaseReadIsViewCases(string method, string path)
    {
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("POST", "/api/persons")]
    [InlineData("PUT", "/api/person-conditions/7/clinical")]
    [InlineData("POST", "/api/person-conditions/7/contacts/import")]
    [InlineData("PUT", "/api/tasks/3/status")]
    public void RequiredForACaseWriteIsEditCases(string method, string path)
    {
        Assert.Equal(Privileges.EditCases, PrivilegeRules.RequiredFor(method, path));
    }

    [Fact]
    public void RequiredForMatchesWholeSegmentsOnly()
    {
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor("GET", "/api/usersettings"));
    }

    [Fact]
    public void RequiredForIgnoresTheCaseOfThePath()
    {
        Assert.Equal(Privileges.ManageUsers, PrivilegeRules.RequiredFor("get", "/API/Users"));
    }

    [Fact]
    public void AllowsACallerHoldingThePrivilege()
    {
        Assert.True(PrivilegeRules.Allows([Privileges.ViewCases], "GET", "/api/persons/1"));
    }

    [Fact]
    public void AllowsNotACallerLackingThePrivilege()
    {
        Assert.False(PrivilegeRules.Allows([Privileges.ViewCases], "PUT", "/api/persons/1"));
    }

    [Fact]
    public void AllowsACallerWithNoPrivilegeToReadTheReferenceData()
    {
        Assert.True(PrivilegeRules.Allows([], "GET", "/api/conditions"));
    }

    [Fact]
    public void AllowsNotACallerWithEveryOtherPrivilegeToAdministerUsers()
    {
        Assert.False(PrivilegeRules.Allows(
            [Privileges.ViewCases, Privileges.EditCases, Privileges.ManageReferenceData],
            "GET",
            "/api/users"));
    }
}
