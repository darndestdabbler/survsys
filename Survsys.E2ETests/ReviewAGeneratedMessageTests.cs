using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/review-a-generated-message.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. The export it reviews is
/// the one <c>Fixtures/nmi-log.sql</c> seeds and the harness reseeds before
/// the suite, found in the log by its record number,
/// <see cref="SurvsysHarness.SeededExportLog"/>, and its accepted outcome,
/// because identity assigns the log ids and the seed exports that case twice.
///
/// The generated message is asserted line by line rather than as one string:
/// the fixture's line ends are whatever the checkout gave the SQL file, and the
/// lines are what the document promises.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ReviewAGeneratedMessageTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-a-generated-message.md";

    private const string Outcome = "accepted";
    private const string Queued = "2025-11-04 08:00";
    private const string Sent = "2025-11-04 08:05";

    /// <summary>The generated message's four segments, as the document prints them.</summary>
    private static readonly string[] MessageLines =
    [
        @"MSH|^~\&|SURVSYS|CT_DPH|CDC|CDC_NNDSS|20251104080500||ORU^R01^ORU_R01|NMI202500048-1|P|2.5.1",
        "PID|1||202500048^^^SURVSYS||NGUYEN^AN||19920229|M",
        "OBR|1||202500048^SURVSYS|68991-9^Epidemiologic information^LN|||20251103",
        "OBX|1|CWE|78746-5^Country of birth^LN||VNM^Viet Nam^ISO3166_1||||||F",
    ];

    [Fact]
    public async Task ReviewAGeneratedMessage()
    {
        // 1. Open the application tier's address and press Export log.
        await Step(Document, 1, "open the application tier's address and press Export log", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-export-log").ClickAsync();
        });

        // 2. The Export log screen.
        await Step(Document, 2, "read the browser tab, the heading and the table", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");
            await Expect(Page.Locator("#export-log-heading")).ToHaveTextAsync("Export log");
            await Expect(Page.Locator("#export-log-table")).ToBeVisibleAsync();
        });

        // 3. The seeded export that was sent and accepted, opened by its record number.
        await Step(Document, 3, "find the accepted 202500048 row and press its record number", async () =>
        {
            var row = Page.Locator("#export-log-table [data-export]")
                .Filter(new() { Has = Page.Locator("[data-export-record]", new() { HasTextString = SurvsysHarness.SeededExportLog }) })
                .Filter(new() { Has = Page.Locator("[data-export-outcome]", new() { HasTextString = Outcome }) });

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-export-sent]")).ToHaveTextAsync(Sent);
            await row.Locator("[data-export-record] a").ClickAsync();
        });

        // 4. The review screen.
        await Step(Document, 4, "read the browser tab, the heading and the back button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Generated message — Survsys");
            await Expect(Page.Locator("#generated-message-review-heading"))
                .ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\s*Export \d+\s*$"));
            await Expect(Page.Locator("#generated-message-review-back")).ToHaveTextAsync("Back to the export log");
        });

        // 5. The export's summary.
        await Step(Document, 5, "read the record number, guide, queued, sent and outcome", async () =>
        {
            await Expect(Page.Locator("#generated-message-review-record")).ToHaveTextAsync(SurvsysHarness.SeededExportLog);
            await Expect(Page.Locator("#generated-message-review-guide")).ToHaveTextAsync("—");
            await Expect(Page.Locator("#generated-message-review-queued")).ToHaveTextAsync(Queued);
            await Expect(Page.Locator("#generated-message-review-sent")).ToHaveTextAsync(Sent);
            await Expect(Page.Locator("#generated-message-review-outcome")).ToHaveTextAsync(Outcome);
        });

        // 6. The generated message, whole.
        await Step(Document, 6, "read the generated message whole", async () =>
        {
            await Expect(Page.GetByText("The message that was, or would be, sent to CDC for this export.")).ToBeVisibleAsync();
            await Expect(Page.Locator("#generated-message-review-none")).ToHaveCountAsync(0);

            var message = (await Page.Locator("#generated-message-review-message").InnerTextAsync())
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(MessageLines, message);
        });
    }
}
