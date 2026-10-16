using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The Investigation and Administrative round-trips, against the real
/// database: each saved whole onto the seeded report through its <c>PUT</c>
/// and read back through its <c>GET</c>, carrying the values the legacy fills
/// wrote —
/// <c>../nedss/docs/db-snapshots/session-012/03-after-investigation-fill.txt</c>
/// for the Investigation tab and
/// <c>../nedss/docs/db-snapshots/session-012/05-after-admin-fill.txt</c> for
/// the Administrative tab.
/// </summary>
/// <remarks>
/// The report is <c>Fixtures/person-conditions.sql</c>'s, found by the record
/// number <see cref="SqlServerFixture.SeededReportRecordNumber"/>. The
/// morbidity-report tests in this collection clear the
/// <c>person_conditions</c> table, so each test re-seeds the report if it is
/// gone before it runs, as <see cref="LabEndpointsTests"/> does. Afterwards
/// each test clears the columns it set and nothing else, so the shared report
/// reads to the other tests as it did before. The suite does not skip when
/// SQL Server is absent; <see cref="SqlServerFixture"/> fails it loudly
/// instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class InvestigationAdministrativeEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// The Investigation round-trip: the session-012 investigation fill —
    /// the four Contact Oriented codes as the lowercase <c>unknown</c> the
    /// legacy wrote, imported from <c>11</c> and transmission mode <c>AB</c>,
    /// which <c>03-after-investigation-fill.txt</c> dumps for report 12 —
    /// with a risk-factors text the legacy fill left empty, saved whole and
    /// read back field for field.
    /// </summary>
    [Fact]
    public async Task InvestigationDataSavedOnAReportReadsBackWhole()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var request = new UpdateInvestigationRequest(
                FoodHandler: "unknown",
                HealthcareWorker: "unknown",
                GroupLiving: "unknown",
                DayCareAssociation: "unknown",
                ImportedFromId: 11,
                TransmissionMode: "AB",
                RiskFactors: "Travel to an endemic area",
                RiskFactorsNotes: null,
                OtherData1: null,
                OtherData2: null);

            using var saved = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/investigation", request, Json);
            saved.EnsureSuccessStatusCode();

            var answered = (await saved.Content.ReadFromJsonAsync<InvestigationResponse>(Json))!;

            using var read = await client.GetAsync($"/api/person-conditions/{reportId}/investigation");
            read.EnsureSuccessStatusCode();

            var round = (await read.Content.ReadFromJsonAsync<InvestigationResponse>(Json))!;

            // What the save answered with and what the read returns are the
            // same investigation data.
            Assert.Equal(answered, round);

            // Every value round-tripped, as the legacy wrote it: the codes in
            // the case they were sent, not folded.
            Assert.Equal(reportId, round.ReportId);
            Assert.Equal("unknown", round.FoodHandler);
            Assert.Equal("unknown", round.HealthcareWorker);
            Assert.Equal("unknown", round.GroupLiving);
            Assert.Equal("unknown", round.DayCareAssociation);
            Assert.Equal(11, round.ImportedFromId);
            Assert.Equal("AB", round.TransmissionMode);
            Assert.Equal("Travel to an endemic area", round.RiskFactors);
            Assert.Null(round.RiskFactorsNotes);
            Assert.Null(round.OtherData1);
            Assert.Null(round.OtherData2);
        }
        finally
        {
            await ClearInvestigationOf(reportId);
        }
    }

    /// <summary>
    /// The Administrative round-trip: the session-012 admin fill — the two
    /// case-status keys and the outcome key as <c>11</c>, and outbreak
    /// associated as the capitalised <c>Unknown</c>, which
    /// <c>05-after-admin-fill.txt</c> dumps for report 12 — with an event name
    /// and the two LHD investigation dates the legacy fill left empty, saved
    /// whole and read back field for field.
    /// </summary>
    [Fact]
    public async Task AdministrativeDataSavedOnAReportReadsBackWhole()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        try
        {
            var request = new UpdateAdministrativeRequest(
                LhdCaseStatusId: 11,
                StateCaseStatusId: 11,
                OutbreakAssociated: "Unknown",
                LhdInvestigationStartDate: new DateOnly(2026, 3, 13),
                LhdInvestigationCompletedDate: new DateOnly(2026, 3, 20),
                InvestigationOutcomeDispositionId: 11,
                EventName: "March cluster",
                ResultsReportedToClinicianDate: null,
                CdcFirstVerballyNotifiedDate: null);

            using var saved = await client.PutAsJsonAsync(
                $"/api/person-conditions/{reportId}/administrative", request, Json);
            saved.EnsureSuccessStatusCode();

            var answered = (await saved.Content.ReadFromJsonAsync<AdministrativeResponse>(Json))!;

            using var read = await client.GetAsync($"/api/person-conditions/{reportId}/administrative");
            read.EnsureSuccessStatusCode();

            var round = (await read.Content.ReadFromJsonAsync<AdministrativeResponse>(Json))!;

            // What the save answered with and what the read returns are the
            // same administrative data.
            Assert.Equal(answered, round);

            // Every value round-tripped, as the legacy wrote it.
            Assert.Equal(reportId, round.ReportId);
            Assert.Equal(11, round.LhdCaseStatusId);
            Assert.Equal(11, round.StateCaseStatusId);
            Assert.Equal("Unknown", round.OutbreakAssociated);
            Assert.Equal(new DateOnly(2026, 3, 13), round.LhdInvestigationStartDate);
            Assert.Equal(new DateOnly(2026, 3, 20), round.LhdInvestigationCompletedDate);
            Assert.Equal(11, round.InvestigationOutcomeDispositionId);
            Assert.Equal("March cluster", round.EventName);
            Assert.Null(round.ResultsReportedToClinicianDate);
            Assert.Null(round.CdcFirstVerballyNotifiedDate);
        }
        finally
        {
            await ClearAdministrativeOf(reportId);
        }
    }

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built — the same
    /// re-seed <see cref="LabEndpointsTests"/> makes.
    /// </summary>
    private async Task<int> EnsureSeededReport()
    {
        await using var context = server.NewContext();

        var report = await context.PersonConditions.SingleOrDefaultAsync(
            report => report.RecordNumber == SqlServerFixture.SeededReportRecordNumber);

        if (report is not null)
        {
            return report.Id;
        }

        var personId = await context.Persons
            .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
            .Select(person => person.Id)
            .SingleAsync();

        var conditionId = await context.Conditions
            .Where(condition => condition.Name == "Malaria")
            .Select(condition => condition.Id)
            .SingleAsync();

        report = new PersonCondition
        {
            PersonId = personId,
            ConditionId = conditionId,
            RecordNumber = SqlServerFixture.SeededReportRecordNumber,
            FirstReportedPhDate = new DateOnly(2026, 3, 12),
            EventOnsetDate = new DateOnly(2026, 3, 8),
            CreatedAt = DateTimeOffset.Now,
        };

        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        return report.Id;
    }

    /// <summary>
    /// Clears the ten investigation columns this test set, and no other, so
    /// the shared report reads to the rest of the collection as it did
    /// before. A report test may have deleted it meanwhile, which is nothing
    /// to clear.
    /// </summary>
    private async Task ClearInvestigationOf(int reportId)
    {
        await using var context = server.NewContext();

        await context.PersonConditions
            .Where(report => report.Id == reportId)
            .ExecuteUpdateAsync(report => report
                .SetProperty(column => column.FoodHandler, (string?)null)
                .SetProperty(column => column.HealthcareWorker, (string?)null)
                .SetProperty(column => column.GroupLiving, (string?)null)
                .SetProperty(column => column.DayCareAssociation, (string?)null)
                .SetProperty(column => column.ImportedFromId, (int?)null)
                .SetProperty(column => column.TransmissionMode, (string?)null)
                .SetProperty(column => column.RiskFactors, (string?)null)
                .SetProperty(column => column.RiskFactorsNotes, (string?)null)
                .SetProperty(column => column.OtherData1, (string?)null)
                .SetProperty(column => column.OtherData2, (string?)null));
    }

    /// <summary>
    /// Clears the nine administrative columns this test set, and no other,
    /// for the reason <see cref="ClearInvestigationOf"/> clears its ten.
    /// </summary>
    private async Task ClearAdministrativeOf(int reportId)
    {
        await using var context = server.NewContext();

        await context.PersonConditions
            .Where(report => report.Id == reportId)
            .ExecuteUpdateAsync(report => report
                .SetProperty(column => column.LhdCaseStatusId, (int?)null)
                .SetProperty(column => column.StateCaseStatusId, (int?)null)
                .SetProperty(column => column.OutbreakAssociated, (string?)null)
                .SetProperty(column => column.LhdInvestigationStartDate, (DateOnly?)null)
                .SetProperty(column => column.LhdInvestigationCompletedDate, (DateOnly?)null)
                .SetProperty(column => column.InvestigationOutcomeDispositionId, (int?)null)
                .SetProperty(column => column.EventName, (string?)null)
                .SetProperty(column => column.ResultsReportedToClinicianDate, (DateOnly?)null)
                .SetProperty(column => column.CdcFirstVerballyNotifiedDate, (DateOnly?)null));
    }
}
