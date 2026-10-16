using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The harness itself, proved end to end.
/// </summary>
/// <remarks>
/// <strong>These are not walkthrough tests.</strong> A walkthrough test is
/// named after a document under <c>docs/walkthroughs/</c> and performs that
/// document's written steps in their written order;
/// <see cref="ReviewTheConditionsCatalogueTests"/> is the first of those, and
/// it is where the screen's wording is held to a document. What these tests
/// prove is the thing it stands on: two tiers running, a seeded database
/// behind them, a browser driving the screen, and video recording on.
///
/// Every assertion about the screen goes through Playwright's web-first
/// assertions, which retry until the element is there. That is the repository's
/// answer to render-wait flakiness, and the reason it declined a prerelease
/// package offering the same thing.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SurvsysHarnessTests(SurvsysHarness harness) : SurvsysPageTest
{
    [Fact]
    public async Task BothTiersAnswerReadiness()
    {
        using var asking = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        foreach (var tier in new[] { harness.ApiBaseAddress, harness.WebBaseAddress })
        {
            using var answer = await asking.GetAsync(new Uri(new Uri(tier), SurvsysHarness.ReadinessPath));

            Assert.True(
                answer.IsSuccessStatusCode,
                $"{tier}{SurvsysHarness.ReadinessPath} answered {(int)answer.StatusCode}. The "
                + "harness waits on this endpoint before the first click of every walkthrough, "
                + "so a tier that stops answering it stops the whole suite.");
        }
    }

    [Fact]
    public async Task TheConditionsScreenShowsTheSeededConditions()
    {
        await Page.GotoAsync(harness.WebBaseAddress + "conditions");

        await Expect(Page.Locator("#conditions-heading")).ToHaveTextAsync("Conditions");
        await Expect(Page.Locator("#conditions-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#conditions-table")).ToBeVisibleAsync();

        // The seven fixture rows, in the order the endpoint promises and the
        // screen therefore shows: by name. The fixture inserts them in
        // another order, so this is an assertion about the chain and not
        // about the fixture.
        await Expect(Page.Locator("#conditions-table tbody tr"))
            .ToHaveCountAsync(SurvsysHarness.SeededConditionsByName.Length);
        await Expect(Page.Locator("#conditions-table [data-condition-name]"))
            .ToHaveTextAsync(SurvsysHarness.SeededConditionsByName);
    }

    [Fact]
    public async Task ThePeopleScreenShowsTheSeededPeople()
    {
        await Open(harness.WebBaseAddress + "people");

        await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");

        // Every box empty lists everyone on file, which is what the screen's
        // own lead-in promises.
        await Page.Locator("#person-search-search").ClickAsync();

        await Expect(Page.Locator("#person-search-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#person-search-total")).ToHaveTextAsync(
            $"{SurvsysHarness.SeededPeopleByName.Length} person(s) found, "
            + $"showing {SurvsysHarness.SeededPeopleByName.Length}.");

        // The seven fixture rows, in the order the endpoint promises and the
        // screen therefore shows: last name then first name. The fixture
        // inserts them in another order, so this is an assertion about the
        // chain and not about the fixture.
        await Expect(Page.Locator("#person-search-results [data-person-name]"))
            .ToHaveTextAsync(SurvsysHarness.SeededPeopleByName);
    }

    /// <summary>
    /// Seeding leaves what the fixtures fill — the conditions, the people and
    /// the people's own details — and nothing a walkthrough fills: no report,
    /// no share, no specimen, no test, no result and no susceptibility row.
    /// Run here over whatever the walkthroughs before this test left behind —
    /// a laboratory walkthrough leaves a specimen that names its person — so
    /// it proves the deletion order holds against a database the suite has
    /// already used, which is the database every second run starts from.
    /// </summary>
    [Fact]
    public void SeedingLeavesNothingAWalkthroughFills()
    {
        harness.Seed();

        // The deletion order runs from the laboratory chain down to the
        // report and only then to the people's details and the people, so
        // the tables a walkthrough fills are the ones up to and including the
        // report's.
        const string ReportTable = "person_conditions";
        var filledByWalkthroughs = SurvsysHarness.TablesInDeletionOrder
            .TakeWhile(table => table != ReportTable)
            .Append(ReportTable);
        var leftOver = new List<string>();

        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        foreach (var table in filledByWalkthroughs)
        {
            using var count = connection.CreateCommand();
            count.CommandText = $"SELECT COUNT(*) FROM {table}";

            if ((int)count.ExecuteScalar()! is var rows && rows > 0)
            {
                leftOver.Add($"{table} holds {rows}");
            }
        }

        Assert.True(
            leftOver.Count == 0,
            $"Seeding left rows behind: {string.Join("; ", leftOver)}. A walkthrough that "
            + "needs a report or a specimen creates it, so one still there after seeding is "
            + "one the next walkthrough's document did not describe.");
    }

    /// <summary>
    /// The walkthrough contract's fourth clause: every end-to-end run records
    /// video. The recording is on for every test in the suite because the
    /// base class turns it on, and this is the assertion that says so.
    /// </summary>
    [Fact]
    public async Task EveryTestInThisSuiteRecordsVideo()
    {
        await Page.GotoAsync(harness.WebBaseAddress + "conditions");

        var video = Page.Video;

        Assert.NotNull(video);

        var recording = await video.PathAsync();

        Assert.Equal(SurvsysPageTest.VideoDirectory, Path.GetDirectoryName(recording));
        Assert.Equal(".webm", Path.GetExtension(recording));
    }
}
