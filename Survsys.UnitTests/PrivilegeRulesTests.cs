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
    [InlineData("GET", "/api/codes/specimen_source")]
    [InlineData("GET", "/api/vocabulary/master-loincs")]
    [InlineData("GET", "/api/vocabulary/child-values/csv")]
    [InlineData("GET", "/api/notification-setup/phin-value-sets")]
    [InlineData("GET", "/api/conditions/3/rules")]
    [InlineData("GET", "/api/ct-towns")]
    [InlineData("GET", "/api/zip-codes/csv")]
    [InlineData("GET", "/api/vaccines")]
    [InlineData("GET", "/api/forms/2")]
    [InlineData("GET", "/api/treatments")]
    [InlineData("GET", "/api/ecr-condition-mappings/csv")]
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
    [InlineData("POST", "/api/codes/race")]
    [InlineData("PUT", "/api/codes/12")]
    [InlineData("POST", "/api/vocabulary/master-loincs")]
    [InlineData("DELETE", "/api/vocabulary/child-loincs/7")]
    [InlineData("POST", "/api/vocabulary/organisms/csv")]
    [InlineData("POST", "/api/notification-setup/units-of-measure/csv")]
    [InlineData("PUT", "/api/conditions/3/rules")]
    [InlineData("POST", "/api/ct-towns")]
    [InlineData("PUT", "/api/ct-towns/Andover")]
    [InlineData("POST", "/api/ct-towns/csv")]
    [InlineData("PUT", "/api/zip-codes/06101")]
    [InlineData("POST", "/api/zip-codes/csv")]
    [InlineData("POST", "/api/vaccines")]
    [InlineData("PUT", "/api/vaccines/3")]
    [InlineData("POST", "/api/vaccines/csv")]
    [InlineData("POST", "/api/forms")]
    [InlineData("PUT", "/api/forms/2")]
    [InlineData("PUT", "/api/treatments/4")]
    [InlineData("POST", "/api/treatments/csv")]
    [InlineData("POST", "/api/ecr-condition-mappings")]
    [InlineData("PUT", "/api/ecr-condition-mappings/3")]
    [InlineData("DELETE", "/api/ecr-condition-mappings/3")]
    [InlineData("POST", "/api/ecr-condition-mappings/csv")]
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
    [InlineData("GET", "/api/audit")]
    [InlineData("GET", "/api/system-settings")]
    [InlineData("PUT", "/api/system-settings/cdc_batch_file_prefix")]
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
    [InlineData("PUT", "/api/cases/forms/5/answers")]
    [InlineData("POST", "/api/cases/7/treatments")]
    public void RequiredForACaseWriteIsEditCases(string method, string path)
    {
        Assert.Equal(Privileges.EditCases, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/saved-views")]
    [InlineData("POST", "/api/saved-views")]
    [InlineData("PUT", "/api/saved-views/2")]
    [InlineData("DELETE", "/api/saved-views/2")]
    [InlineData("GET", "/api/saved-views/2/export")]
    public void RequiredForASavedViewIsViewCasesWhateverTheMethod(string method, string path)
    {
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/person-merges/candidates")]
    [InlineData("GET", "/api/person-merges/persons/4")]
    [InlineData("POST", "/api/person-merges")]
    public void RequiredForDuplicatePersonsAndMergesIsMergePersonsWhateverTheMethod(string method, string path)
    {
        Assert.Equal(Privileges.MergePersons, PrivilegeRules.RequiredFor(method, path));
    }

    [Theory]
    [InlineData("GET", "/api/reports")]
    [InlineData("POST", "/api/reports")]
    [InlineData("GET", "/api/reports/case-counts")]
    [InlineData("POST", "/api/reports/case-counts")]
    public void RequiredForAReportIsRunReportsWhateverTheMethod(string method, string path)
    {
        Assert.Equal(Privileges.RunReports, PrivilegeRules.RequiredFor(method, path));
    }

    /// <summary>
    /// A workflow move is gated by the endpoint, transition by transition, so
    /// the rule asks only that the caller may see cases: a state approver need
    /// not hold edit_cases to approve. The read keeps what every case read has.
    /// </summary>
    [Theory]
    [InlineData("PUT", "/api/person-conditions/7/workflow")]
    [InlineData("GET", "/api/person-conditions/7/workflow")]
    public void RequiredForAWorkflowMoveIsViewCasesWhateverTheMethod(string method, string path)
    {
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor(method, path));
    }

    [Fact]
    public void RequiredForMatchesWholeSegmentsOnly()
    {
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor("GET", "/api/usersettings"));
        Assert.Equal(Privileges.ViewCases, PrivilegeRules.RequiredFor("GET", "/api/reportsx"));
    }

    [Fact]
    public void AllowsNotACallerWhoViewsCasesToRunAReport()
    {
        Assert.False(PrivilegeRules.Allows([Privileges.ViewCases], "GET", "/api/reports/case-counts"));
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
