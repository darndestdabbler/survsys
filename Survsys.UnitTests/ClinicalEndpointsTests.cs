using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the clinical endpoints make of a report and its person, and what they
/// refuse before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that the report exists, that the save lands on the
/// report and its person as one, and that a missing report is a 404 are
/// <c>Survsys.IntegrationTests</c>'s to prove against a real SQL Server. The
/// fields here are the ones
/// <c>../nedss/docs/functional/epitrax-cmr-deep-editing.md</c> section 3 maps:
/// the report's onset, diagnosis and treatment, and its person's died? answer
/// and date of death.
/// </remarks>
public class ClinicalEndpointsTests
{
    private static PersonCondition Report() => new()
    {
        Id = 14,
        PersonId = 11,
        ConditionId = 74,
        RecordNumber = "202600004",
        CreatedAt = new DateTimeOffset(2026, 3, 13, 0, 19, 3, TimeSpan.FromHours(-6)),
        EventOnsetDate = new DateOnly(2026, 3, 8),
        DiseaseOnsetDate = new DateOnly(2026, 3, 1),
        DateDiagnosed = new DateOnly(2026, 3, 5),
        TreatmentGiven = "Yes",
    };

    private static Person Person() => new()
    {
        Id = 11,
        LastName = "Rodriguez",
        FirstName = "Maria",
        DeathStatus = "No",
        DateOfDeath = null,
    };

    [Fact]
    public void ToResponseCarriesTheReportsFieldsAndItsPersons()
    {
        var response = ClinicalEndpoints.ToResponse.Compile().Invoke(Report(), Person());

        Assert.Equal(
            new ClinicalResponse(
                ReportId: 14,
                DiseaseOnsetDate: new DateOnly(2026, 3, 1),
                DateDiagnosed: new DateOnly(2026, 3, 5),
                TreatmentGiven: "Yes",
                DeathStatus: "No",
                DateOfDeath: null),
            response);
    }

    /// <summary>
    /// A report whose clinical data has not been entered: every field is
    /// nullable and comes back null rather than as a default.
    /// </summary>
    [Fact]
    public void ToResponseCarriesAReportWithNoClinicalDataYet()
    {
        var report = Report();
        report.DiseaseOnsetDate = null;
        report.DateDiagnosed = null;
        report.TreatmentGiven = null;

        var response = ClinicalEndpoints.ToResponse.Compile().Invoke(report, Person());

        Assert.Null(response.DiseaseOnsetDate);
        Assert.Null(response.DateDiagnosed);
        Assert.Null(response.TreatmentGiven);
        Assert.Equal(14, response.ReportId);
    }

    /// <summary>
    /// The saved report is described by the read endpoint's own mapping,
    /// compiled — so what a save answers with and what a read returns cannot
    /// drift apart.
    /// </summary>
    [Fact]
    public void RespondIsTheReadMappingItself()
    {
        var report = Report();
        var person = Person();

        Assert.Equal(
            ClinicalEndpoints.ToResponse.Compile().Invoke(report, person),
            ClinicalEndpoints.Respond(report, person));
    }

    [Fact]
    public void WhyNotClinicalAcceptsTheAnsweredFields()
    {
        Assert.Null(ClinicalEndpoints.WhyNotClinical(new UpdateClinicalRequest(
            DiseaseOnsetDate: new DateOnly(2026, 3, 1),
            DateDiagnosed: new DateOnly(2026, 3, 5),
            TreatmentGiven: "Yes",
            DeathStatus: "No",
            DateOfDeath: null)));
    }

    /// <summary>
    /// A save of a report whose clinical data has not been entered: every
    /// field left null is accepted, because every field is optional.
    /// </summary>
    [Fact]
    public void WhyNotClinicalAcceptsAllNull()
    {
        Assert.Null(ClinicalEndpoints.WhyNotClinical(
            new UpdateClinicalRequest(null, null, null, null, null)));
    }

    /// <summary>
    /// A date of death goes with a died? answer of Yes, which is what the
    /// legacy tab records: the date is kept with the status.
    /// </summary>
    [Fact]
    public void WhyNotClinicalAcceptsADateOfDeathWhenDied()
    {
        Assert.Null(ClinicalEndpoints.WhyNotClinical(new UpdateClinicalRequest(
            DiseaseOnsetDate: null,
            DateDiagnosed: null,
            TreatmentGiven: null,
            DeathStatus: "Yes",
            DateOfDeath: new DateOnly(2026, 3, 20))));
    }

    [Theory]
    [InlineData("Dead")]
    [InlineData("yes")]
    [InlineData("Y")]
    [InlineData("")]
    public void WhyNotClinicalRefusesADeathStatusThatIsNotAnAnswer(string death)
    {
        Assert.Contains($"deathStatus '{death}' is not an answer",
            ClinicalEndpoints.WhyNotClinical(
                new UpdateClinicalRequest(null, null, null, death, null)),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Given")]
    [InlineData("yes")]
    [InlineData("")]
    public void WhyNotClinicalRefusesATreatmentGivenThatIsNotAnAnswer(string treatment)
    {
        Assert.Contains($"treatmentGiven '{treatment}' is not an answer",
            ClinicalEndpoints.WhyNotClinical(
                new UpdateClinicalRequest(null, null, treatment, null, null)),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// A date of death with a died? answer that is not Yes is refused: the
    /// legacy tab leaves the date blank when Died is No, and a date without a
    /// death is a contradiction.
    /// </summary>
    [Theory]
    [InlineData("No")]
    [InlineData("Unknown")]
    [InlineData(null)]
    public void WhyNotClinicalRefusesADateOfDeathWithoutDied(string? death)
    {
        Assert.Contains("dateOfDeath is set on a report whose died? answer is not Yes",
            ClinicalEndpoints.WhyNotClinical(new UpdateClinicalRequest(
                null, null, null, death, new DateOnly(2026, 3, 20))),
            StringComparison.Ordinal);
    }
}
