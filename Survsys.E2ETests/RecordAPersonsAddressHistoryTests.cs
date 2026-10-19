using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/record-a-persons-address-history.md</c>,
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
/// position, save the person screen's address rows, whose cells the document
/// reads left to right.
///
/// The walkthrough changes Maria Rodriguez's addresses, which the record
/// demographics walkthrough reads as seeded, so the database is reseeded
/// before the steps and again after them, whether they pass or not — the
/// reseeding the document tells its reader to do. The report's address is
/// kept from step 5 and opened again at step 10, which is how the document
/// has its reader prove the save reached the database.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RecordAPersonsAddressHistoryTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/record-a-persons-address-history.md";

    [Fact]
    public async Task RecordAPersonsAddressHistory()
    {
        harness.Seed();

        try
        {
            await PerformTheSteps();
        }
        finally
        {
            harness.Seed();
        }
    }

    private async Task PerformTheSteps()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Rodriguez in Last name, Maria in First name, then Search.
        await Step(Document, 2, "type Rodriguez in Last name and Maria in First name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-first-name").FillAsync("Maria");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found, and Create CMR on that row.
        await Step(Document, 3, "read the Rodriguez, Maria row and press Create CMR on it", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Rodriguez, Maria"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. Giardiasis and Utah State.
        await Step(Document, 4, "choose Giardiasis from Disease and Utah State from Investigating agency", async () =>
        {
            await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "Disease", Exact = true }), "Giardiasis");
            await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "Investigating agency", Exact = true }), "Utah State");
        });

        // 5. Save & Continue, the report's header, and its address kept for
        //    step 10.
        var reportAddress = string.Empty;

        await Step(Document, 5, "press Save & Continue, read the report form's header and note its address", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Rodriguez, Maria");
            await Expect(Page.Locator("#demographics-addresses")).ToBeVisibleAsync();

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 6. The one address, undated.
        await Step(Document, 6, "read the one address in the Addresses section", async () =>
        {
            await Expect(Page.Locator("#demographics-address-0-street")).ToHaveValueAsync("1234 Main Street");
            await Expect(Page.Locator("#demographics-address-0-postal-code")).ToHaveValueAsync("84101");
            await Expect(Page.Locator("#demographics-address-0-beginning")).ToHaveValueAsync("");
            await Expect(Page.Locator("#demographics-address-0-ending")).ToHaveValueAsync("");
            await Expect(Page.Locator("#demographics-address-1-street")).ToHaveCountAsync(0);
        });

        // 7. Edit, and the current address dated from the day she moved in.
        await Step(Document, 7, "press Edit and type 2024-03-01 in the address's Earliest known", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();

            await Page.Locator("#demographics-address-0-beginning").FillAsync("2024-03-01");
        });

        // 8. The earlier address, typed in lower case with a nine-digit zip,
        //    and both its dates. Its State list is the second on the tab.
        await Step(Document, 8, "press Add address and fill in the earlier address with its dates", async () =>
        {
            await Page.Locator("#demographics-address-add").ClickAsync();

            await Page.Locator("#demographics-address-1-street").FillAsync("45 east 400 south");
            await Page.Locator("#demographics-address-1-city").FillAsync("salt lake city");
            await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "State", Exact = true }).Nth(1), "Utah");
            await Page.Locator("#demographics-address-1-postal-code").FillAsync("841111234");
            await Page.Locator("#demographics-address-1-beginning").FillAsync("2019-06-01");
            await Page.Locator("#demographics-address-1-ending").FillAsync("2024-03-01");
        });

        // 9. Save & Continue, and the message that the save succeeded.
        await Step(Document, 9, "press Save & Continue and read the saved message", async () =>
        {
            await Page.Locator("#demographics-save").ClickAsync();

            await Expect(Page.Locator("#demographics-saved")).ToContainTextAsync("The demographics have been saved.");
            await Expect(Page.Locator("#demographics-error")).ToHaveCountAsync(0);
        });

        // 10. The report's address again, read afresh: the current address
        //     open, the earlier one closed by its dates and made regular.
        await Step(Document, 10, "open the report's address again and read the two addresses back", async () =>
        {
            await Open(reportAddress);

            await Expect(Page.Locator("#demographics-address-0-street")).ToHaveValueAsync("1234 Main Street");
            await Expect(Page.Locator("#demographics-address-0-beginning")).ToHaveValueAsync("2024-03-01");
            await Expect(Page.Locator("#demographics-address-0-ending")).ToHaveValueAsync("");

            await Expect(Page.Locator("#demographics-address-1-street")).ToHaveValueAsync("45 East 400 South");
            await Expect(Page.Locator("#demographics-address-1-city")).ToHaveValueAsync("Salt Lake City");
            await Expect(Page.Locator("#demographics-address-1-postal-code")).ToHaveValueAsync("84111-1234");
            await Expect(Page.Locator("#demographics-address-1-beginning")).ToHaveValueAsync("2019-06-01");
            await Expect(Page.Locator("#demographics-address-1-ending")).ToHaveValueAsync("2024-03-01");
            await Expect(Page.Locator("#demographics-address-2-street")).ToHaveCountAsync(0);
        });

        // 11. People, the search for her, and her name on the one row.
        await Step(Document, 11, "press People, search for Rodriguez, Maria and press her name", async () =>
        {
            await Page.Locator("#nav-people").ClickAsync();
            await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");

            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-first-name").FillAsync("Maria");
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Rodriguez, Maria"]);
            await Page.Locator("#person-search-results [data-person-name] a").ClickAsync();
        });

        // 12. Her own screen's Addresses table: both addresses, in the order
        //     they were recorded, read left to right.
        await Step(Document, 12, "read the two rows of her Addresses table", async () =>
        {
            await Expect(Page.Locator("#person-heading")).ToHaveTextAsync("Rodriguez, Maria");

            var rows = Page.Locator("#person-addresses-table tbody tr");

            await Expect(rows).ToHaveCountAsync(2);
            await Expect(rows.Nth(0).Locator("td")).ToHaveTextAsync(
                ["1234 Main Street", "Apt 5B", "Salt Lake City", "UT", "84101", "SL"]);
            await Expect(rows.Nth(1).Locator("td")).ToHaveTextAsync(
                ["45 East 400 South", "—", "Salt Lake City", "UT", "84111-1234", "—"]);
        });
    }

    /// <summary>
    /// Opens <paramref name="box"/>, chooses the item reading
    /// <paramref name="item"/> by the words on it, and reads the box, which
    /// now shows what was chosen — the same way the creation walkthrough's
    /// test does it, and for the same reasons.
    /// </summary>
    private async Task Choose(ILocator box, string item)
    {
        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
