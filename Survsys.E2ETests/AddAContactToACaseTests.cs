using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/add-a-contact-to-a-case.md</c>,
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
/// position.
///
/// The document has its reader note two things, and this test keeps both:
/// Maria Rodriguez's identifier, read off the People screen at step 3 because
/// the database assigns it and no document can name it, which is what is
/// typed into the contact's Person box and what the contact row is then
/// asserted against; and the report's address, opened again at step 16, which
/// is how the document has its reader prove the contact reached the database
/// rather than the screen alone.
///
/// The exposure's own cells are asserted one identifier at a time rather than
/// as the row's cells together, because an exposure with a contact holds a
/// table inside its Contacts cell, so "the row's cells" is no longer the six
/// the reader is looking at.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class AddAContactToACaseTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/add-a-contact-to-a-case.md";

    /// <summary>The exposure this walkthrough records, as the reader types it.</summary>
    private const string ExposureName = "Family dinner, 12 Oak Street";

    [Fact]
    public async Task AddAContactToACase()
    {
        // 1. Open the application tier's address and press People, landing on
        //    the People screen.
        await Step(Document, 1, "open the application tier's address, press People and read its heading", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-people").ClickAsync();

            await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");
            await Expect(Page.Locator("#people-lead-in"))
                .ToContainTextAsync("Leave every box empty to list everyone on file.");
        });

        // 2. Rodriguez in Last name, then Search.
        await Step(Document, 2, "type Rodriguez in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. Two people found, and Maria's identifier noted off her row.
        var contactPersonId = string.Empty;

        await Step(Document, 3, "read the count line, the two Rodriguez rows, and note Maria's ID", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("2 person(s) found, showing 2.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Rodriguez, Carlos", "Rodriguez, Maria"]);

            var maria = Page.Locator("#person-search-results tbody tr")
                .Filter(new() { HasText = "Rodriguez, Maria" });

            await Expect(maria).ToHaveCountAsync(1);

            contactPersonId = (await maria.Locator("[data-person-id]").InnerTextAsync()).Trim();

            Assert.True(
                int.TryParse(contactPersonId, out var identifier) && identifier > 0,
                $"The ID column of the Rodriguez, Maria row reads \"{contactPersonId}\", which is "
                + "not an identifier. The document has its reader note that number and type it "
                + "into the contact's Person box, so a column that does not hold one is a "
                + "defect in the screen or in the document.");
        });

        // 4. New CMR, landing on the creation screen's first step. Its
        //    heading is read before anything is typed, because the People
        //    screen's search boxes carry the same identifiers as this
        //    screen's: a box filled while the other screen is still on the
        //    page is a box the Search here never sees.
        await Step(Document, 4, "press New CMR in the application bar and read the creation screen's heading", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();

            await Expect(Page.Locator("#new-report-heading"))
                .ToHaveTextAsync("New case morbidity report");
            await Expect(Page.Locator("#new-report-lead-in")).ToContainTextAsync("Step 1 of 2.");
        });

        // 5. Zimmerman in Last name, then Search.
        await Step(Document, 5, "type Zimmerman in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 6. One person found: Zimmerman, Paul.
        await Step(Document, 6, "read the count line and the Zimmerman, Paul row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Zimmerman, Paul"]);
        });

        // 7. Create CMR on that row.
        await Step(Document, 7, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 8. Step 2 of 2. Reporting on Zimmerman, Paul.
        await Step(Document, 8, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Zimmerman, Paul"));

        // 9. Hepatitis A from the Disease list.
        await Step(Document, 9, "choose Hepatitis A from Disease", async () =>
            await Choose("Disease", "Hepatitis A"));

        // 10. Salt Lake County from the Investigating agency list.
        await Step(Document, 10, "choose Salt Lake County from Investigating agency", async () =>
            await Choose("Investigating agency", "Salt Lake County"));

        // 11. Save & Continue, the report's header, and its address kept for
        //     step 16.
        var reportAddress = string.Empty;

        await Step(Document, 11, "press Save & Continue, read the report form's header and note its address", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 12. The Contacts tab: no exposures yet, and no contact in the form.
        await Step(Document, 12, "press the Contacts tab and read the empty Exposures table and the Contacts section", async () =>
        {
            await OpenContactsTab();

            await Expect(Page.Locator("#contacts-exposures tbody tr"))
                .ToHaveTextAsync(["No exposures recorded yet."]);
            await Expect(Page.Locator("#contacts-entry")).ToBeVisibleAsync();
            await Expect(Page.Locator("#contacts-entry-contact-add")).ToHaveTextAsync("Add contact");
            await Expect(Page.Locator("#contacts-entry-contact-0")).ToHaveCountAsync(0);
        });

        // 13. The exposure's name and its two dates; no place.
        await Step(Document, 13, "type the exposure's Name and enter its Exposure start and Exposure end", async () =>
        {
            await Page.Locator("#contacts-entry-name").FillAsync(ExposureName);
            await Page.Locator("#contacts-entry-exposure-start").FillAsync("2026-09-10");
            await Page.Locator("#contacts-entry-exposure-end").FillAsync("2026-09-12");
        });

        // 14. Add contact, then Maria's identifier, her contact type and her
        //     disposition.
        await Step(Document, 14, "press Add contact and fill the contact's Person, Contact type, Disposition and Disposition date", async () =>
        {
            await Page.Locator("#contacts-entry-contact-add").ClickAsync();

            await Expect(Page.Locator("#contacts-entry-contact-0")).ToBeVisibleAsync();
            await Expect(Page.Locator("#contacts-entry-contact-remove-0")).ToHaveTextAsync("Remove");

            await Page.Locator("#contacts-entry-contact-0-person-id").FillAsync(contactPersonId);
            await Page.Locator("#contacts-entry-contact-0-contact-type").FillAsync("household");
            await Page.Locator("#contacts-entry-contact-0-disposition-id").FillAsync("3");
            await Page.Locator("#contacts-entry-contact-0-disposition-date").FillAsync("2026-09-22");
        });

        // 15. Record exposure: the message, the exposure with the contact
        //     inside it, and the form emptied.
        await Step(Document, 15, "press Record exposure and read the recorded message, the exposure with its contact and the emptied form", async () =>
        {
            await Page.Locator("#contacts-entry-record").ClickAsync();

            await Expect(Page.Locator("#contacts-entry-saved"))
                .ToContainTextAsync("The exposure has been recorded.");
            await Expect(Page.Locator("#contacts-entry-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#contacts-entry-complaint")).ToHaveCountAsync(0);

            await ReadTheExposureAndItsContact(contactPersonId);

            await Expect(Page.Locator("#contacts-entry-name")).ToHaveValueAsync(string.Empty);
            await Expect(Page.Locator("#contacts-entry-contact-0")).ToHaveCountAsync(0);
        });

        // 16. The report's address again, read afresh from the database.
        await Step(Document, 16, "open the report's address again, press Contacts and read the contact back", async () =>
        {
            await Open(reportAddress);
            await OpenContactsTab();

            await ReadTheExposureAndItsContact(contactPersonId);
        });
    }

    /// <summary>
    /// The one exposure and the one contact inside it, as the document
    /// promises them at steps 15 and 16: the exposure's own cells by the
    /// identifiers the tab puts on them, a dash for the place and for the
    /// sources this walkthrough leaves empty, and the contacts table's one
    /// row carrying the person, the type, the disposition, the disposition
    /// date and a dash for each end of a time range nobody entered.
    /// </summary>
    /// <remarks>
    /// Shared by the two steps that read it because the document says of the
    /// second that it reads what the first did, "exactly as in step 15" — and
    /// a copy of the assertions could drift from that promise.
    /// </remarks>
    private async Task ReadTheExposureAndItsContact(string contactPersonId)
    {
        // The one exposure, named by the position the tab puts in its
        // identifier: row zero is there and row one is not. Counting the
        // table's rows instead would count the contacts table's rows too,
        // since that table is inside this one's Contacts cell.
        await Expect(Page.Locator("#contacts-exposure-0")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#contacts-exposure-1")).ToHaveCountAsync(0);

        await Expect(Page.Locator("#contacts-exposure-0-name")).ToHaveTextAsync(ExposureName);
        await Expect(Page.Locator("#contacts-exposure-0-exposure-start")).ToHaveTextAsync("2026-09-10");
        await Expect(Page.Locator("#contacts-exposure-0-exposure-end")).ToHaveTextAsync("2026-09-12");
        await Expect(Page.Locator("#contacts-exposure-0-place")).ToHaveTextAsync("—");
        await Expect(Page.Locator("#contacts-exposure-0-sources-none")).ToHaveTextAsync("—");

        await Expect(Page.Locator("#contacts-exposure-0-contacts-none")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#contacts-exposure-0-contacts thead th")).ToHaveTextAsync(
            ["Person", "Type", "Disposition", "Disposition date", "From", "To"]);
        await Expect(Page.Locator("#contacts-exposure-0-contacts tbody tr")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#contacts-exposure-0-contact-0 td")).ToHaveTextAsync(
            [contactPersonId, "household", "3", "2026-09-22", "—", "—"]);
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
