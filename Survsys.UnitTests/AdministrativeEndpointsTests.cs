using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.UnitTests;

/// <summary>
/// What the administrative endpoints make of a report, and what they refuse
/// before asking the database anything.
/// </summary>
/// <remarks>
/// No database and no host: that the report exists, that the save lands whole
/// and that a missing report is a 404 are <c>Survsys.IntegrationTests</c>'s to
/// prove against a real SQL Server. The fields here are the ones
/// <c>../nedss/docs/functional/session-012-investigation-admin-contacts-notes.md</c>
/// maps under the Administrative tab, and the values are the ones
/// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c> shows
/// the legacy fill writing to report 12: the two case-status keys and the
/// outcome key as <c>11</c>, and outbreak associated as the capitalised
/// <c>Unknown</c>.
/// </remarks>
public class AdministrativeEndpointsTests
{
    private static PersonCondition Report() => new()
    {
        Id = 12,
        PersonId = 12,
        ConditionId = 74,
        RecordNumber = "202600002",
        CreatedAt = new DateTimeOffset(2026, 3, 13, 0, 16, 37, TimeSpan.FromHours(-6)),
        EventOnsetDate = new DateOnly(2026, 3, 12),
        LhdCaseStatusId = 11,
        StateCaseStatusId = 11,
        OutbreakAssociated = "Unknown",
        LhdInvestigationStartDate = new DateOnly(2026, 3, 13),
        LhdInvestigationCompletedDate = new DateOnly(2026, 3, 20),
        InvestigationOutcomeDispositionId = 11,
        EventName = "March cluster",
        ResultsReportedToClinicianDate = new DateOnly(2026, 3, 14),
        CdcFirstVerballyNotifiedDate = new DateOnly(2026, 3, 15),
    };

    private static UpdateAdministrativeRequest AllNull() =>
        new(null, null, null, null, null, null, null, null, null);

    [Fact]
    public void ToResponseCarriesTheReportsAdministrativeFields()
    {
        var response = AdministrativeEndpoints.ToResponse.Compile().Invoke(Report());

        Assert.Equal(
            new AdministrativeResponse(
                ReportId: 12,
                LhdCaseStatusId: 11,
                StateCaseStatusId: 11,
                OutbreakAssociated: "Unknown",
                LhdInvestigationStartDate: new DateOnly(2026, 3, 13),
                LhdInvestigationCompletedDate: new DateOnly(2026, 3, 20),
                InvestigationOutcomeDispositionId: 11,
                EventName: "March cluster",
                ResultsReportedToClinicianDate: new DateOnly(2026, 3, 14),
                CdcFirstVerballyNotifiedDate: new DateOnly(2026, 3, 15)),
            response);
    }

