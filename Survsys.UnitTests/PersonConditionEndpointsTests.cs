using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the case morbidity report endpoints make of a stored report, and what
/// they refuse before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: what needs either — that the person and the
/// condition are on file, that the record number follows the highest already
/// issued, that the report comes back from its own <c>Location</c> — is
/// <c>Survsys.IntegrationTests</c>'s, against a real SQL Server.
///
/// The report built here is the one
/// <c>../nedss/docs/db-snapshots/session-010/02-after-cmr-creation.txt</c>
/// dumps, with this schema's own identifiers: record number
/// <c>202600001</c>, the first report of its year.
/// </remarks>
public class PersonConditionEndpointsTests
{
    private static PersonCondition Created() => new()
    {
        Id = 11,
        PersonId = 13,
        ConditionId = 74,
        RecordNumber = "202600001",
        AgencyId = 1,
        FirstReportedPhDate = new DateOnly(2026, 3, 12),
        EventOnsetDate = new DateOnly(2026, 3, 12),
        CreatedAt = new DateTimeOffset(2026, 3, 12, 22, 7, 16, TimeSpan.FromHours(-6)),
        CaseStatus = CaseStatusRules.ConfirmedCode,
        MmwrWeek = 10,
        MmwrYear = 2026,
        WorkflowState = WorkflowRules.UnderInvestigationCode,
    };

    [Fact]
    public void ToResponseCarriesEveryColumnThisPhaseStores()
    {
        var response = PersonConditionEndpoints.ToResponse.Compile().Invoke(Created());

        Assert.Equal(
            new(11, 13, 74, "202600001", 1, new DateOnly(2026, 3, 12), new DateOnly(2026, 3, 12),
                new DateTimeOffset(2026, 3, 12, 22, 7, 16, TimeSpan.FromHours(-6)),
                CaseStatusRules.ConfirmedCode, 10, 2026, WorkflowRules.UnderInvestigationCode),
            response);
    }

    /// <summary>
    /// A report with no investigating agency and no first-reported date: both
    /// columns are nullable, and a report that has neither comes back with
    /// nulls rather than with a zero or a default date.
    /// </summary>
    [Fact]
    public void ToResponseCarriesAReportWithNoAgencyAndNoReportedDate()
    {
        var report = Created();
        report.AgencyId = null;
        report.FirstReportedPhDate = null;

        var response = PersonConditionEndpoints.ToResponse.Compile().Invoke(report);

        Assert.Null(response.AgencyId);
        Assert.Null(response.FirstReportedPhDate);
        Assert.Equal("202600001", response.RecordNumber);
    }

    /// <summary>
    /// The created report is described by the read endpoint's own mapping,
    /// compiled — so what a creation answers with and what a read returns
    /// cannot drift apart.
    /// </summary>
    [Fact]
    public void RespondIsTheReadMappingItself()
    {
        var report = Created();

        Assert.Equal(
            PersonConditionEndpoints.ToResponse.Compile().Invoke(report),
            PersonConditionEndpoints.Respond(report));
    }

    [Fact]
    public void WhyNotAReportAcceptsAPersonAConditionAnAgencyAndADate()
    {
        Assert.Null(PersonConditionEndpoints.WhyNotAReport(new CreatePersonConditionRequest(
            PersonId: 13,
            ConditionId: 74,
            AgencyId: AgencyEndpoints.All[0].Id,
            FirstReportedPhDate: new DateOnly(2026, 3, 12))));
    }

