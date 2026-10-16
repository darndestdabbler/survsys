using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the work-queue and sharing endpoints make of a stored report, and what
/// they refuse before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: what needs either — that a report exists, that it
/// is or is not already shared, which reports an agency's queue holds — is
/// <c>Survsys.IntegrationTests</c>'s, against a real SQL Server. What is here is
/// the pure validators and the queue projection, proven as the other slices'
/// are.
/// </remarks>
public class WorkQueueEndpointsTests
{
    private static PersonCondition Report(int? agencyId) => new()
    {
        Id = 11,
        PersonId = 13,
        ConditionId = 74,
        RecordNumber = "202600001",
        AgencyId = agencyId,
        EventOnsetDate = new DateOnly(2026, 3, 12),
        CaseStatus = CaseStatusRules.ConfirmedCode,
        WorkflowState = WorkflowRules.AcceptedCode,
    };

    [Fact]
    public void WhyNotAShareAcceptsEveryAgencyTheTierLists()
    {
        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.Null(WorkQueueEndpoints.WhyNotAShare(new ShareReportRequest(agency.Id)));
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void WhyNotAShareRefusesAnAgencyNoAgencyHas(int agencyId)
    {
        Assert.Contains($"agencyId {agencyId} is not an agency",
            WorkQueueEndpoints.WhyNotAShare(new ShareReportRequest(agencyId)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAQueueAgencyAcceptsEveryAgencyTheTierLists()
    {
        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.Null(WorkQueueEndpoints.WhyNotAQueueAgency(agency.Id));
        }
    }

    /// <summary>
    /// A missing agency arrives as zero and is named as required, so a caller
    /// who left the parameter out is told to supply it rather than told it names
    /// no agency.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAQueueAgencyRefusesAMissingAgencyAsRequired(int agencyId)
    {
        Assert.Contains("agencyId is required",
            WorkQueueEndpoints.WhyNotAQueueAgency(agencyId),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAQueueAgencyRefusesAnAgencyNoAgencyHas()
    {
        Assert.Contains("agencyId 99 is not an agency",
            WorkQueueEndpoints.WhyNotAQueueAgency(99),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The queue row carries the report's own columns, and the workflow code is
    /// the one the report holds.
    /// </summary>
    [Fact]
    public void ToResponseCarriesTheReportsOwnColumns()
    {
        var row = WorkQueueEndpoints.ToResponse(agencyId: 1).Compile().Invoke(Report(agencyId: 1));

        Assert.Equal(
            new(11, "202600001", 13, 74, 1, WorkflowRules.AcceptedCode,
                CaseStatusRules.ConfirmedCode, new DateOnly(2026, 3, 12), Shared: false),
            row);
    }

    /// <summary>
    /// The report the agency investigates is owned, not shared: the row read for
    /// the agency whose id it carries is not shared-in.
    /// </summary>
    [Fact]
    public void ToResponseMarksTheInvestigatingAgencysOwnReportNotShared()
    {
        var row = WorkQueueEndpoints.ToResponse(agencyId: 1).Compile().Invoke(Report(agencyId: 1));

        Assert.False(row.Shared);
    }

    /// <summary>
    /// A report read for an agency that is not the one investigating it is
    /// shared-in: it is in that agency's queue only because it was shared, which
    /// is what the flag says.
    /// </summary>
    [Fact]
    public void ToResponseMarksAnotherAgencysReportShared()
    {
        var row = WorkQueueEndpoints.ToResponse(agencyId: 2).Compile().Invoke(Report(agencyId: 1));

        Assert.True(row.Shared);
    }

    /// <summary>
    /// A report with no investigating agency can be in a queue only by a share,
    /// so it reads as shared-in.
    /// </summary>
    [Fact]
    public void ToResponseMarksAReportWithNoAgencyShared()
    {
        var row = WorkQueueEndpoints.ToResponse(agencyId: 2).Compile().Invoke(Report(agencyId: null));

        Assert.True(row.Shared);
        Assert.Null(row.AgencyId);
    }

    /// <summary>
    /// A report with no stored workflow code is carried at the assigned-to-LHD
    /// default, so a queue row never shows an absent state.
    /// </summary>
    [Fact]
    public void ToResponseReadsAnEmptyWorkflowCodeAsTheDefault()
    {
        var report = Report(agencyId: 1);
        report.WorkflowState = null;

        var row = WorkQueueEndpoints.ToResponse(agencyId: 1).Compile().Invoke(report);

        Assert.Equal(WorkflowRules.InitialCode, row.WorkflowState);
    }
}
