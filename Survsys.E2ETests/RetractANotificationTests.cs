using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/retract-a-notification.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It sends a case of its own
/// on <strong>Okafor, Chidi</strong> for <strong>Lyme disease</strong>, which
/// the harness's guide fixture exports under a guide, then retracts it, and
/// finds both exports in the log by the case's record number, newest first —
/// every case the tests before it queued is processed beside it.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RetractANotificationTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/retract-a-notification.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-02";

    /// <summary>
    /// The retraction's OBR: the Epidemiologic Information order with its
    /// result status, OBR-25, set to X.
    /// </summary>
    private const string RetractedOrder =
        "OBR|1|||68991-9^Epidemiologic Information^LN|||||||||||||||||||||X";

    [Fact]
    public async Task RetractANotification()
    {
        var recordNumber = string.Empty;

        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Search the person by last name.
        await Step(Document, 2, "type Okafor in Last name and press Search", async () =>
        {
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
            await Page.Locator("#person-search-last-name").FillAsync(Person);
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. Create the report on the one row.
        await Step(Document, 3, "press Create CMR on the Okafor, Chidi row", async () =>
        {
            await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync([PersonName]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. The report's details, then Save & Continue.
        await Step(Document, 4, "enter the date, choose the disease and agency, and save", async () =>
        {
            await Page.Locator("#report-first-reported").FillAsync(FirstReported);
            await Choose("Disease", Condition);
            await Choose("Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 5. The report's form, and the record number the case is followed by.
        await Step(Document, 5, "read the report's header bar and note the record number", async () =>
        {
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-record-number"))
                .ToHaveTextAsync(new Regex($"^{DateTime.Now.Year}\\d{{5}}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
        });

        // 6. Queue the case for notification.
        await Step(Document, 6, "press Edit, then Queue for notification, and read the confirmation", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Page.Locator("#report-notify-queue").ClickAsync();
            await Expect(Page.Locator("#report-notify-queued"))
                .ToContainTextAsync("This case was queued for notification at");
        });

        // 7. Send it.
        await Step(Document, 7, "press Notifications, then Process now, and read the processed line", async () =>
            await ProcessNow());

        // 8. The case's accepted export on the Export log, retracted.
        await Step(Document, 8, "press Export log, find the case's accepted row and press Retract", async () =>
        {
            await Page.Locator("#nav-export-log").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");

            var row = Rows(recordNumber);

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-export-outcome]")).ToHaveTextAsync("accepted");
            await row.GetByRole(AriaRole.Button, new() { Name = "Retract" }).ClickAsync();
        });

        // 9. The confirmation.
        await Step(Document, 9, "read the retraction's confirmation", async () =>
        {
            await Expect(Page.Locator("#export-log-action-done"))
                .ToHaveTextAsync($"A retraction of case {recordNumber} is queued (NOT118 = X).");
            await Expect(Page.Locator("#export-log-action-error")).ToHaveCountAsync(0);
        });

        // 10. Send the retraction.
        await Step(Document, 10, "press Notifications, then Process now, and read the processed line", async () =>
            await ProcessNow());

        // 11. Two exports of the case, the retraction newest.
        await Step(Document, 11, "press Export log and read the case's two rows", async () =>
        {
            await Page.Locator("#nav-export-log").ClickAsync();

            var rows = Rows(recordNumber);

            await Expect(rows).ToHaveCountAsync(2);
            await Expect(rows.Locator("[data-export-outcome]")).ToHaveTextAsync(["accepted", "accepted"]);
            await rows.First.Locator("[data-export-record] a").ClickAsync();
        });

        // 12. The retraction's message: its OBR carries the status X.
        await Step(Document, 12, "read the retraction's generated message", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Generated message — Survsys");
            await Expect(Page.Locator("#generated-message-review-record")).ToHaveTextAsync(recordNumber);

            var message = (await Page.Locator("#generated-message-review-message").InnerTextAsync())
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(["MSH", "OBR"], message.Select(line => line[..3]));
            Assert.Equal(RetractedOrder, message[1]);
        });
    }

    /// <summary>Presses Notifications, then Process now, and reads the processed line.</summary>
    private async Task ProcessNow()
    {
        await Page.Locator("#nav-notification-queue").ClickAsync();
        await Expect(Page.Locator("#notification-queue-heading")).ToHaveTextAsync("Notification queue");
        await Page.Locator("#notification-queue-process").ClickAsync();
        await Expect(Page.Locator("#notification-queue-processed"))
            .ToHaveTextAsync(new Regex(@"^\s*\d+ case\(s\) processed; each is on the export log\.\s*$"));
        await Expect(Page.Locator("#notification-queue-process-error")).ToHaveCountAsync(0);
    }

    /// <summary>The export log's rows for <paramref name="record"/>, newest first.</summary>
    private ILocator Rows(string record) =>
        Page.Locator("#export-log-table [data-export]")
            .Filter(new() { Has = Page.Locator("[data-export-record]", new() { HasTextString = record }) });

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
