using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the investigation endpoints make of a report, and what they refuse
/// before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that the report exists, that the save lands whole
/// and that a missing report is a 404 are <c>Survsys.IntegrationTests</c>'s to
/// prove against a real SQL Server. The fields here are the ones
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Investigation tab, and the values are the ones
/// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
/// shows the legacy fill writing to report 12: the four Contact Oriented
/// codes as the lowercase <c>unknown</c>, imported from <c>11</c> and
/// transmission mode <c>AB</c>.
/// </remarks>
public class InvestigationEndpointsTests
{
    private static PersonCondition Report() => new()
    {
        Id = 12,
        PersonId = 12,
        ConditionId = 74,
        RecordNumber = "202600002",
        CreatedAt = new DateTimeOffset(2026, 3, 13, 0, 16, 37, TimeSpan.FromHours(-6)),
        EventOnsetDate = new DateOnly(2026, 3, 12),
        FoodHandler = "unknown",
        HealthcareWorker = "unknown",
        GroupLiving = "unknown",
        DayCareAssociation = "unknown",
        ImportedFromId = 11,
        TransmissionMode = "AB",
        RiskFactors = "Travel to an endemic area",
        RiskFactorsNotes = "Returned 2026-03-01",
        OtherData1 = "one",
        OtherData2 = "two",
    };

    private static UpdateInvestigationRequest AllNull() =>
        new(null, null, null, null, null, null, null, null, null, null);

    [Fact]
    public void ToResponseCarriesTheReportsInvestigationFields()
    {
        var response = InvestigationEndpoints.ToResponse.Compile().Invoke(Report());

        Assert.Equal(
            new InvestigationResponse(
                ReportId: 12,
                FoodHandler: "unknown",
                HealthcareWorker: "unknown",
                GroupLiving: "unknown",
                DayCareAssociation: "unknown",
                ImportedFromId: 11,
                TransmissionMode: "AB",
                RiskFactors: "Travel to an endemic area",
                RiskFactorsNotes: "Returned 2026-03-01",
                OtherData1: "one",
                OtherData2: "two"),
            response);
    }

    /// <summary>
    /// A report whose investigation data has not been entered: every field is
    /// nullable and comes back null rather than as a default.
    /// </summary>
    [Fact]
    public void ToResponseCarriesAReportWithNoInvestigationDataYet()
    {
        var report = Report();
        report.FoodHandler = null;
        report.HealthcareWorker = null;
        report.GroupLiving = null;
        report.DayCareAssociation = null;
        report.ImportedFromId = null;
        report.TransmissionMode = null;
        report.RiskFactors = null;
        report.RiskFactorsNotes = null;
        report.OtherData1 = null;
        report.OtherData2 = null;

        var response = InvestigationEndpoints.ToResponse.Compile().Invoke(report);

        Assert.Equal(new InvestigationResponse(
            12, null, null, null, null, null, null, null, null, null, null), response);
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

        Assert.Equal(
            InvestigationEndpoints.ToResponse.Compile().Invoke(report),
            InvestigationEndpoints.Respond(report));
    }

    /// <summary>
    /// The session-012 fill as the legacy wrote it: lowercase codes, the
    /// imported-from key and the transmission-mode code, which this endpoint
    /// must take back as they are.
    /// </summary>
    [Fact]
    public void WhyNotInvestigationAcceptsTheLegacyFill()
    {
        Assert.Null(InvestigationEndpoints.WhyNotInvestigation(new UpdateInvestigationRequest(
            FoodHandler: "unknown",
            HealthcareWorker: "unknown",
            GroupLiving: "unknown",
            DayCareAssociation: "unknown",
            ImportedFromId: 11,
            TransmissionMode: "AB",
            RiskFactors: null,
            RiskFactorsNotes: null,
            OtherData1: null,
            OtherData2: null)));
    }

    /// <summary>
    /// A save of a report whose investigation data has not been entered: every
    /// field left null is accepted, because every field is optional.
    /// </summary>
    [Fact]
    public void WhyNotInvestigationAcceptsAllNull()
    {
        Assert.Null(InvestigationEndpoints.WhyNotInvestigation(AllNull()));
    }

    /// <summary>
    /// Each Contact Oriented answer in each case the legacy screen or its
    /// database might present it: the screen shows "Unknown", the row holds
    /// <c>unknown</c>, and neither is wrong.
    /// </summary>
    [Theory]
    [InlineData("Yes")]
    [InlineData("No")]
    [InlineData("Unknown")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("unknown")]
    [InlineData("YES")]
    [InlineData("UNKNOWN")]
    public void WhyNotInvestigationAcceptsAnAnswerInAnyCase(string answer)
    {
        Assert.Null(InvestigationEndpoints.WhyNotInvestigation(
            AllNull() with
            {
                FoodHandler = answer,
                HealthcareWorker = answer,
                GroupLiving = answer,
                DayCareAssociation = answer,
            }));
    }

    /// <summary>
    /// The imported-from key, the transmission mode and the free text are not
    /// checked: no list stands behind the first two here, and the last is
    /// free. Any value is accepted.
    /// </summary>
    [Fact]
    public void WhyNotInvestigationDoesNotCheckTheUncodedFields()
    {
        Assert.Null(InvestigationEndpoints.WhyNotInvestigation(
            AllNull() with
            {
                ImportedFromId = -1,
                TransmissionMode = "not a code",
                RiskFactors = "",
                RiskFactorsNotes = new string('x', 10_000),
                OtherData1 = "   ",
                OtherData2 = "anything",
            }));
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("Please select")]
    [InlineData("")]
    [InlineData(" Yes")]
    public void WhyNotInvestigationRefusesAFoodHandlerThatIsNotAnAnswer(string code)
    {
        Assert.Contains($"foodHandler '{code}' is not an answer",
            InvestigationEndpoints.WhyNotInvestigation(AllNull() with { FoodHandler = code }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("N")]
    [InlineData("")]
    public void WhyNotInvestigationRefusesAHealthcareWorkerThatIsNotAnAnswer(string code)
    {
        Assert.Contains($"healthcareWorker '{code}' is not an answer",
            InvestigationEndpoints.WhyNotInvestigation(
                AllNull() with { HealthcareWorker = code }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Maybe")]
    [InlineData("")]
    public void WhyNotInvestigationRefusesAGroupLivingThatIsNotAnAnswer(string code)
    {
        Assert.Contains($"groupLiving '{code}' is not an answer",
            InvestigationEndpoints.WhyNotInvestigation(AllNull() with { GroupLiving = code }),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("U")]
    [InlineData("")]
    public void WhyNotInvestigationRefusesADayCareAssociationThatIsNotAnAnswer(string code)
    {
        Assert.Contains($"dayCareAssociation '{code}' is not an answer",
            InvestigationEndpoints.WhyNotInvestigation(
                AllNull() with { DayCareAssociation = code }),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The first refused field is the one named, in the order the tab shows
    /// them, so a caller fixing one refusal at a time walks the tab top down.
    /// </summary>
    [Fact]
    public void WhyNotInvestigationNamesTheFirstRefusedField()
    {
        Assert.Contains("groupLiving 'G'",
            InvestigationEndpoints.WhyNotInvestigation(
                AllNull() with { GroupLiving = "G", DayCareAssociation = "D" }),
            StringComparison.Ordinal);
    }
}
