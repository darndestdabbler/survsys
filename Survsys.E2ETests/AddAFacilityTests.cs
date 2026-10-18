using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/add-a-facility.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the Facilities and Edit
/// facility screens carry — never by a MudBlazor class or a cell's position.
///
/// The facility it adds is a row left behind, so the harness clears the
/// facilities table before it seeds (see
/// <see cref="SurvsysHarness.TablesInDeletionOrder"/>). That is what lets this
/// test assert the list holds exactly one facility — the one it just added —
/// rather than however many a run before it left.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class AddAFacilityTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/add-a-facility.md";

    private const string Name = "Davis County Health Clinic";
    private const string ShortName = "DCHC";
    private const string Agency = "Davis County";
    private const string Street = "22 South State Street";
    private const string City = "Clearfield";

    /// <summary>The two letters typed into State, which are stored upper-cased.</summary>
    private const string TypedState = "ut";

    /// <summary>The upper-cased state the save stores and reads back.</summary>
    private const string SavedState = "UT";

    private const string Zip = "84015";
    private const string Phone = "801-525-5000";

    [Fact]
    public async Task AddAFacility()
    {
        // 1. Open the application tier's address and press Facilities.
        await Step(Document, 1, "open the application tier's address and press Facilities", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-facilities").ClickAsync();
        });

        // 2. The browser tab, the heading and the New facility button.
        await Step(Document, 2, "read the browser tab, the heading and the New facility button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Facilities — Survsys");
            await Expect(Page.Locator("#facilities-heading")).ToHaveTextAsync("Facilities");
            await Expect(Page.Locator("#facilities-new")).ToContainTextAsync("New facility");
        });

        // 3. Press New facility.
        await Step(Document, 3, "press New facility", async () =>
            await Page.Locator("#facilities-new").ClickAsync());

        // 4. The New facility screen.
        await Step(Document, 4, "read the New facility heading and browser tab", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("New facility — Survsys");
            await Expect(Page.Locator("#facility-heading")).ToHaveTextAsync("New facility");
        });

        // 5. Fill the form in, box by box, choosing the agency from its list.
        await Step(Document, 5, "fill the facility's form in", async () =>
        {
            await Page.Locator("#facility-name").FillAsync(Name);
            await Page.Locator("#facility-short-name").FillAsync(ShortName);
            await Choose("Agency", Agency);
            await Page.Locator("#facility-street").FillAsync(Street);
            await Page.Locator("#facility-city").FillAsync(City);
            await Page.Locator("#facility-state").FillAsync(TypedState);
            await Page.Locator("#facility-zip").FillAsync(Zip);
            await Page.Locator("#facility-phone").FillAsync(Phone);
        });

        // 6. Press Save and land back on the Facilities list.
        await Step(Document, 6, "press Save and return to the Facilities list", async () =>
        {
            await Page.Locator("#facility-save").ClickAsync();
            await Expect(Page.Locator("#facilities-heading")).ToHaveTextAsync("Facilities");
        });

        // 7. The count line: the facility added is the only one on file.
        await Step(Document, 7, "read the count line above the list", async () =>
            await Expect(Page.Locator("#facilities-total"))
                .ToHaveTextAsync("1 facility(ies), showing 1."));

        // 8. The column headings and the one row the list holds.
        await Step(Document, 8, "read the column headings and the facility's row", async () =>
        {
            await Expect(Page.Locator("#facilities-table thead th"))
                .ToHaveTextAsync(["Name", "Agency", "City", "Zip"]);

            var row = Page.Locator($"#facilities-table [data-facility='{Name}']");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-facility-agency]")).ToHaveTextAsync(Agency);
            await Expect(row.Locator("[data-facility-city]")).ToHaveTextAsync(City);
            await Expect(row.Locator("[data-facility-zip]")).ToHaveTextAsync(Zip);
        });

        // 9. Press the facility's name to open it.
        await Step(Document, 9, "press the facility's name to open it", async () =>
            await Page.Locator("#facilities-table")
                .GetByRole(AriaRole.Link, new() { Name = Name })
                .ClickAsync());

        // 10. The whole record read back, with the state upper-cased.
        await Step(Document, 10, "read the facility's record back", async () =>
        {
            await Expect(Page.Locator("#facility-heading")).ToHaveTextAsync("Edit facility");
            await Expect(Page.Locator("#facility-name")).ToHaveValueAsync(Name);
            await Expect(Page.Locator("#facility-short-name")).ToHaveValueAsync(ShortName);
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Agency", Exact = true }))
                .ToHaveTextAsync(Agency);
            await Expect(Page.Locator("#facility-street")).ToHaveValueAsync(Street);
            await Expect(Page.Locator("#facility-city")).ToHaveValueAsync(City);
            await Expect(Page.Locator("#facility-state")).ToHaveValueAsync(SavedState);
            await Expect(Page.Locator("#facility-zip")).ToHaveValueAsync(Zip);
            await Expect(Page.Locator("#facility-phone")).ToHaveValueAsync(Phone);
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the other walkthrough
    /// tests in this suite do it. The label is matched exactly, so the box
    /// pressed and read back is the one named, never the hidden input a list
    /// that already holds a value puts its identifier on.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
