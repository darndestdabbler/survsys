using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/queue-a-case-for-notification.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It creates the case it
/// queues through the New CMR screens, on <strong>Okafor, Chidi</strong> for
/// <strong>Lyme disease</strong>, a pair no other walkthrough reports on, and
/// finds its queue row by the report's own id, which the notification queue
/// keys each row by. That is what makes the row deterministic: every case the
/// tests before it created is on the queue too, since a case change queues a
/// case, so the count line is read for its shape and the row by its id. The
/// queue lives in a table the harness clears, so this test adds no clearing
/// of its own.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class QueueACaseForNotificationTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/queue-a-case-for-notification.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string FirstReported = "2026-10-02";

    /// <summary>The report's address, whose last segment is the id its queue row is keyed by.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    [Fact]
    public async Task QueueACaseForNotification()
    {
        var reportId = string.Empty;
        var recordNumber = string.Empty;

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
        await Step(Document, 3, "type Okafor in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync(Person);
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 4. One person found, with a Create CMR button.
        await Step(Document, 4, "read the count line and the Okafor, Chidi row", async () =>
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

        // 8. The disease — Lyme disease, which no other walkthrough reports on Okafor.
        await Step(Document, 8, "choose Lyme disease from Disease", async () =>
            await Choose("#report-disease", "Disease", Condition));

        // 9. The agency, then Save & Continue.
        await Step(Document, 9, "choose Davis County from Investigating agency and save", async () =>
        {
            await Choose("#report-agency", "Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 10. The report's form, and the record number the queue row is read by.
        await Step(Document, 10, "read the report's header bar and note the record number", async () =>
        {
            var year = DateTime.Now.Year;

            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(Agency);
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(new Regex($"^{year}\\d{{5}}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();

            var address = ReportAddress.Match(Page.Url);
            Assert.True(address.Success, $"The address {Page.Url} does not end in /event/edit/ and a number.");
            reportId = address.Groups[1].Value;
        });

        // 11. The CDC notification panel, and Queue for notification.
        await Step(Document, 11, "read the CDC notification panel and press Queue for notification", async () =>
        {
            await Expect(Page.Locator("#report-notify")).ToContainTextAsync("CDC notification");
            await Expect(Page.Locator("#report-notify")).ToContainTextAsync(
                "Queue this case to be notified to CDC. A case change queues it automatically; "
                + "this queues it again by hand.");
            await Page.Locator("#report-notify-queue").ClickAsync();
        });

        // 12. The queued confirmation.
        await Step(Document, 12, "read the queued confirmation", async () =>
        {
            await Expect(Page.Locator("#report-notify-queued")).ToHaveTextAsync(
                new Regex(@"^\s*This case was queued for notification at \d{4}-\d{2}-\d{2} \d{2}:\d{2}\.\s*$"));
            await Expect(Page.Locator("#report-notify-error")).ToHaveCountAsync(0);
        });

        // 13. Go to the Notification queue screen.
        await Step(Document, 13, "press Notification queue and read the screen", async () =>
        {
            await Page.Locator("#nav-notification-queue").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Notification queue — Survsys");
            await Expect(Page.Locator("#notification-queue-heading")).ToHaveTextAsync("Notification queue");
        });

        // 14. The count line, and the case's own row.
        await Step(Document, 14, "read the count line and the queued case's row", async () =>
        {
            await Expect(Page.Locator("#notification-queue-total"))
                .ToHaveTextAsync(new Regex(@"^\s*\d+ case\(s\) queued, showing \d+\.\s*$"));

            var row = Page.Locator($"#notification-queue-row-{reportId}");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-notification-record]")).ToHaveTextAsync(recordNumber);
            await Expect(row.Locator("[data-notification-patient]")).ToHaveTextAsync(PersonName);
            await Expect(row.Locator("[data-notification-condition]")).ToHaveTextAsync(Condition);
            await Expect(row.Locator("[data-notification-queued]"))
                .ToHaveTextAsync(new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$"));
            await Expect(row.Locator("[data-notification-hold]")).ToHaveTextAsync("—");
            await Expect(Page.Locator($"#notification-queue-hold-{reportId}")).ToHaveTextAsync("Hold");
        });
    }

    /// <summary>
    /// Chooses an item from a MudBlazor select, naming it by the list's id and
    /// the item by its text, and waits until the list shows the item chosen —
    /// the same way the other walkthrough tests in this suite do it. The label
    /// is matched exactly, so the combobox read back is the one named.
    /// </summary>
    private async Task Choose(string list, string label, string item)
    {
        await Page.Locator(list).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }))
            .ToHaveTextAsync(item);
    }
}