    /// <summary>
    /// The two optional fields are optional here as well as in the schema: the
    /// creation screen asks for an agency, and the endpoint does not insist on
    /// one, because the legacy column does not.
    /// </summary>
    [Fact]
    public void WhyNotAReportAcceptsAReportWithNoAgencyAndNoReportedDate()
    {
        Assert.Null(PersonConditionEndpoints.WhyNotAReport(
            new CreatePersonConditionRequest(PersonId: 13, ConditionId: 74)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void WhyNotAReportRefusesAReportWithNoPerson(int personId)
    {
        Assert.Contains("personId is required",
            PersonConditionEndpoints.WhyNotAReport(
                new CreatePersonConditionRequest(personId, ConditionId: 74)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void WhyNotAReportRefusesAReportWithNoCondition(int conditionId)
    {
        Assert.Contains("conditionId is required",
            PersonConditionEndpoints.WhyNotAReport(
                new CreatePersonConditionRequest(PersonId: 13, ConditionId: conditionId)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The agency is refused here rather than in the database, because the
    /// agency list is a static one in this tier: there is no foreign key
    /// behind <c>agency_id</c> yet to refuse it, which is exactly why this
    /// check is worth having.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void WhyNotAReportRefusesAnAgencyNoAgencyHas(int agencyId)
    {
        Assert.Contains($"agencyId {agencyId} is not an agency",
            PersonConditionEndpoints.WhyNotAReport(
                new CreatePersonConditionRequest(PersonId: 13, ConditionId: 74, AgencyId: agencyId)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void WhyNotAReportAcceptsEveryAgencyTheTierLists()
    {
        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.Null(PersonConditionEndpoints.WhyNotAReport(
                new CreatePersonConditionRequest(PersonId: 13, ConditionId: 74, AgencyId: agency.Id)));
        }
    }

    /// <summary>
    /// The person is named first when both are missing, so the message a
    /// caller reads is about one thing at a time.
    /// </summary>
    [Fact]
    public void WhyNotAReportNamesThePersonFirst()
    {
        Assert.Contains("personId is required",
            PersonConditionEndpoints.WhyNotAReport(new CreatePersonConditionRequest(0, 0, 99)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Each status a report may be set to: the four known codes, and null,
    /// which clears the status to Unknown.
    /// </summary>
    [Theory]
    [InlineData(CaseStatusRules.ConfirmedCode)]
    [InlineData(CaseStatusRules.ProbableCode)]
    [InlineData(CaseStatusRules.SuspectedCode)]
    [InlineData(CaseStatusRules.NotACaseCode)]
    [InlineData(null)]
    public void WhyNotAStatusAcceptsAKnownCodeOrNull(string? caseStatus)
    {
        Assert.Null(PersonConditionEndpoints.WhyNotAStatus(new UpdateCaseStatusRequest(caseStatus)));
    }

    /// <summary>
    /// A code no case may be set to: the legacy codes this installation does
    /// not use, a code in the wrong case, and a value that is not a code. The
    /// message names the value the caller offered.
    /// </summary>
    [Theory]
    [InlineData("D")]
    [InlineData("OS")]
    [InlineData("c")]
    [InlineData("X")]
    public void WhyNotAStatusRefusesACodeNoCaseMayBeSetTo(string caseStatus)
    {
        Assert.Contains($"caseStatus '{caseStatus}'",
            PersonConditionEndpoints.WhyNotAStatus(new UpdateCaseStatusRequest(caseStatus)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Every agency the tier lists is one a report may be assigned to, as it is
    /// one a report may be created against: the two checks read the same static
    /// list.
    /// </summary>
    [Fact]
    public void WhyNotAnAssignmentAcceptsEveryAgencyTheTierLists()
    {
        foreach (var agency in AgencyEndpoints.All)
        {
            Assert.Null(PersonConditionEndpoints.WhyNotAnAssignment(
                new AssignReportRequest(agency.Id)));
        }
    }

    /// <summary>
    /// An agency nobody has is refused before the database is asked, as it is on
    /// creation: there is no foreign key behind <c>agency_id</c> to refuse it,
    /// which is why this check is worth having. The message names the value.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(99)]
    public void WhyNotAnAssignmentRefusesAnAgencyNoAgencyHas(int agencyId)
    {
        Assert.Contains($"agencyId {agencyId} is not an agency",
            PersonConditionEndpoints.WhyNotAnAssignment(new AssignReportRequest(agencyId)),
            StringComparison.Ordinal);
    }
}
