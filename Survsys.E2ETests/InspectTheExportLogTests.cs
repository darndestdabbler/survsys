using System.Text.RegularExpressions;
using Microsoft.Playwright;
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
/// never by a MudBlazor class or a cell's position. The row it reads is one
/// the queue worker writes while the test watches: it creates a case on
/// <strong>Okafor, Chidi</strong> for <strong>Lyme disease</strong>, which the
/// harness's guide fixture exports under a guide, queues it and presses
/// Process now. Every case the tests before it queued is processed in the same
/// pass and logged beside it, so the count line is read for its shape and the
/// row by the case's own record number.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class InspectTheExportLogTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/inspect-the-export-log.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-02";

    private static readonly Regex Minute = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$");

    [Fact]
    public async Task InspectTheExportLog()
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

        // 5. The report's form, and the record number the row is found by.
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

        // 8. The Export log screen and its lead-in.
        await Step(Document, 8, "press Export log and read the browser tab, the heading and the lead-in", async () =>
        {
            await Page.Locator("#nav-export-log").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Export log — Survsys");
            await Expect(Page.Locator("#export-log-heading")).ToHaveTextAsync("Export log");
            await Expect(Page.GetByText(
                "The history of case exports to CDC, newest first. Open an export to read the "
                + "message that was generated for it, resend its case, or retract a notification "
                + "CDC accepted.")).ToBeVisibleAsync();
        });

        // 9. The count line.
        await Step(Document, 9, "read the count line", async () =>
        {
            await Expect(Page.Locator("#export-log-total"))
                .ToHaveTextAsync(new Regex(@"^\s*\d+ export\(s\), showing \d+\.\s*$"));
            await Expect(Page.Locator("#export-log-none")).ToHaveCountAsync(0);
        });

        // 10. The case's own row.
        await Step(Document, 10, "read the row for the case's record number", async () =>
        {
            var row = Page.Locator("#export-log-table [data-export]")
                .Filter(new() { Has = Page.Locator("[data-export-record]", new() { HasTextString = recordNumber }) });

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-export-queued]")).ToHaveTextAsync(Minute);
            await Expect(row.Locator("[data-export-sent]")).ToHaveTextAsync(Minute);
            await Expect(row.Locator("[data-export-outcome]")).ToHaveTextAsync("accepted");
            await Expect(row.Locator("[data-export-detail]"))
                .ToHaveTextAsync(new Regex(@"^\s*Written to the outbox as survsys-\d{17}-[0-9a-f]{32}\.hl7\.\s*$"));
            await Expect(row.Locator("[data-export-processing]")).ToHaveTextAsync("P");
            await Expect(row.Locator("[data-export-queue]")).ToHaveTextAsync("production");
            await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Resend" })).ToBeVisibleAsync();
            await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Retract" })).ToBeVisibleAsync();
        });
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
