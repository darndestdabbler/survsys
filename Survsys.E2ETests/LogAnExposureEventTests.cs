using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/log-an-exposure-event.md</c>,
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
/// position. The two lists on the creation screen are opened and their item
/// chosen by the text the document names, as the creation walkthrough's test
/// does; the exposure row is reached by the position the Contacts tab puts
/// in its identifier, which is how the document names it too: the one row,
/// newest first.
///
/// The report's address is kept from step 9 and opened again at step 14,
/// which is how the document has its reader prove the exposure reached the
/// database rather than the screen alone.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class LogAnExposureEventTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/log-an-exposure-event.md";

    /// <summary>
    /// The one exposure row as the document promises it at steps 13 and 14:
    /// the name, the two dates, the place, and a dash under each of the two
    /// columns this walkthrough leaves empty.
    /// </summary>
    private static readonly string[] ExposureRow =
    [
        "Lakeside Community Pool outing",
        "2026-09-18",
        "2026-09-20",
        "Lakeside Community Pool",
        "—",
        "—",
    ];

    /// <summary>
    /// The exposure row's own cells but its last, which holds the Add contact,
    /// Edit and Delete buttons rather than anything recorded.
    /// </summary>
    private const string ExposureCells = "#contacts-exposure-0 > td:not(:last-child)";

    [Fact]
    public async Task LogAnExposureEvent()
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

        // 6. Giardiasis from the Disease list.
        await Step(Document, 6, "choose Giardiasis from Disease", async () =>
            await Choose("Disease", "Giardiasis"));

        // 7. Salt Lake County from the Investigating agency list.
        await Step(Document, 7, "choose Salt Lake County from Investigating agency", async () =>
            await Choose("Investigating agency", "Salt Lake County"));

        // 8. Save & Continue.
        await Step(Document, 8, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 9. The report's form: the header names him and the disease, and the
        //    eleven tabs are there. Its address is kept for step 14.
        var reportAddress = string.Empty;

        await Step(Document, 9, "read the report form's header and tab strip, and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Okafor, Chidi");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Giardiasis");
            await Expect(Page.Locator("[id^='report-tab-']")).ToHaveTextAsync(
                ["Demographic", "Clinical", "Laboratory", "Investigation", "Administrative", "Forms", "Vaccines", "Treatments", "Encounters", "Contacts", "Notes"]);

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 10. The Contacts tab: no exposures yet, and the empty entry form.
        await Step(Document, 10, "press Edit, then the Contacts tab, and read the empty Exposures table and the entry form", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await OpenContactsTab();

            await Expect(Page.Locator("#contacts-exposures thead th")).ToHaveTextAsync(
                ["Name", "From", "To", "Place", "Contacts", "Sources", "Actions"]);
            await Expect(Page.Locator("#contacts-exposures tbody tr"))
                .ToHaveTextAsync(["No exposures recorded yet."]);

            await Expect(Page.Locator("#contacts-entry")).ToBeVisibleAsync();
            await Expect(Page.Locator("#contacts-entry-place")).ToBeVisibleAsync();
            await Expect(Page.Locator("#contacts-entry-contact-add")).ToHaveTextAsync("Add contact");
            await Expect(Page.Locator("#contacts-entry-contact-0")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-entry-source-add")).ToHaveTextAsync("Add source");
            await Expect(Page.Locator("#contacts-entry-source-0")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-entry-record")).ToHaveTextAsync("Record exposure");
        });

        // 11. The exposure's name and its two dates.
        await Step(Document, 11, "type the exposure's Name and enter its Exposure start and Exposure end", async () =>
        {
            await Page.Locator("#contacts-entry-name").FillAsync("Lakeside Community Pool outing");
            await Page.Locator("#contacts-entry-exposure-start").FillAsync("2026-09-18");
            await Page.Locator("#contacts-entry-exposure-end").FillAsync("2026-09-20");
        });

        // 12. The place's type from its list, and its name, street and city.
        await Step(Document, 12, "choose Pool from Place type and fill the Place name, Street and City under Place", async () =>
        {
            await Choose("Place type", "Pool");
            await Page.Locator("#contacts-entry-place-name").FillAsync("Lakeside Community Pool");
            await Page.Locator("#contacts-entry-place-street").FillAsync("88 Lakeshore Drive");
            await Page.Locator("#contacts-entry-place-city").FillAsync("Provo");
        });

        // 13. Record exposure: the message, the one row, and the form emptied.
        await Step(Document, 13, "press Record exposure and read the recorded message, the one row and the emptied form", async () =>
        {
            await Page.Locator("#contacts-entry-record").ClickAsync();

            await Expect(Page.Locator("#contacts-entry-saved"))
                .ToContainTextAsync("The exposure has been recorded.");
            await Expect(Page.Locator("#contacts-entry-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-entry-complaint")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#contacts-exposures tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator(ExposureCells)).ToHaveTextAsync(ExposureRow);

            await Expect(Page.Locator("#contacts-entry-name")).ToHaveValueAsync(string.Empty);
            await Expect(Page.Locator("#contacts-entry-place-name")).ToHaveValueAsync(string.Empty);
            await Expect(Page.Locator("#contacts-entry-contact-0")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-entry-source-0")).ToHaveCountAsync(0);
        });

        // 14. The report's address again, read afresh from the database.
        await Step(Document, 14, "open the report's address again, press Contacts and read the row back", async () =>
        {
            await Open(reportAddress);
            await OpenContactsTab();

            await Expect(Page.Locator("#contacts-exposures tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator(ExposureCells)).ToHaveTextAsync(ExposureRow);
        });
    }

    /// <summary>
    /// Presses the Contacts tab and waits for its table, which is what the
    /// reader sees once the tab is open — and what keeps the next step from
    /// typing into a form that is not yet there.
    /// </summary>
    private async Task OpenContactsTab()
    {
        await Page.Locator("#report-tab-contacts").ClickAsync();
        await Expect(Page.Locator("#contacts-exposures")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the creation
    /// walkthrough's test does it, and for the same reasons.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
