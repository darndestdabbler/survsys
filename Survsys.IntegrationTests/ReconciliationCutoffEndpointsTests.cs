using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The reconciliation cutoff, against the real database: stored and read back,
/// a week that is not an MMWR week refused, and the verification totals it
/// bounds moving as it moves.
/// </summary>
/// <remarks>
/// The cases and the cutoff row this suite writes are taken out after each
/// test; the cases' export-log rows go with them by cascade.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ReconciliationCutoffEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    /// <summary>
    /// No cutoff reads as a 404; one stored reads back, and stored again it
    /// replaces the one row rather than adding a second.
    /// </summary>
    [Fact]
    public async Task ACutoffIsStoredAndReadBack()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            using var none = await client.GetAsync("/api/reconciliation-cutoff");
            Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);

            await Set(client, 2025, 52);
            await Set(client, 2026, 10);

            Assert.Equal(
                new ReconciliationCutoffResponse(2026, 10),
                await client.GetFromJsonAsync<ReconciliationCutoffResponse>("/api/reconciliation-cutoff", Json));

            await using var context = server.NewContext();
            Assert.Equal(1, await context.ReconciliationCutoffs.CountAsync());
        }
        finally
        {
            await Clear();
        }
    }

    /// <summary>A week outside 1 to 53 is refused.</summary>
    [Fact]
    public async Task AWeekThatIsNotAnMmwrWeekIsRefused()
    {
        using var response = await api.CreateClient().PutAsJsonAsync(
            "/api/reconciliation-cutoff", new ReconciliationCutoffRequest(2026, 54), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The totals count the accepted, notifiable cases after the cutoff: with
    /// the cutoff at week 10 the week-12 case is counted and the week-8 case is
    /// not, a case never accepted is never counted, and moving the cutoff back
    /// to week 5 counts the week-8 case too.
    /// </summary>
    [Fact]
    public async Task TheCutoffBoundsTheTotals()
    {
        await Clear();
        var client = api.CreateClient();

        try
        {
            await Seed("ITEST-R-8", week: 8, accepted: true);
            await Seed("ITEST-R-12", week: 12, accepted: true);
            await Seed("ITEST-R-13", week: 13, accepted: false);

            await Set(client, 2026, 10);
            Assert.Equal(1, await MalariaTotal(client));

            await Set(client, 2026, 5);
            Assert.Equal(2, await MalariaTotal(client));
        }
        finally
        {
            await Clear();
        }
    }

    private static async Task Set(HttpClient client, int year, int week)
    {
        using var response = await client.PutAsJsonAsync(
            "/api/reconciliation-cutoff", new ReconciliationCutoffRequest(year, week), Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>The 2026 Malaria total, or 0 where there is none.</summary>
    private static async Task<int> MalariaTotal(HttpClient client)
    {
        var totals = (await client.GetFromJsonAsync<List<ReconciliationTotalResponse>>(
            "/api/reconciliation-cutoff/totals", Json))!;

        return totals.SingleOrDefault(total => total.Condition == "Malaria" && total.MmwrYear == 2026)?.Count ?? 0;
    }

    /// <summary>
    /// A confirmed Malaria case of 2026 in <paramref name="week"/>, with an
    /// export logged accepted or failed.
    /// </summary>
    private async Task Seed(string recordNumber, int week, bool accepted)
    {
        await using var context = server.NewContext();
        var report = new PersonCondition
        {
            RecordNumber = recordNumber,
            PersonId = await context.Persons
                .Where(person => person.LastName == "Rodriguez" && person.FirstName == "Maria")
                .Select(person => person.Id).SingleAsync(),
            ConditionId = await context.Conditions
                .Where(condition => condition.Name == "Malaria").Select(condition => condition.Id).SingleAsync(),
            CaseStatus = CaseStatusRules.ConfirmedCode,
            MmwrYear = 2026,
            MmwrWeek = week,
            CreatedAt = DateTimeOffset.Now,
        };
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        context.CdcExportLog.Add(new CdcExportLogEntry
        {
            PersonConditionId = report.Id,
            RecordNumber = recordNumber,
            QueuedAt = DateTime.UtcNow,
            Outcome = accepted ? NotificationQueueService.AcceptedOutcome : NotificationQueueService.FailedOutcome,
        });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Takes every case out, which cascades to the export log, and the cutoff
    /// row, so each test starts and ends with neither.
    /// </summary>
    private async Task Clear()
    {
        await using var context = server.NewContext();

        await context.PersonConditions.ExecuteDeleteAsync();
        await context.ReconciliationCutoffs.ExecuteDeleteAsync();
    }
}
