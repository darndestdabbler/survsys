using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The clinical save's effect on the stored MMWR week and year, against the
/// real database: a correction to the disease onset date, or the date
/// diagnosed where there is no onset, moves the report to the MMWR week that
/// date falls in, and leaves the stored MMWR untouched where neither is given.
/// </summary>
/// <remarks>
/// The sibling of <see cref="LabEndpointsTests"/>, built the same way: the
/// report is <c>Fixtures/person-conditions.sql</c>'s, re-seeded if a report
/// test in this collection has cleared it. The clinical save writes columns of
/// the report rather than rows of its own, so there is nothing to clear
/// afterwards. The suite does not skip when SQL Server is absent;
/// <see cref="SqlServerFixture"/> fails it loudly instead.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ClinicalEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task AClinicalSaveReadsTheMmwrOffTheOnsetDateAndIsAudited()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        using var put = await client.PutAsJsonAsync($"/api/person-conditions/{reportId}/clinical",
            new UpdateClinicalRequest(
                DiseaseOnsetDate: new DateOnly(2026, 7, 4),
                DateDiagnosed: new DateOnly(2026, 3, 1),
                TreatmentGiven: null,
                DeathStatus: null,
                DateOfDeath: null),
            Json);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var expected = MmwrWeek.Of(new DateOnly(2026, 7, 4));
        var (week, year) = await StoredMmwr(reportId);
        Assert.Equal(expected.Week, week);
        Assert.Equal(expected.Year, year);

        Assert.Contains("Update", await AuditOperations("person_conditions", reportId));
    }

    [Fact]
    public async Task AClinicalSaveFallsBackToTheDiagnosisDateWhenThereIsNoOnset()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        using var put = await client.PutAsJsonAsync($"/api/person-conditions/{reportId}/clinical",
            new UpdateClinicalRequest(
                DiseaseOnsetDate: null,
                DateDiagnosed: new DateOnly(2026, 7, 4),
                TreatmentGiven: null,
                DeathStatus: null,
                DateOfDeath: null),
            Json);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var expected = MmwrWeek.Of(new DateOnly(2026, 7, 4));
        var (week, year) = await StoredMmwr(reportId);
        Assert.Equal(expected.Week, week);
        Assert.Equal(expected.Year, year);
    }

    [Fact]
    public async Task AClinicalSaveLeavesTheStoredMmwrWhenNeitherDateIsGiven()
    {
        var reportId = await EnsureSeededReport();
        var client = api.CreateClient();

        // First, a save with an onset date sets the MMWR to a known week.
        using (var seed = await client.PutAsJsonAsync($"/api/person-conditions/{reportId}/clinical",
            new UpdateClinicalRequest(
                DiseaseOnsetDate: new DateOnly(2026, 7, 4),
                DateDiagnosed: null,
                TreatmentGiven: null,
                DeathStatus: null,
                DateOfDeath: null),
            Json))
        {
            Assert.Equal(HttpStatusCode.OK, seed.StatusCode);
        }

        var before = await StoredMmwr(reportId);

        // Then a save with neither date leaves the MMWR exactly where it was.
        using var put = await client.PutAsJsonAsync($"/api/person-conditions/{reportId}/clinical",
            new UpdateClinicalRequest(
                DiseaseOnsetDate: null,
                DateDiagnosed: null,
                TreatmentGiven: "Yes",
                DeathStatus: null,
                DateOfDeath: null),
            Json);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var after = await StoredMmwr(reportId);
        Assert.Equal(before, after);
        Assert.Equal(MmwrWeek.Of(new DateOnly(2026, 7, 4)).Week, after.Week);
    }

    /// <summary>The MMWR week and year stored on a report, read through a fresh context.</summary>
    private async Task<(int? Week, int? Year)> StoredMmwr(int reportId)
    {
        await using var context = server.NewContext();

        return await context.PersonConditions
            .Where(report => report.Id == reportId)
            .Select(report => new ValueTuple<int?, int?>(report.MmwrWeek, report.MmwrYear))
            .SingleAsync();
    }

    /// <summary>
    /// The operations audited against a table for one single-column key, in the
    /// order they were written, read back through a fresh context.
    /// </summary>
    private async Task<List<string>> AuditOperations(string table, int key)
    {
        await using var context = server.NewContext();

        return await context.Set<AuditEntry>()
            .Where(entry => entry.TableName == table && entry.KeyValues == key.ToString())
            .OrderBy(entry => entry.Id)
            .Select(entry => entry.Operation)
            .ToListAsync();
    }

    /// <summary>
    /// The seeded report's id, re-seeding the report if a report test in this
    /// collection has cleared it since the fixture was built — the helper
    /// <see cref="LabEndpointsTests"/> keeps for the same reason.
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
}
