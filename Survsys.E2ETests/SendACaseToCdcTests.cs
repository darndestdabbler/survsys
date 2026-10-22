using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/send-a-case-to-cdc.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It creates a case on
/// <strong>Okafor, Chidi</strong> for <strong>Lyme disease</strong>, which the
/// harness's guide fixture exports under a guide, and finds it on the queue and
/// in the log by its own record number, because every case the tests before it
/// queued is waiting and processed beside it. The harness gives the API tier
/// the file transport, so the send is a file in
/// <see cref="SurvsysHarness.OutboxDirectory"/>, which the last step reads as
/// the document's reader opens their own outbox folder.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SendACaseToCdcTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/send-a-case-to-cdc.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-02";

    private static readonly Regex Minute = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$");

    private static readonly Regex Written = new(@"^\s*Written to the outbox as (survsys-\d{17}-[0-9a-f]{32}\.hl7)\.\s*$");

    [Fact]
    public async Task SendACaseToCdc()
    {
        var recordNumber = string.Empty;
        var batchFile = string.Empty;

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
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
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

        // 7. The case waiting on the Notification queue.
        await Step(Document, 7, "press Notifications and find the case's row", async () =>
        {
            await Page.Locator("#nav-notification-queue").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Notification queue — Survsys");
            await Expect(Page.Locator("#notification-queue-heading")).ToHaveTextAsync("Notification queue");

            var row = QueueRow(recordNumber);

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-notification-patient]")).ToHaveTextAsync(PersonName);
            await Expect(row.Locator("[data-notification-condition]")).ToHaveTextAsync(Condition);
            await Expect(row.Locator("[data-notification-hold]")).ToHaveTextAsync("—");
        });

        // 8. Process the queue: the processed line, and the case gone from it.
        await Step(Document, 8, "press Process now, read the processed line and find the case gone", async () =>
        {
            await Page.Locator("#notification-queue-process").ClickAsync();
            await Expect(Page.Locator("#notification-queue-processed"))
                .ToHaveTextAsync(new Regex(@"^\s*\d+ case\(s\) processed; each is on the export log\.\s*$"));
            await Expect(Page.Locator("#notification-queue-process-error")).ToHaveCountAsync(0);
            await Expect(QueueRow(recordNumber)).ToHaveCountAsync(0);
        });

        // 9. The case's sent row on the Export log, and the file it was written to.
        await Step(Document, 9, "press Export log and read the case's row", async () =>
        {
            await Page.Locator("#nav-export-log").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");

            var row = Page.Locator("#export-log-table [data-export]")
                .Filter(new() { Has = Page.Locator("[data-export-record]", new() { HasTextString = recordNumber }) });

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-export-sent]")).ToHaveTextAsync(Minute);
            await Expect(row.Locator("[data-export-outcome]")).ToHaveTextAsync("accepted");
            await Expect(row.Locator("[data-export-detail]")).ToHaveTextAsync(Written);

            batchFile = Written.Match(await row.Locator("[data-export-detail]").InnerTextAsync()).Groups[1].Value;
        });

        // 10. The file in the outbox: the message, as it was sent.
        await Step(Document, 10, "open the outbox folder and read the file the detail names", async () =>
        {
            var path = Path.Combine(SurvsysHarness.OutboxDirectory, batchFile);

            Assert.True(File.Exists(path), $"{batchFile} is not in {SurvsysHarness.OutboxDirectory}.");

            var segments = (await File.ReadAllTextAsync(path)).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.StartsWith(@"MSH|^~\&|Survsys^", segments[0]);
            Assert.Contains("|ORU^R01^ORU_R01|", segments[0]);
            Assert.Equal("OBR|1|||68991-9^Epidemiologic Information^LN", segments[^1]);
        });
    }

    /// <summary>The notification queue's row for <paramref name="record"/>.</summary>
    private ILocator QueueRow(string record) =>
        Page.Locator("#notification-queue-table [data-notification-case]")
            .Filter(new() { Has = Page.Locator("[data-notification-record]", new() { HasTextString = record }) });

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
