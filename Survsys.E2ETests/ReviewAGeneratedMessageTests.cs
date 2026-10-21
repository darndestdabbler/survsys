using System.Text.RegularExpressions;
using Microsoft.Playwright;
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
/// one the queue worker makes while the test watches: it creates a case on
/// <strong>Okafor, Chidi</strong> for <strong>Lyme disease</strong>, which the
/// harness's guide fixture exports under a guide, queues it, presses Process
/// now, and finds the export in the log by the case's own record number,
/// because every case the tests before it queued is processed in the same pass.
///
/// The generated message is asserted segment by segment rather than as one
/// string: its sending time and control id are the moment of the send, which
/// the document does not print, and the lines are what it promises.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ReviewAGeneratedMessageTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-a-generated-message.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-02";

    private static readonly Regex Minute = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$");

    [Fact]
    public async Task ReviewAGeneratedMessage()
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

        // 5. The report's form, and the record number the export is found by.
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

        // 7. Process the queue.
        await Step(Document, 7, "press Notifications, then Process now, and read the processed line", async () =>
        {
            await Page.Locator("#nav-notification-queue").ClickAsync();
            await Expect(Page.Locator("#notification-queue-heading")).ToHaveTextAsync("Notification queue");
            await Page.Locator("#notification-queue-process").ClickAsync();
            await Expect(Page.Locator("#notification-queue-processed"))
                .ToHaveTextAsync(new Regex(@"^\s*\d+ case\(s\) processed; each is on the export log\.\s*$"));
            await Expect(Page.Locator("#notification-queue-process-error")).ToHaveCountAsync(0);
        });

        // 8. The case's export on the Export log, opened by its record number.
        await Step(Document, 8, "press Export log, find the case's accepted row and press its record number", async () =>
        {
            await Page.Locator("#nav-export-log").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");

            var row = Row(recordNumber);

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-export-outcome]")).ToHaveTextAsync("accepted");
            await row.Locator("[data-export-record] a").ClickAsync();
        });

        // 9. The review screen.
        await Step(Document, 9, "read the browser tab, the heading and the back button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Generated message — Survsys");
            await Expect(Page.Locator("#generated-message-review-heading"))
                .ToHaveTextAsync(new Regex(@"^\s*Export \d+\s*$"));
            await Expect(Page.Locator("#generated-message-review-back")).ToHaveTextAsync("Back to the export log");
        });

        // 10. The export's summary.
        await Step(Document, 10, "read the export's summary", async () =>
        {
            await Expect(Page.Locator("#generated-message-review-record")).ToHaveTextAsync(recordNumber);
            await Expect(Page.Locator("#generated-message-review-guide")).ToHaveTextAsync(new Regex(@"^\s*\d+\s*$"));
            await Expect(Page.Locator("#generated-message-review-queued")).ToHaveTextAsync(Minute);
            await Expect(Page.Locator("#generated-message-review-sent")).ToHaveTextAsync(Minute);
            await Expect(Page.Locator("#generated-message-review-outcome")).ToHaveTextAsync("accepted");
            await Expect(Page.Locator("#generated-message-review-detail"))
                .ToHaveTextAsync(new Regex(@"^\s*Written to the outbox as survsys-\d{17}-[0-9a-f]{32}\.hl7\.\s*$"));
            await Expect(Page.Locator("#generated-message-review-processing")).ToHaveTextAsync("P");
            await Expect(Page.Locator("#generated-message-review-queue")).ToHaveTextAsync("production");
            await Expect(Page.Locator("#generated-message-review-messages")).ToHaveCountAsync(0);
        });

        // 11. The generated message, whole.
        await Step(Document, 11, "read the generated message whole", async () =>
        {
            await Expect(Page.GetByText("The message that was, or would be, sent to CDC for this export.")).ToBeVisibleAsync();
            await Expect(Page.Locator("#generated-message-review-none")).ToHaveCountAsync(0);

            var message = (await Page.Locator("#generated-message-review-message").InnerTextAsync())
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

            Assert.Equal(["MSH", "OBR"], message.Select(line => line[..3]));
            Assert.Matches(MessageHeader, message[0]);
            Assert.Equal(EpidemiologicInformation, message[1]);
        });
    }

    /// <summary>The header segment, up to the sending time and control id the document does not print.</summary>
    private static readonly Regex MessageHeader = new(
        @"^MSH\|\^~\\&\|Survsys\^2\.16\.840\.1\.114222\.4\.3\.3\.2\.1\^ISO\|CTDPH\^2\.16\.840\.1\.114222\.4\.1\.3650\^ISO"
        + @"\|CDC\^2\.16\.840\.1\.114222\.4\.3\.2\.10\^ISO\|CDC\^2\.16\.840\.1\.114222\.4\.1\.3650\^ISO"
        + @"\|\d{14}[^|]*\|\|ORU\^R01\^ORU_R01\|SURVSYS\d+\|P\|2\.5\.1\|+NOTF_ORU_v3\.0$");

    /// <summary>The one OBR, as the document prints it.</summary>
    private const string EpidemiologicInformation = "OBR|1|||68991-9^Epidemiologic Information^LN";

    /// <summary>The export log's row for <paramref name="record"/>.</summary>
    private ILocator Row(string record) =>
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
