using System.IO;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/export-a-dataset.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It creates the one report
/// it exports through the New CMR screens, because the suite seeds no report;
/// the report lives in a table the harness already clears, and the saved view
/// in one the save-a-view step taught it to clear, so this test adds no
/// clearing of its own.
///
/// The report is opened for <strong>Bartonellosis</strong>, which no other
/// walkthrough in this suite reports on. That is what makes the export
/// deterministic: however many reports the tests before it have left in the
/// shared database, the view's Bartonellosis filter selects this one and no
/// other, so the file is exactly its header and this report's row.
///
/// A download needs a context that accepts one. The base class turns video
/// recording on in its context options, which the walkthrough contract
/// requires; this class adds <c>AcceptDownloads</c> on top of them rather than
/// replacing them, so the recording stays on.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ExportADatasetTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/export-a-dataset.md";

    private const string Person = "Zimmerman";
    private const string PersonName = "Zimmerman, Paul";
    private const string Condition = "Bartonellosis";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-01";
    private const string ViewName = "Bartonellosis export";

    /// <summary>The columns as the list reads them back: the catalogue's order.</summary>
    private const string Columns = "last_name, first_name, condition";

    /// <summary>
    /// The whole file the export writes: a header of the chosen column names
    /// and the one report's row, each line ended with CRLF, as
    /// <see cref="Survsys.Api.CaseExport"/> writes it.
    /// </summary>
    private const string ExpectedCsv = "last_name,first_name,condition\r\nZimmerman,Paul,Bartonellosis\r\n";

    /// <summary>
    /// The base class's context options, with downloads accepted so the export
    /// link's file can be read. Video recording and the signed-in state the
    /// base sets are kept.
    /// </summary>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.AcceptDownloads = true;

        return options;
    }

    [Fact]
    public async Task ExportADataset()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. The Step 1 of 2 screen.
        await Step(Document, 2, "read the browser tab, the heading and the Step 1 of 2 lead-in", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("New case morbidity report — Survsys");
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
            await Expect(Page.Locator("#new-report-lead-in")).ToContainTextAsync("Step 1 of 2.");
        });

        // 3. Search the person by last name.
        await Step(Document, 3, "type Zimmerman in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync(Person);
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 4. One person found, with a Create CMR button.
        await Step(Document, 4, "read the count line and the Zimmerman, Paul row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync([PersonName]);
        });

        // 5. Create the report on that row.
        await Step(Document, 5, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 6. The Step 2 of 2 line names the person.
        await Step(Document, 6, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync($"Step 2 of 2. Reporting on {PersonName}"));

        // 7. The date first reported to public health.
        await Step(Document, 7, "enter the First reported to public health date", async () =>
            await Page.Locator("#report-first-reported").FillAsync(FirstReported));

        // 8. The disease — Bartonellosis, which no other walkthrough reports on.
        await Step(Document, 8, "choose Bartonellosis from Disease", async () =>
            await Choose("Disease", Condition));

        // 9. The agency, then Save & Continue.
        await Step(Document, 9, "choose Davis County from Investigating agency and save", async () =>
        {
            await Choose("Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 10. The report's form: the report this walkthrough will export.
        await Step(Document, 10, "read the report's header bar", async () =>
        {
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(Agency);
        });

        // 11. Go to the Saved views screen.
        await Step(Document, 11, "press Saved views and read the screen", async () =>
        {
            await Page.Locator("#nav-saved-views").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Saved views — Survsys");
            await Expect(Page.Locator("#saved-views-heading")).ToHaveTextAsync("Saved views");
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
        });

        // 12. Define and save the view that selects the report.
        await Step(Document, 12, "define and save the Bartonellosis export view", async () =>
        {
            await Page.Locator("#saved-view-name").FillAsync(ViewName);
            await Page.Locator("#saved-view-column-last_name").ClickAsync();
            await Page.Locator("#saved-view-column-first_name").ClickAsync();
            await Page.Locator("#saved-view-column-condition").ClickAsync();
            await Choose("Condition", Condition);
            await Page.Locator("#saved-view-save").ClickAsync();
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
        });

        // 13. The view read back in the list, with its columns and filter.
        await Step(Document, 13, "read the saved view back in the list", async () =>
        {
            var row = Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row).ToContainTextAsync(Columns);
            await Expect(row).ToContainTextAsync(Condition);
        });

        // 14. Follow the Export CSV link, capturing the download and its name.
        IDownload? download = null;

        await Step(Document, 14, "press Export CSV and read the downloaded file's name", async () =>
        {
            download = await Page.RunAndWaitForDownloadAsync(async () =>
                await Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']")
                    .GetByRole(AriaRole.Link, new() { Name = "Export CSV", Exact = true })
                    .ClickAsync());

            Assert.Matches(@"^saved-view-\d+\.csv$", download.SuggestedFilename);
        });

        // 15. Open the file: its header and the one report's row.
        await Step(Document, 15, "open the downloaded file and read its two lines", async () =>
        {
            using var stream = await download!.CreateReadStreamAsync();
            using var reader = new StreamReader(stream);
            var csv = await reader.ReadToEndAsync();

            Assert.Equal(ExpectedCsv, csv);
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — even an empty one, as the saved view's
    /// <strong>Condition</strong> opens on <strong>Any</strong> — renders the
    /// input carrying that identifier as <c>type="hidden"</c> and shows a
    /// sibling box beside it. The hidden input cannot be pressed, and it is not
    /// what the reader presses: the box carrying the label is.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
