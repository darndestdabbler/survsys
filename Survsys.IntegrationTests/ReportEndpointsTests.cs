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
/// narrows the counts, and that a grouping the endpoint does not know is a 400;
/// and the ad-hoc report over a saved view, counted by one of its columns and
/// exported as CSV, refusing a column the view does not export and a view the
/// caller does not have.
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

    /// <summary>
    /// A saved view of one condition, counted by its condition column: one
    /// group, holding every report of that condition the database has.
    /// </summary>
    [Fact]
    public async Task ASavedViewIsCountedByOneOfItsColumns()
    {
        var client = api.CreateClient();
        var view = await ViewOfOneCondition(client);

        var counts = (await client.GetFromJsonAsync<IReadOnlyList<CaseCountResponse>>(
            $"/api/reports/saved-views/{view.Id}/counts?groupBy={ExportColumns.Condition}", Json))!;

        var expected = await ReportsOf(view.ConditionId!.Value);
        var only = Assert.Single(counts);
        Assert.Equal(expected, only.Count);
        Assert.True(only.Count >= 1);
    }

    [Fact]
    public async Task ASavedViewsCountsExportAsCsvOfTheColumnAndItsCount()
    {
        var client = api.CreateClient();
        var view = await ViewOfOneCondition(client);

        using var response = await client.GetAsync(
            $"/api/reports/saved-views/{view.Id}/counts/export?groupBy={ExportColumns.Condition}");
        var lines = (await response.Content.ReadAsStringAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("condition,count", lines[0]);
        Assert.EndsWith("," + await ReportsOf(view.ConditionId!.Value), lines[1], StringComparison.Ordinal);
    }

    /// <summary>A column the catalogue knows but the view does not export is a 400.</summary>
    [Fact]
    public async Task CountingASavedViewByAColumnItDoesNotExportIsA400()
    {
        var client = api.CreateClient();
        var view = await ViewOfOneCondition(client);

        using var response = await client.GetAsync(
            $"/api/reports/saved-views/{view.Id}/counts?groupBy={ExportColumns.Agency}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CountingASavedViewNobodyHasIsA404()
    {
        using var response = await api.CreateClient().GetAsync(
            $"/api/reports/saved-views/{int.MaxValue}/counts?groupBy={ExportColumns.Condition}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A new saved view of the first active condition, exporting its record
    /// number and condition, with a report of that condition opened for it.
    /// </summary>
    private async Task<SavedViewResponse> ViewOfOneCondition(HttpClient client)
    {
        var person = (await client.GetFromJsonAsync<PagedResult<PersonResponse>>("/api/persons", Json))!.Items[0];
        var condition = (await client.GetFromJsonAsync<PagedResult<ConditionResponse>>("/api/conditions?pageSize=200", Json))!
            .Items.First(item => item.Active);

        using var opened = await client.PostAsJsonAsync("/api/person-conditions",
            new CreatePersonConditionRequest(person.Id, condition.Id), Json);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);

        using var saved = await client.PostAsJsonAsync("/api/saved-views",
            new SaveSavedViewRequest("View " + Guid.NewGuid().ToString("N"),
                [ExportColumns.RecordNumber, ExportColumns.Condition], ConditionId: condition.Id), Json);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);

        return (await saved.Content.ReadFromJsonAsync<SavedViewResponse>(Json))!;
    }

    /// <summary>What the database holds: how many reports the condition has.</summary>
    private async Task<int> ReportsOf(int conditionId)
    {
        using var context = server.NewContext();

        return await context.PersonConditions.CountAsync(report => report.ConditionId == conditionId);
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
