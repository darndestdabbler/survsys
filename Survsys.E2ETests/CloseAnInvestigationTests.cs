using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/close-an-investigation.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position. Every list is opened and its item chosen by the text the
/// document names, as the creation walkthrough's test does.
///
/// The document has its reader note two things at step 9 — the record number
/// and the report's address — and this test keeps both: the record number is
/// what the messages and the row's first cell are asserted against, and the
/// address is opened again at step 18, which is how the document has its
/// reader prove the closed case is still on file. The report's id is read
/// off that address only because the queue row's identifier carries it: the
/// reader finds the row by its record number and types the id nowhere.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class CloseAnInvestigationTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/close-an-investigation.md";

    /// <summary>The report's address, as the document has the reader note it.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    /// <summary>
    /// The case's onset as the queue shows it: the day the report was opened,
    /// which is today, written as the document says it is.
    /// </summary>
    private static readonly string Today =
        DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task CloseAnInvestigation()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Abbott in Last name, then Search.
        await Step(Document, 2, "type Abbott in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Abbott");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found: Abbott, Jane.
        await Step(Document, 3, "read the count line and the Abbott, Jane row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Abbott, Jane"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Abbott, Jane.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Abbott, Jane"));

        // 6. Lyme disease from the Disease list.
        await Step(Document, 6, "choose Lyme disease from Disease", async () =>
            await Choose("Disease", "Lyme disease"));

        // 7. Utah State from the Investigating agency list.
        await Step(Document, 7, "choose Utah State from Investigating agency", async () =>
            await Choose("Investigating agency", "Utah State"));

        // 8. Save & Continue.
        await Step(Document, 8, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 9. The report's form, and the two things the reader notes: the
        //    record number and the address, whose id names the queue row.
        var recordNumber = string.Empty;
        var reportId = string.Empty;
        var reportAddress = string.Empty;

        await Step(Document, 9, "read the report form's header and note the record number and its address", async () =>
        {
            var year = DateTime.Today.Year.ToString("D4", CultureInfo.InvariantCulture);

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Lyme disease");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Utah State");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(new Regex($"^{year}\\d{{5}}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
            reportAddress = Page.Url;

            var address = ReportAddress.Match(reportAddress);
            Assert.True(address.Success, $"The address {reportAddress} does not end in /event/edit/ and a number.");
            reportId = address.Groups[1].Value;
        });

        // 10. The Agency board, Summit County chosen, without this report.
        await Step(Document, 10, "press Agency board, choose Summit County and read that the report is not in its queue", async () =>
        {
            await Page.Locator("#nav-agency-queue").ClickAsync();
            await Expect(Page.Locator("#agency-queue-heading")).ToHaveTextAsync("Agency board");

            await Choose("Agency", "Summit County");

            await Expect(Page.Locator("#work-queue-busy")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#work-queue-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#queue-row-{reportId}")).ToHaveCountAsync(0);
        });

        // 11. Utah State's board, the case's row at its top, and Assign on it
        //     opening the agency choice for that report.
        await Step(Document, 11, "choose Utah State, press Assign on the top row and choose Summit County from Assign to agency", async () =>
        {
            await Choose("Agency", "Utah State");

            await Expect(Page.Locator("#work-queue-table tbody tr").First)
                .ToHaveAttributeAsync("id", $"queue-row-{reportId}");
            await Expect(Page.Locator($"#queue-row-{reportId}-record")).ToHaveTextAsync(recordNumber);

            await Page.Locator($"#queue-row-{reportId}-assign").ClickAsync();

            await Expect(Page.Locator("#agency-queue-assign")).ToContainTextAsync($"Assign report {recordNumber}");
            await Choose("Assign to agency", "Summit County");
        });

        // 12. Assign: the message, and the row now on Summit County's board.
        await Step(Document, 12, "press Assign, read the assigned message, choose Summit County and read the row", async () =>
        {
            await Page.Locator("#agency-queue-assign-save").ClickAsync();

            await Expect(Page.Locator("#agency-queue-assign-saved"))
                .ToHaveTextAsync($"Report {recordNumber} is assigned, at the assigned-to-LHD start.");
            await Expect(Page.Locator("#agency-queue-assign-error")).ToHaveCountAsync(0);

            await Choose("Agency", "Summit County");

            await Expect(Page.Locator($"#queue-row-{reportId} td")).ToHaveTextAsync(
                [recordNumber, "Lyme disease", "Assigned to LHD", "Unknown", Today, "Investigating", "Assign Share"]);
        });

        // 13. The Work queue, Summit County chosen.
        await Step(Document, 13, "press Work queue and choose Summit County from Agency", async () =>
        {
            await Page.Locator("#nav-queue").ClickAsync();
            await Expect(Page.Locator("#assignee-queue-heading")).ToHaveTextAsync("Work queue");

            await Choose("Agency", "Summit County");
        });

        // 14. The row, and its Accept button.
        await Step(Document, 14, "read the row and its Accept button", async () =>
        {
            await Expect(Page.Locator($"#queue-row-{reportId} td")).ToHaveTextAsync(
                [recordNumber, "Lyme disease", "Assigned to LHD", "Unknown", Today, "Investigating", "Accept"]);
            await Expect(Page.Locator($"#queue-row-{reportId}-advance")).ToHaveTextAsync("Accept");
        });

        // 15. Accept: the message, the row at Accepted, the next move offered.
        await Step(Document, 15, "press Accept and read the moved message, the row at Accepted and its next button", async () =>
            await Advance(reportId, recordNumber, "Accepted", "Begin investigation"));

        // 16. Begin investigation: the message, the row at Under
        //     investigation, the next move offered.
        await Step(Document, 16, "press Begin investigation and read the moved message, the row at Under investigation and its next button", async () =>
            await Advance(reportId, recordNumber, "Under investigation", "Close"));

        // 17. Close: the message, the row at Closed, and no button at all.
        await Step(Document, 17, "press Close and read the moved message, the row at Closed and its empty Action column", async () =>
        {
            await Page.Locator($"#queue-row-{reportId}-advance").ClickAsync();

            await Expect(Page.Locator("#queue-action-saved"))
                .ToHaveTextAsync($"Report {recordNumber} moved to Closed.");
            await Expect(Page.Locator("#queue-action-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#queue-row-{reportId}-workflow")).ToHaveTextAsync("Closed");
            await Expect(Page.Locator($"#queue-row-{reportId}-advance")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#queue-row-{reportId} td")).ToHaveTextAsync(
                [recordNumber, "Lyme disease", "Closed", "Unknown", Today, "Investigating", ""]);
        });

        // 18. The report's address again, read afresh from the database.
        await Step(Document, 18, "open the report's address again and read its header, now investigated by Summit County", async () =>
        {
            await Open(reportAddress);

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Lyme disease");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(recordNumber);
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Summit County");
        });
    }

    /// <summary>
    /// Presses the one button the case's row offers and reads what the
    /// document promises of every move: the message naming the record number
    /// and the state reached, the row's workflow cell reading that state, and
    /// the button now reading the next move. Steps 15 and 16 are this with
    /// different words; step 17 is not, because a closed row offers nothing.
    /// </summary>
    private async Task Advance(string reportId, string recordNumber, string reached, string nextMove)
    {
        await Page.Locator($"#queue-row-{reportId}-advance").ClickAsync();

        await Expect(Page.Locator("#queue-action-saved"))
            .ToHaveTextAsync($"Report {recordNumber} moved to {reached}.");
        await Expect(Page.Locator("#queue-action-error")).ToHaveCountAsync(0);
        await Expect(Page.Locator($"#queue-row-{reportId}-workflow")).ToHaveTextAsync(reached);
        await Expect(Page.Locator($"#queue-row-{reportId}-advance")).ToHaveTextAsync(nextMove);
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the creation
    /// walkthrough's test does it, and for the same reasons. The label is
    /// matched exactly, which is what tells the Agency board's **Agency** list
    /// from its **Assign to agency** and **Share with agency** lists.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
