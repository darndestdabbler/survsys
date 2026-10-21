using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/inspect-the-export-log.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screen carries —
/// never by a MudBlazor class or a cell's position. The log it reads is the
/// one <c>Fixtures/nmi-log.sql</c> seeds and the harness reseeds before the
/// suite. Nothing in the application writes the export log yet — not even the
/// queue-a-case walkthrough, which fills the notification queue — so the
/// count line is exactly the seed's and the rows are read in the screen's
/// order, newest first, by their record number.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class InspectTheExportLogTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/inspect-the-export-log.md";

    [Fact]
    public async Task InspectTheExportLog()
    {
        // 1. Open the application tier's address and press Export log.
        await Step(Document, 1, "open the application tier's address and press Export log", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-export-log").ClickAsync();
        });

        // 2. The Export log screen and its lead-in.
        await Step(Document, 2, "read the browser tab, the heading and the lead-in", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");
            await Expect(Page.Locator("#export-log-heading")).ToHaveTextAsync("Export log");
            await Expect(Page.GetByText(
                "The history of case exports to CDC, newest first. Open an export to read the "
                + "message that was generated for it.")).ToBeVisibleAsync();
        });

        // 3. The count line: exactly the seed's two exports.
        await Step(Document, 3, "read the count line", async () =>
        {
            await Expect(Page.Locator("#export-log-total"))
                .ToHaveTextAsync($"{SurvsysHarness.SeededExportLogRows} export(s), showing {SurvsysHarness.SeededExportLogRows}.");
            await Expect(Page.Locator("#export-log-none")).ToHaveCountAsync(0);
        });

        // 4. The two rows, newest first: the queued export, then the accepted one.
        await Step(Document, 4, "read the two rows for 202500048, newest first", async () =>
        {
            var rows = Page.Locator("#export-log-table [data-export]");

            await Expect(rows).ToHaveCountAsync(SurvsysHarness.SeededExportLogRows);
            await Expect(rows.Locator("[data-export-record]"))
                .ToHaveTextAsync([SurvsysHarness.SeededExportLog, SurvsysHarness.SeededExportLog]);
            await Expect(rows.Locator("[data-export-queued]"))
                .ToHaveTextAsync(["2025-11-10 08:00", "2025-11-04 08:00"]);
            await Expect(rows.Locator("[data-export-sent]"))
                .ToHaveTextAsync(["—", "2025-11-04 08:05"]);
            await Expect(rows.Locator("[data-export-outcome]"))
                .ToHaveTextAsync(["—", "accepted"]);
        });
    }
}
