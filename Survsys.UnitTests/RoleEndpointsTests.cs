using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the role endpoints decide without the database: the wire shape and
/// what a role to save must be.
/// </summary>
public class RoleEndpointsTests
{
    [Fact]
    public void ToResponseCarriesTheRoleAndItsPrivilegesInTheCataloguesOrder()
    {
        var response = RoleEndpoints.ToResponse(
            new Role { Id = 2, Name = "Investigator" },
            [Privileges.EditCases, Privileges.ViewCases]);

        Assert.Equal(2, response.Id);
        Assert.Equal("Investigator", response.Name);
        Assert.Equal([Privileges.ViewCases, Privileges.EditCases], response.Privileges);
    }

    [Fact]
    public void WhyNotARoleAcceptsANameAndPrivilegesFromTheCatalogue()
    {
        Assert.Null(RoleEndpoints.WhyNotARole(new SaveRoleRequest("Investigator", [Privileges.ViewCases])));
    }

    [Fact]
    public void WhyNotARoleAcceptsARoleWithNoPrivileges()
    {
        Assert.Null(RoleEndpoints.WhyNotARole(new SaveRoleRequest("Visitor")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhyNotARoleRefusesARoleWithNoName(string name)
    {
        Assert.Contains("name is required", RoleEndpoints.WhyNotARole(new SaveRoleRequest(name)), StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotARoleRefusesANameLongerThanTheColumn()
    {
        Assert.Contains("name is longer than 100",
            RoleEndpoints.WhyNotARole(new SaveRoleRequest(new string('a', 101))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotARoleRefusesAPrivilegeOutsideTheCatalogue()
    {
        Assert.Contains("privileges holds 'delete_everything'",
            RoleEndpoints.WhyNotARole(new SaveRoleRequest("Investigator", [Privileges.ViewCases, "delete_everything"])),
            StringComparison.Ordinal);
    }
}
