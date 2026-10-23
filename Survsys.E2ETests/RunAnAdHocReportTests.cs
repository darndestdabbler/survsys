using System.IO;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/run-an-ad-hoc-report.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry. The
/// document's counts are of a database with no Lyme disease case on file, and
/// every seeded condition is one other walkthroughs open cases of, so the test
/// reseeds before its steps, as the document's reader starts from a seeded
/// database, and again after them, so the walkthroughs that follow find
/// neither its cases nor its view.
///
/// The export is a download, so the context accepts downloads on top of the
/// base class's options, as <see cref="ExportADatasetTests"/>'s does, keeping
/// the video recording on.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RunAnAdHocReportTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/run-an-ad-hoc-report.md";

    private const string Condition = "Lyme disease";
    private const string ViewName = "Lyme disease by agency";

    /// <summary>The whole file the export writes, each line ended with CRLF.</summary>
    private const string ExpectedCsv = "agency,count\r\nDavis County,1\r\nSummit County,1\r\n";

    /// <inheritdoc cref="ExportADatasetTests.ContextOptions"/>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.AcceptDownloads = true;

        return options;
    }

    [Fact]
    public async Task RunAnAdHocReport()
    {
        harness.Seed();

        try
        {
            await PerformTheSteps();
        }
        finally
        {
            harness.Seed();
        }
    }

    private async Task PerformTheSteps()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2-3. The first case, in Davis County.
        await Step(Document, 2, "search Nguyen and press Create CMR on the Nguyen, An row", async () =>
            await CreateFor("Nguyen", "Nguyen, An"));

        await Step(Document, 3, "choose the disease and Davis County, and save", async () =>
            await SaveReport("Nguyen, An", "Davis County"));

        // 4-5. The second case, in Summit County.
        await Step(Document, 4, "press New CMR, search Abbott and press Create CMR on the Abbott, Jane row", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await CreateFor("Abbott", "Abbott, Jane");
        });

        await Step(Document, 5, "choose the disease and Summit County, and save", async () =>
            await SaveReport("Abbott, Jane", "Summit County"));

        // 6. The saved view that selects both cases.
        await Step(Document, 6, "save the Lyme disease by agency view", async () =>
        {
            await Page.Locator("#nav-saved-views").ClickAsync();
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
            await Page.Locator("#saved-view-name").FillAsync(ViewName);
            await Page.Locator("#saved-view-column-last_name").ClickAsync();
            await Page.Locator("#saved-view-column-agency").ClickAsync();
            await Choose("Condition", Condition);
            await Page.Locator("#saved-view-save").ClickAsync();

            await Expect(Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']")).ToHaveCountAsync(1);
        });

        // 7. The Reports screen, and the view chosen.
        await Step(Document, 7, "press Reports and choose the saved view", async () =>
        {
            await Page.Locator("#nav-reports").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Reports — Survsys");
            await Expect(Page.Locator("#reports-heading")).ToHaveTextAsync("Reports");
            await Expect(Page.Locator("#reports-view-heading")).ToHaveTextAsync("Saved view report");
            await Choose("Saved view", ViewName);

            await Expect(GroupBy()).ToHaveTextAsync("last_name");
        });

        // 8. Group by agency, and Run.
        await Step(Document, 8, "choose agency from Group by and press Run", async () =>
        {
            await GroupBy().ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = "agency", Exact = true }).ClickAsync();
            await Expect(GroupBy()).ToHaveTextAsync("agency");
            await Page.Locator("#reports-view-run").ClickAsync();
        });

        // 9. The counts, one row per agency.
        await Step(Document, 9, "read the counts table", async () =>
        {
            var table = Page.Locator("#reports-view-table");

            await Expect(table.Locator("thead th")).ToHaveTextAsync(["agency", "Count"]);
            await Expect(table.Locator("[data-report-view-group]")).ToHaveTextAsync(["Davis County", "Summit County"]);
            await Expect(table.Locator("[data-report-view-count]")).ToHaveTextAsync(["1", "1"]);
        });

        // 10. Export CSV, and the file's lines.
        await Step(Document, 10, "press Export CSV and read the downloaded file", async () =>
        {
            var download = await Page.RunAndWaitForDownloadAsync(async () =>
                await Page.Locator("#reports-view-export").ClickAsync());

            Assert.Matches(@"^saved-view-\d+-counts\.csv$", download.SuggestedFilename);

            using var stream = await download.CreateReadStreamAsync();
            using var reader = new StreamReader(stream);

            Assert.Equal(ExpectedCsv, await reader.ReadToEndAsync());
        });
    }

    /// <summary>
    /// The saved view report's Group by box: the second on the screen, after
    /// the case counts' own.
    /// </summary>
    private ILocator GroupBy() =>
        Page.GetByRole(AriaRole.Combobox, new() { Name = "Group by", Exact = true }).Nth(1);

    /// <summary>Searches <paramref name="lastName"/> and presses Create CMR on its one row.</summary>
    private async Task CreateFor(string lastName, string personName)
    {
        await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
        await Page.Locator("#person-search-last-name").FillAsync(lastName);
        await Page.Locator("#person-search-search").ClickAsync();
        await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync([personName]);
        await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
    }

    /// <summary>Chooses the condition and <paramref name="agency"/>, saves, and reads the header bar.</summary>
    private async Task SaveReport(string personName, string agency)
    {
        await Choose("Disease", Condition);
        await Choose("Investigating agency", agency);
        await Page.Locator("#report-save").ClickAsync();

        await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
        await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(personName);
        await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the other walkthrough
    /// tests in this suite do it.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