    /// <summary>
    /// A report whose administrative data has not been entered: every field is
    /// nullable and comes back null rather than as a default.
    /// </summary>
    [Fact]
    public void ToResponseCarriesAReportWithNoAdministrativeDataYet()
    {
        var report = Report();
        report.LhdCaseStatusId = null;
        report.StateCaseStatusId = null;
        report.OutbreakAssociated = null;
        report.LhdInvestigationStartDate = null;
        report.LhdInvestigationCompletedDate = null;
        report.InvestigationOutcomeDispositionId = null;
        report.EventName = null;
        report.ResultsReportedToClinicianDate = null;
        report.CdcFirstVerballyNotifiedDate = null;

        var response = AdministrativeEndpoints.ToResponse.Compile().Invoke(report);

        Assert.Equal(new AdministrativeResponse(
            12, null, null, null, null, null, null, null, null, null), response);
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
            AdministrativeEndpoints.ToResponse.Compile().Invoke(report),
            AdministrativeEndpoints.Respond(report));
    }

    /// <summary>
    /// The session-012 fill as the legacy wrote it: the three keys and the
    /// capitalised code, which this endpoint must take back as they are.
    /// </summary>
    [Fact]
    public void WhyNotAdministrativeAcceptsTheLegacyFill()
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(new UpdateAdministrativeRequest(
            LhdCaseStatusId: 11,
            StateCaseStatusId: 11,
            OutbreakAssociated: "Unknown",
            LhdInvestigationStartDate: null,
            LhdInvestigationCompletedDate: null,
            InvestigationOutcomeDispositionId: 11,
            EventName: null,
            ResultsReportedToClinicianDate: null,
            CdcFirstVerballyNotifiedDate: null)));
    }

    /// <summary>
    /// A save of a report whose administrative data has not been entered:
    /// every field left null is accepted, because every field is optional.
    /// </summary>
    [Fact]
    public void WhyNotAdministrativeAcceptsAllNull()
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(AllNull()));
    }

    [Theory]
    [InlineData("Yes")]
    [InlineData("No")]
    [InlineData("Unknown")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("unknown")]
    [InlineData("YES")]
    public void WhyNotAdministrativeAcceptsAnOutbreakAnswerInAnyCase(string answer)
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(
            AllNull() with { OutbreakAssociated = answer }));
    }

    [Theory]
    [InlineData("Y")]
    [InlineData("Please select")]
    [InlineData("")]
    [InlineData(" Yes")]
    public void WhyNotAdministrativeRefusesAnOutbreakAnswerThatIsNotOne(string code)
    {
        Assert.Contains($"outbreakAssociated '{code}' is not an answer",
            AdministrativeEndpoints.WhyNotAdministrative(
                AllNull() with { OutbreakAssociated = code }),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The investigation dates in order, or on the same day, are accepted: an
    /// investigation started and completed on one day is a real one.
    /// </summary>
    [Theory]
    [InlineData("2026-03-13", "2026-03-20")]
    [InlineData("2026-03-13", "2026-03-13")]
    public void WhyNotAdministrativeAcceptsInvestigationDatesInOrder(string started, string completed)
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(AllNull() with
        {
            LhdInvestigationStartDate = DateOnly.Parse(started),
            LhdInvestigationCompletedDate = DateOnly.Parse(completed),
        }));
    }

    /// <summary>
    /// Either investigation date alone is accepted: the order is only checked
    /// where there are two dates to order.
    /// </summary>
    [Fact]
    public void WhyNotAdministrativeAcceptsOneInvestigationDateAlone()
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(
            AllNull() with { LhdInvestigationStartDate = new DateOnly(2026, 3, 13) }));
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(
            AllNull() with { LhdInvestigationCompletedDate = new DateOnly(2026, 3, 13) }));
    }

    [Fact]
    public void WhyNotAdministrativeRefusesAnInvestigationCompletedBeforeItStarted()
    {
        Assert.Contains(
            "lhdInvestigationCompletedDate 2026-03-12 is before lhdInvestigationStartDate 2026-03-13",
            AdministrativeEndpoints.WhyNotAdministrative(AllNull() with
            {
                LhdInvestigationStartDate = new DateOnly(2026, 3, 13),
                LhdInvestigationCompletedDate = new DateOnly(2026, 3, 12),
            }),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// The keys, the event name and the two reported dates are not checked:
    /// no list stands behind the keys here, the name is free, and the reported
    /// dates have nothing to be ordered against. Any value is accepted.
    /// </summary>
    [Fact]
    public void WhyNotAdministrativeDoesNotCheckTheUncodedFields()
    {
        Assert.Null(AdministrativeEndpoints.WhyNotAdministrative(AllNull() with
        {
            LhdCaseStatusId = -1,
            StateCaseStatusId = 0,
            InvestigationOutcomeDispositionId = int.MaxValue,
            EventName = "",
            ResultsReportedToClinicianDate = new DateOnly(1900, 1, 1),
            CdcFirstVerballyNotifiedDate = new DateOnly(2099, 12, 31),
        }));
    }

    /// <summary>
    /// The outbreak code is checked before the dates, so a request wrong in
    /// both ways is refused for the field the tab shows first.
    /// </summary>
    [Fact]
    public void WhyNotAdministrativeNamesTheOutbreakCodeBeforeTheDates()
    {
        Assert.Contains("outbreakAssociated 'X'",
            AdministrativeEndpoints.WhyNotAdministrative(AllNull() with
            {
                OutbreakAssociated = "X",
                LhdInvestigationStartDate = new DateOnly(2026, 3, 13),
                LhdInvestigationCompletedDate = new DateOnly(2026, 3, 12),
            }),
            StringComparison.Ordinal);
    }
}
