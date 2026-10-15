using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The harness itself, proved end to end.
/// </summary>
/// <remarks>
/// <strong>This is not a walkthrough test.</strong> A walkthrough test is
/// named after a document under <c>docs/walkthroughs/</c> and performs that
/// document's written steps in their written order; the first of those is
/// written in the session that writes the first document. What these tests
/// prove is the thing those tests will stand on: two tiers running, a seeded
/// database behind them, a browser driving the screen, and video recording
/// on.
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

    /// <summary>
    /// The two values the screen has to translate on the way to a reader: a
    /// condition with no CDC code, and one that may not be chosen for a new
    /// report. A walkthrough's wording will describe what is on the screen,
    /// so what is on the screen is what this asserts.
    /// </summary>
    [Fact]
    public async Task TheScreenShowsEachConditionAsTheWalkthroughWillDescribeIt()
    {
        await Page.GotoAsync(harness.WebBaseAddress + "conditions");

        var malaria = Page.Locator("[data-condition='Malaria']");
        await Expect(malaria.Locator("[data-condition-cdc-code]")).ToHaveTextAsync("10130");
        await Expect(malaria.Locator("[data-condition-active]")).ToHaveTextAsync("Yes");

        // Bartonellosis is a real condition with no CDC code, which the
        // screen shows as a dash rather than as an empty cell.
        await Expect(Page.Locator("[data-condition='Bartonellosis'] [data-condition-cdc-code]"))
            .ToHaveTextAsync("—");

        await Expect(Page
                .Locator("[data-condition='Influenza, animal isolates'] [data-condition-active]"))
            .ToHaveTextAsync("No");
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
