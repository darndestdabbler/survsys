using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the case search makes of a stored report once it is joined to its
/// patient, condition and agency.
/// </summary>
/// <remarks>
/// No database and no host: which reports a criterion matches, how the pages
/// partition them and the order they come in are facts about SQL Server and
/// are <c>Survsys.IntegrationTests</c>'s, against a real one. What is here is
/// the projection to the wire, compiled and proven over a joined row as the
/// work queue's is.
/// </remarks>
public class CaseSearchEndpointsTests
{
    private static readonly Func<JoinedCase, CaseSearchItemResponse> Respond =
        CaseSearchEndpoints.ToResponse.Compile();

    private static JoinedCase Joined(PersonCondition report, Person person, Condition condition, Agency? agency) =>
        new() { Report = report, Person = person, Condition = condition, Agency = agency };

    private static PersonCondition Report() => new()
    {
        Id = 11,
        PersonId = 13,
        ConditionId = 74,
        RecordNumber = "202600001",
        AgencyId = 5,
        EventOnsetDate = new DateOnly(2026, 3, 12),
        CaseStatus = CaseStatusRules.ConfirmedCode,
        MmwrWeek = 11,
        MmwrYear = 2026,
        WorkflowState = WorkflowRules.AcceptedCode,
    };

    private static Person Patient(string? firstName = "Maria") => new()
    {
        Id = 13,
        LastName = "Rodriguez",
        FirstName = firstName,
    };

    private static Condition Malaria() => new() { Id = 74, Name = "Malaria", Active = true };

    private static Agency SaltLake() => new() { Id = 5, Name = "Salt Lake County Health Department" };

    /// <summary>
    /// The row carries the report's own columns, the patient's id and name,
    /// the condition's id and name, and the agency's id and name.
    /// </summary>
    [Fact]
    public void ToResponseCarriesTheReportItsPatientItsConditionAndItsAgency()
    {
        var row = Respond(Joined(Report(), Patient(), Malaria(), SaltLake()));

        Assert.Equal(
            new(11, "202600001", 13, "Rodriguez, Maria", 74, "Malaria",
                5, "Salt Lake County Health Department", WorkflowRules.AcceptedCode,
                CaseStatusRules.ConfirmedCode, 11, 2026, new DateOnly(2026, 3, 12)),
            row);
    }

    /// <summary>
    /// A patient with no first name on file is named by the last name alone,
    /// with no dangling comma, as the People results name one.
    /// </summary>
    [Fact]
    public void ToResponseNamesAPatientWithNoFirstNameByTheLastNameAlone()
    {
        var row = Respond(Joined(Report(), Patient(firstName: null), Malaria(), SaltLake()));

        Assert.Equal("Rodriguez", row.PatientName);
    }

    /// <summary>
    /// A report with no investigating agency carries null for both the
    /// agency's id and its name, rather than a name for an agency it has not.
    /// </summary>
    [Fact]
    public void ToResponseCarriesNoAgencyWhereTheReportHasNone()
    {
        var report = Report();
        report.AgencyId = null;

        var row = Respond(Joined(report, Patient(), Malaria(), agency: null));

        Assert.Null(row.AgencyId);
        Assert.Null(row.AgencyName);
    }

    /// <summary>
    /// A report with no stored workflow code is carried at the assigned-to-LHD
    /// default, so a search row never shows an absent or blank state. Null, an
    /// empty string and whitespace all read as the default, the same reading
    /// <see cref="WorkflowRules.Read(string?)"/> and the work queue give.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToResponseReadsAnEmptyOrWhitespaceWorkflowCodeAsTheDefault(string? stored)
    {
        var report = Report();
        report.WorkflowState = stored;

        var row = Respond(Joined(report, Patient(), Malaria(), SaltLake()));

        Assert.Equal(WorkflowRules.InitialCode, row.WorkflowState);
    }

    /// <summary>
    /// A stored code is carried as it is: the default stands in only for an
    /// absent one.
    /// </summary>
    [Fact]
    public void ToResponseCarriesAStoredWorkflowCodeAsItIs()
    {
        var report = Report();
        report.WorkflowState = WorkflowRules.ClosedCode;

        var row = Respond(Joined(report, Patient(), Malaria(), SaltLake()));

        Assert.Equal(WorkflowRules.ClosedCode, row.WorkflowState);
    }

    /// <summary>
    /// What nobody has set stays null on the wire: an unclassified case, an
    /// MMWR week not yet computed and an onset not yet known are null rather
    /// than a default pretending to be a value.
    /// </summary>
    [Fact]
    public void ToResponseLeavesWhatIsNotSetNull()
    {
        var report = Report();
        report.CaseStatus = null;
        report.MmwrWeek = null;
        report.MmwrYear = null;
        report.EventOnsetDate = null;

        var row = Respond(Joined(report, Patient(), Malaria(), SaltLake()));

        Assert.Null(row.CaseStatus);
        Assert.Null(row.MmwrWeek);
        Assert.Null(row.MmwrYear);
        Assert.Null(row.EventOnsetDate);
    }
}
