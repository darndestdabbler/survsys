using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The ad-hoc case-count report, against the real database: that grouping by
/// condition counts each condition's reports, that an event onset range
/// narrows the counts, and that a grouping the endpoint does not know is a 400.
/// </summary>
/// <remarks>
/// The suite shares one database, and other tests open reports in it, so each
/// count is checked against what the database itself holds rather than a
/// number written here. The fixture's seeded Malaria report, whose event onset
/// is the 8th of March 2026, is what the range has to take in or leave out.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class ReportEndpointsTests(SqlServerFixture server) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly DateOnly SeededOnset = new(2026, 3, 8);

    private readonly SurvsysApi api = new(server.ConnectionString);

    public void Dispose() => api.Dispose();

    [Fact]
    public async Task GroupingByConditionCountsEachConditionsReports()
    {
        var counts = await CaseCounts("groupBy=condition");

        Assert.Equal(await CountsByCondition(null, null), Dictionary(counts));
        Assert.True(Dictionary(counts)[SqlServerFixture.SeededReportCondition] >= 1);
    }

    [Fact]
    public async Task AnOnsetRangeNarrowsTheCounts()
    {
        var day = SeededOnset.ToString("yyyy-MM-dd");
        var after = SeededOnset.AddDays(1).ToString("yyyy-MM-dd");

        var onTheDay = Dictionary(await CaseCounts($"groupBy=condition&from={day}&to={day}"));
        var afterTheDay = Dictionary(await CaseCounts($"groupBy=condition&from={after}&to={after}"));

        Assert.Equal(await CountsByCondition(SeededOnset, SeededOnset), onTheDay);
        Assert.True(onTheDay[SqlServerFixture.SeededReportCondition] >= 1);
        Assert.Equal(await CountsByCondition(SeededOnset.AddDays(1), SeededOnset.AddDays(1)), afterTheDay);
    }

    [Fact]
    public async Task AnUnknownGroupingIsA400()
    {
        using var response = await api.CreateClient().GetAsync("/api/reports/case-counts?groupBy=person");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<IReadOnlyList<CaseCountResponse>> CaseCounts(string query)
    {
        using var response = await api.CreateClient().GetAsync($"/api/reports/case-counts?{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<CaseCountResponse>>(Json))!;
    }

    private static Dictionary<string, int> Dictionary(IEnumerable<CaseCountResponse> counts) =>
        counts.ToDictionary(row => row.Group!, row => row.Count);

    /// <summary>What the database holds: each condition's report count over the range.</summary>
    private async Task<Dictionary<string, int>> CountsByCondition(DateOnly? start, DateOnly? end)
    {
        using var context = server.NewContext();

        var rows = await (
            from report in context.PersonConditions
            where (start == null || report.EventOnsetDate >= start) && (end == null || report.EventOnsetDate <= end)
            join condition in context.Conditions on report.ConditionId equals condition.Id
            select condition.Name)
            .ToListAsync();

        return rows.GroupBy(name => name).ToDictionary(group => group.Key, group => group.Count());
    }
}
