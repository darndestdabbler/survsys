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
            AgencyId: 1,
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
    /// An agency identifier nothing can have is refused here; one no agency on
    /// file has is the agencies table's to refuse.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotAReportRefusesAnIdentifierNoAgencyCanHave(int agencyId)
    {
        Assert.Contains($"agencyId {agencyId} is not an agency",
            PersonConditionEndpoints.WhyNotAReport(
                new CreatePersonConditionRequest(PersonId: 13, ConditionId: 74, AgencyId: agencyId)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(99)]
    public void WhyNotAReportAcceptsAnyIdentifierAnAgencyCouldHave(int agencyId)
    {
        Assert.Null(PersonConditionEndpoints.WhyNotAReport(
            new CreatePersonConditionRequest(PersonId: 13, ConditionId: 74, AgencyId: agencyId)));
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
    /// The investigator rides on the report's own response, so a screen that
    /// has the report has its investigator without a second ask.
    /// </summary>
    [Fact]
    public void ToResponseCarriesTheInvestigator()
    {
        var report = Created();
        report.InvestigatorId = 7;

        Assert.Equal(7, PersonConditionEndpoints.ToResponse.Compile().Invoke(report).InvestigatorId);
    }

    /// <summary>
    /// Every transition passes with what it needs: a bare code for most, an
    /// agency for a route, an investigator for an assignment, and a note that
    /// fits the column on any of them.
    /// </summary>
    [Theory]
    [InlineData(WorkflowRules.AcceptCode, null, null)]
    [InlineData(WorkflowRules.InvestigateCode, null, null)]
    [InlineData(WorkflowRules.CloseCode, null, null)]
    [InlineData(WorkflowRules.RouteCode, 3, null)]
    [InlineData(WorkflowRules.AssignInvestigatorCode, null, 9)]
    [InlineData(WorkflowRules.DoNotInvestigateCode, null, null)]
    [InlineData(WorkflowRules.ReopenCode, null, null)]
    [InlineData(WorkflowRules.ApproveCode, null, null)]
    [InlineData(WorkflowRules.RejectCode, null, null)]
    [InlineData(" close ", null, null)]
    public void WhyNotATransitionAcceptsEachMoveWithWhatItNeeds(string transition, int? agencyId, int? investigatorId)
    {
        Assert.Null(PersonConditionEndpoints.WhyNotATransition(
            new WorkflowTransitionRequest(transition, new string('n', PersonConditionEndpoints.NoteLength), agencyId, investigatorId)));
    }

    /// <summary>
    /// A code that is not a transition: empty, a state code offered where a
    /// move was asked for, the wrong case, and a legacy move this graph does
    /// not have. The message names the value and lists the moves there are.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(WorkflowRules.AcceptedCode)]
    [InlineData("Accept")]
    [InlineData("approve_by_lhd")]
    public void WhyNotATransitionRefusesACodeThatIsNotAMove(string? transition)
    {
        var why = PersonConditionEndpoints.WhyNotATransition(new WorkflowTransitionRequest(transition));

        Assert.Contains($"transition '{transition}'", why, StringComparison.Ordinal);
        Assert.Contains(WorkflowRules.AssignInvestigatorCode, why, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotATransitionRefusesARouteWithNoAgency(int? agencyId)
    {
        Assert.Contains("agencyId is required for a route",
            PersonConditionEndpoints.WhyNotATransition(new WorkflowTransitionRequest(WorkflowRules.RouteCode, AgencyId: agencyId)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void WhyNotATransitionRefusesAnAssignmentWithNoInvestigator(int? investigatorId)
    {
        Assert.Contains("investigatorId is required",
            PersonConditionEndpoints.WhyNotATransition(
                new WorkflowTransitionRequest(WorkflowRules.AssignInvestigatorCode, InvestigatorId: investigatorId)),
            StringComparison.Ordinal);
    }

    /// <summary>An agency or an investigator on a move that takes neither is ignored, not refused.</summary>
    [Fact]
    public void WhyNotATransitionIgnoresAnAgencyAndAnInvestigatorOnAMoveThatTakesNeither()
    {
        Assert.Null(PersonConditionEndpoints.WhyNotATransition(
            new WorkflowTransitionRequest(WorkflowRules.AcceptCode, AgencyId: 0, InvestigatorId: -1)));
    }

    [Fact]
    public void WhyNotATransitionRefusesANoteLongerThanTheColumn()
    {
        Assert.Contains($"note is longer than {PersonConditionEndpoints.NoteLength}",
            PersonConditionEndpoints.WhyNotATransition(
                new WorkflowTransitionRequest(WorkflowRules.AcceptCode, new string('n', PersonConditionEndpoints.NoteLength + 1))),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The privilege each transition needs: the department's own moves are a
    /// case edit; routing, assigning and the state's review have their own.
    /// </summary>
    [Theory]
    [InlineData(WorkflowTransition.Accept, Privileges.EditCases)]
    [InlineData(WorkflowTransition.Investigate, Privileges.EditCases)]
    [InlineData(WorkflowTransition.Close, Privileges.EditCases)]
    [InlineData(WorkflowTransition.DoNotInvestigate, Privileges.EditCases)]
    [InlineData(WorkflowTransition.Route, Privileges.RouteCases)]
    [InlineData(WorkflowTransition.AssignInvestigator, Privileges.AssignInvestigators)]
    [InlineData(WorkflowTransition.Reopen, Privileges.ApproveCases)]
    [InlineData(WorkflowTransition.Approve, Privileges.ApproveCases)]
    [InlineData(WorkflowTransition.Reject, Privileges.ApproveCases)]
    public void PrivilegeForNamesTheGateOfEachTransition(WorkflowTransition transition, string expected)
    {
        Assert.Equal(expected, PersonConditionEndpoints.PrivilegeFor(transition));
    }

    /// <summary>
    /// With every privilege, the options are the whole of what the state
    /// allows, as codes and in the workflow's order.
    /// </summary>
    [Fact]
    public void OptionsForWithEveryPrivilegeIsEverythingTheStateAllows()
    {
        Assert.Equal(
            new[] { WorkflowRules.InvestigateCode, WorkflowRules.RouteCode, WorkflowRules.AssignInvestigatorCode, WorkflowRules.DoNotInvestigateCode },
            PersonConditionEndpoints.OptionsFor(WorkflowState.Accepted, Privileges.All));
        Assert.Equal(
            new[] { WorkflowRules.ReopenCode, WorkflowRules.ApproveCode, WorkflowRules.RejectCode },
            PersonConditionEndpoints.OptionsFor(WorkflowState.Closed, Privileges.All));
    }

    /// <summary>
    /// With edit_cases alone, a department user sees the department's moves
    /// and none of the routing, assigning or state review; a closed case
    /// offers them nothing.
    /// </summary>
    [Fact]
    public void OptionsForWithEditCasesAloneIsTheDepartmentsMoves()
    {
        Assert.Equal(
            new[] { WorkflowRules.InvestigateCode, WorkflowRules.DoNotInvestigateCode },
            PersonConditionEndpoints.OptionsFor(WorkflowState.Accepted, [Privileges.EditCases]));
        Assert.Empty(PersonConditionEndpoints.OptionsFor(WorkflowState.Closed, [Privileges.EditCases]));
    }

    [Fact]
    public void OptionsForWithNoPrivilegeIsNothing()
    {
        Assert.Empty(PersonConditionEndpoints.OptionsFor(WorkflowState.Accepted, []));
        Assert.Empty(PersonConditionEndpoints.OptionsFor(WorkflowState.AssignedToLhd, [Privileges.ViewCases]));
    }
}
