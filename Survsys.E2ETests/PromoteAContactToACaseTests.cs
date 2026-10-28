using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/promote-a-contact-to-a-case.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class. The index case is opened on <strong>Okafor,
/// Chidi</strong>, and <strong>Zimmerman, Paul</strong> is added as the
/// contact, by the one row the search finds, and the contact row names him.
///
/// Step 11 follows the <strong>(contact event)</strong> link rather than
/// opening the contact's address: the link is a move from one report to
/// another on the same screen, which is what this walkthrough exists to prove
/// the screen follows.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class PromoteAContactToACaseTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/promote-a-contact-to-a-case.md";

    private const string Condition = "Hepatitis A";
    private const string Agency = "Summit County";
    private const string ExposureName = "Family dinner";

    /// <summary>The one contact of the one exposure.</summary>
    private const string Contact = "#contacts-exposure-0-contact-0";

    [Fact]
    public async Task PromoteAContactToACase()
    {
        var indexRecordNumber = string.Empty;
        var contactAddress = string.Empty;
        var contactRecordNumber = string.Empty;

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

        // 3. One person found, and Create CMR on that row.
        await Step(Document, 3, "read the count line and the Okafor, Chidi row, and press Create CMR", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Okafor, Chidi"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. The disease and the agency, then Save & Continue.
        await Step(Document, 4, "choose Hepatitis A and Summit County and press Save & Continue", async () =>
        {
            await Choose("Disease", Condition);
            await Choose("Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 5. The index case's header bar.
        await Step(Document, 5, "read the report's header bar and note the record number", async () =>
        {
            await ExpectHeader("Okafor, Chidi", "Morbidity");

            indexRecordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
        });

        // 6. Edit, then the Contacts tab with no exposures.
        await Step(Document, 6, "press Edit, then the Contacts tab, and read the empty Exposures table", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await Page.Locator("#report-tab-contacts").ClickAsync();

            await Expect(Page.Locator("#contacts-exposures tbody tr"))
                .ToHaveTextAsync(["No exposures recorded yet."]);
        });

        // 7. The exposure, recorded.
        await Step(Document, 7, "type the exposure's name and dates and press Record exposure", async () =>
        {
            await Page.Locator("#contacts-entry-name").FillAsync(ExposureName);
            await Page.Locator("#contacts-entry-exposure-start").FillAsync("2026-09-20");
            await Page.Locator("#contacts-entry-exposure-end").FillAsync("2026-09-20");
            await Page.Locator("#contacts-entry-record").ClickAsync();

            await Expect(Page.Locator("#contacts-entry-saved")).ToContainTextAsync("The exposure has been recorded.");
            await Expect(Page.Locator("#contacts-exposure-0-name")).ToHaveTextAsync(ExposureName);
            await Expect(Page.Locator("#contacts-exposure-1")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-exposure-0-contacts-none")).ToHaveTextAsync("—");
        });

        // 8. Add contact, and Household.
        await Step(Document, 8, "press Add contact on the row and choose Household from Contact type", async () =>
        {
            await Page.Locator("#contacts-exposure-0-add-contact").ClickAsync();

            await Expect(Page.Locator("#contacts-add")).ToContainTextAsync($"Add a contact to {ExposureName}");
            await Choose("Contact type", "Household");
        });

        // 9. Zimmerman found, and Add as contact on his row.
        await Step(Document, 9, "search Zimmerman in the panel and press Add as contact on the row", async () =>
        {
            var panel = Page.Locator("#contacts-add");

            await panel.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await panel.Locator("#person-search-search").ClickAsync();

            await Expect(panel.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Zimmerman, Paul"]);

            await panel.Locator("#person-search-results [data-person-action] button").ClickAsync();
        });

        // 10. The contact, linked to their contact event.
        await Step(Document, 10, "read the added message and the contact with its (contact event) link", async () =>
        {
            await Expect(Page.Locator("#contacts-entry-saved"))
                .ToHaveTextAsync("The contact has been added as a contact event.");
            await Expect(Page.Locator("#contacts-add")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#contacts-exposure-0-contacts tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator($"{Contact} td").Nth(0))
                .ToHaveTextAsync(new Regex(@"^\s*Zimmerman, Paul\s*\(contact event\)\s*$"));
            await Expect(Page.Locator($"{Contact} td").Nth(1)).ToHaveTextAsync("Household");
        });

        // 11. The link followed to the contact's own report.
        await Step(Document, 11, "press (contact event) and read the contact's report", async () =>
        {
            var link = Page.Locator($"{Contact}-event");
            var target = await link.GetAttributeAsync("href");

            await link.ClickAsync();

            Assert.NotNull(target);
            await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(target)}$"));
            await ExpectHeader("Zimmerman, Paul", "Contact");

            contactRecordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
            Assert.NotEqual(indexRecordNumber, contactRecordNumber);

            contactAddress = Page.Url;
        });

        // 12. Edit, and Promote to morbidity report.
        await Step(Document, 12, "press Edit and press Promote to morbidity report", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();

            await Expect(Page.Locator("#report-promote")).ToHaveTextAsync("Promote to morbidity report");
            await Page.Locator("#report-promote").ClickAsync();
        });

        // 13. Promoted.
        await Step(Document, 13, "read the promoted message and the header bar", async () =>
        {
            await Expect(Page.Locator("#report-promoted"))
                .ToHaveTextAsync("The contact event is now a morbidity report.");
            await Expect(Page.Locator("#report-promote-error")).ToHaveCountAsync(0);
            await ExpectHeader("Zimmerman, Paul", "Morbidity");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(contactRecordNumber);
        });

        // 14. The contact's address again, read afresh from the database.
        await Step(Document, 14, "open the contact's report address again and read it as a morbidity report", async () =>
        {
            await Open(contactAddress);

            await ExpectHeader("Zimmerman, Paul", "Morbidity");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(contactRecordNumber);
            await Expect(Page.Locator("#report-promote")).ToHaveCountAsync(0);
        });
    }

    /// <summary>
    /// The header bar of a report in this walkthrough: the patient and event
    /// type named, and the disease and agency every report here shares.
    /// </summary>
    private async Task ExpectHeader(string patient, string eventType)
    {
        await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(patient);
        await Expect(Page.Locator("#report-header-event-type")).ToHaveTextAsync(eventType);
        await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
        await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(Agency);
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
