using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/assign-and-accept-a-case.md</c>,
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
/// The document has its reader note two numbers at step 9 — the record
/// number from the header, and the report's id from the address — and this
/// test keeps both the same way: the record number is what the messages and
/// the row's first cell are asserted against, and the id is what is typed
/// into the Agency board's box and what the queue row's identifier carries,
/// which is how the row carrying the record number is found.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class AssignAndAcceptACaseTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/assign-and-accept-a-case.md";

    /// <summary>The report's address, as the document has the reader note it.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    /// <summary>
    /// The case's onset as the queue shows it: the day the report was opened,
    /// which is today, written as the document says it is.
    /// </summary>
    private static readonly string Today =
        DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task AssignAndAcceptACase()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Okafor in Last name, then Search.
        await Step(Document, 2, "type Okafor in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Okafor");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found: Okafor, Chidi.
        await Step(Document, 3, "read the count line and the Okafor, Chidi row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Okafor, Chidi"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Okafor, Chidi.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Okafor, Chidi"));

        // 6. Listeriosis from the Disease list.
        await Step(Document, 6, "choose Listeriosis from Disease", async () =>
            await Choose("#report-disease", "Disease", "Listeriosis"));

        // 7. Utah State from the Investigating agency list.
        await Step(Document, 7, "choose Utah State from Investigating agency", async () =>
            await Choose("#report-agency", "Investigating agency", "Utah State"));

        // 8. Save & Continue.
        await Step(Document, 8, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 9. The report's form, and the two numbers the reader notes: the
        //    record number from the header, the id from the address.
        var recordNumber = string.Empty;
        var reportId = string.Empty;

        await Step(Document, 9, "read the report form's header and note the record number and the report's id", async () =>
        {
            var year = DateTime.Today.Year.ToString("D4", CultureInfo.InvariantCulture);

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Okafor, Chidi");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Listeriosis");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Utah State");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(new Regex($"^{year}\\d{{5}}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();

            var address = ReportAddress.Match(Page.Url);
            Assert.True(address.Success, $"The address {Page.Url} does not end in /event/edit/ and a number.");
            reportId = address.Groups[1].Value;
        });

        // 10. The Agency board: its tab, heading, hint and two panels.
        await Step(Document, 10, "press Agency board and read its heading, hint and panels", async () =>
        {
            await Page.Locator("#nav-agency-queue").ClickAsync();

            await Expect(Page).ToHaveTitleAsync("Agency board — Survsys");
            await Expect(Page.Locator("#agency-queue-heading")).ToHaveTextAsync("Agency board");
            await Expect(Page.Locator("#work-queue-empty")).ToHaveTextAsync("Choose an agency to see its work queue.");
            await Expect(Page.Locator("#agency-queue-assign")).ToContainTextAsync("Assign a report");
            await Expect(Page.Locator("#agency-queue-share")).ToContainTextAsync("Share a report read-only");
        });

        // 11. Davis County on the board: its queue, without this report.
        await Step(Document, 11, "choose Davis County from Agency and read that the report is not in its queue", async () =>
        {
            await Choose("#agency-queue-select", "Agency", "Davis County");

            await Expect(Page.Locator("#work-queue-busy")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#work-queue-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#queue-row-{reportId}")).ToHaveCountAsync(0);
        });

        // 12. The report's id and Davis County in the Assign a report panel.
        await Step(Document, 12, "type the report's id in Report id and choose Davis County from Assign to agency", async () =>
        {
            await Page.Locator("#agency-queue-assign-report").FillAsync(reportId);
            await Choose("#agency-queue-assign-agency", "Assign to agency", "Davis County");
        });

        // 13. Assign: the message, and the row now on the board.
        await Step(Document, 13, "press Assign and read the assigned message and the row on the board", async () =>
        {
            await Page.Locator("#agency-queue-assign-save").ClickAsync();

            await Expect(Page.Locator("#agency-queue-assign-saved"))
                .ToHaveTextAsync($"Report {recordNumber} is assigned, at the assigned-to-LHD start.");
            await Expect(Page.Locator("#agency-queue-assign-error")).ToHaveCountAsync(0);

            await Expect(Page.Locator($"#queue-row-{reportId} td")).ToHaveTextAsync(
                [recordNumber, "Listeriosis", "Assigned to LHD", "Unknown", Today, "Investigating"]);
        });

        // 14. The Work queue: its tab, heading and hint.
        await Step(Document, 14, "press Work queue and read its heading and hint", async () =>
        {
            await Page.Locator("#nav-queue").ClickAsync();

            await Expect(Page).ToHaveTitleAsync("Work queue — Survsys");
            await Expect(Page.Locator("#assignee-queue-heading")).ToHaveTextAsync("Work queue");
            await Expect(Page.Locator("#work-queue-empty")).ToHaveTextAsync("Choose an agency to see its work queue.");
        });

        // 15. Davis County's queue: the row, and its Accept button.
        await Step(Document, 15, "choose Davis County from Agency and read the row and its Accept button", async () =>
        {
            await Choose("#queue-agency-select", "Agency", "Davis County");

            await Expect(Page.Locator($"#queue-row-{reportId}-record")).ToHaveTextAsync(recordNumber);
            await Expect(Page.Locator($"#queue-row-{reportId}-workflow")).ToHaveTextAsync("Assigned to LHD");
            await Expect(Page.Locator($"#queue-row-{reportId} td")).ToHaveTextAsync(
                [recordNumber, "Listeriosis", "Assigned to LHD", "Unknown", Today, "Investigating", "Accept"]);
            await Expect(Page.Locator($"#queue-row-{reportId}-advance")).ToHaveTextAsync("Accept");
        });

        // 16. Accept: the message, the row at Accepted, the next move offered.
        await Step(Document, 16, "press Accept and read the moved message, the row at Accepted and its next button", async () =>
        {
            await Page.Locator($"#queue-row-{reportId}-advance").ClickAsync();

            await Expect(Page.Locator("#queue-action-saved"))
                .ToHaveTextAsync($"Report {recordNumber} moved to Accepted.");
            await Expect(Page.Locator("#queue-action-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#queue-row-{reportId}-workflow")).ToHaveTextAsync("Accepted");
            await Expect(Page.Locator($"#queue-row-{reportId}-advance")).ToHaveTextAsync("Begin investigation");
        });

        // 17. The Agency board again, read afresh from the database.
        await Step(Document, 17, "press Agency board, choose Davis County again and read the row at Accepted", async () =>
        {
            await Page.Locator("#nav-agency-queue").ClickAsync();
            await Expect(Page.Locator("#agency-queue-heading")).ToHaveTextAsync("Agency board");

            await Choose("#agency-queue-select", "Agency", "Davis County");

            await Expect(Page.Locator($"#queue-row-{reportId}-record")).ToHaveTextAsync(recordNumber);
            await Expect(Page.Locator($"#queue-row-{reportId}-workflow")).ToHaveTextAsync("Accepted");
        });
    }

    /// <summary>
    /// Opens the list whose box carries <paramref name="list"/>, chooses the
    /// item reading <paramref name="item"/> by the words on it, and reads the
    /// box labelled <paramref name="label"/>, which now shows what was
    /// chosen — the same way the creation walkthrough's test does it, and
    /// for the same reasons. The label is matched exactly, which is what
    /// tells the Agency board's **Agency** list from its **Assign to agency**
    /// and **Share with agency** lists.
    /// </summary>
    private async Task Choose(string list, string label, string item)
    {
        await Page.Locator(list).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }))
            .ToHaveTextAsync(item);
    }
}
