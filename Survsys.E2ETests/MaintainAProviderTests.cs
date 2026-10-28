using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/maintain-a-provider.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the Providers and Edit
/// provider screens carry — never by a MudBlazor class or a cell's position.
///
/// The provider it adds is a row left behind, so the harness clears the
/// providers table before it seeds (see
/// <see cref="Survsys.Uat.WalkthroughDatabase.TablesInDeletionOrder"/>). That
/// is what lets this test assert the list holds exactly one provider.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class MaintainAProviderTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/maintain-a-provider.md";

    private const string FirstName = "Rosa";
    private const string LastName = "Delgado";
    private const string ProviderType = "Clinician";
    private const string Phone = "860-555-0142";
    private const string Email = "rosa.delgado@example.org";

    [Fact]
    public async Task MaintainAProvider()
    {
        // 1. Open the application tier's address and press Providers.
        await Step(Document, 1, "open the application tier's address and press Providers", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-providers").ClickAsync();
        });

        // 2. The browser tab, the heading and the New provider button.
        await Step(Document, 2, "read the browser tab, the heading and the New provider button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Providers — Survsys");
            await Expect(Page.Locator("#providers-heading")).ToHaveTextAsync("Providers");
            await Expect(Page.Locator("#providers-new")).ToContainTextAsync("New provider");
        });

        // 3. Press New provider.
        await Step(Document, 3, "press New provider", async () =>
            await Page.Locator("#providers-new").ClickAsync());

        // 4. The New provider screen, Active already on.
        await Step(Document, 4, "read the New provider heading, browser tab and Active switch", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("New provider — Survsys");
            await Expect(Page.Locator("#provider-heading")).ToHaveTextAsync("New provider");
            await Expect(Active()).ToBeCheckedAsync();
        });

        // 5. Fill the form in, box by box, choosing the type from its list.
        await Step(Document, 5, "fill the provider's form in", async () =>
        {
            await Page.Locator("#provider-first-name").FillAsync(FirstName);
            await Page.Locator("#provider-last-name").FillAsync(LastName);
            await Choose("Provider type", ProviderType);
            await Page.Locator("#provider-phone").FillAsync(Phone);
            await Page.Locator("#provider-email").FillAsync(Email);
        });

        // 6. Press Save and land back on the Providers list.
        await Step(Document, 6, "press Save and return to the Providers list", async () =>
        {
            await Page.Locator("#provider-save").ClickAsync();
            await Expect(Page.Locator("#providers-heading")).ToHaveTextAsync("Providers");
        });

        // 7. Search by the start of the last name and read the one row.
        await Step(Document, 7, "search for Del and read the count, the headings and the row", async () =>
        {
            await Page.Locator("#providers-last-name").FillAsync("Del");
            await Page.Locator("#providers-search").ClickAsync();

            await Expect(Page.Locator("#providers-total")).ToHaveTextAsync("1 provider(s), showing 1.");
            await Expect(Page.Locator("#providers-table thead th"))
                .ToHaveTextAsync(["Last name", "First name", "Type", "Phone", "Email", "Active"]);

            var row = Row();

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-provider-first-name]")).ToHaveTextAsync(FirstName);
            await Expect(row.Locator("[data-provider-type]")).ToHaveTextAsync(ProviderType);
            await Expect(row.Locator("[data-provider-phone]")).ToHaveTextAsync(Phone);
            await Expect(row.Locator("[data-provider-email]")).ToHaveTextAsync(Email);
            await Expect(row.Locator("[data-provider-active]")).ToHaveTextAsync("Yes");
        });

        // 8. Press the last name to open the provider.
        await Step(Document, 8, "press Delgado to open the provider", async () =>
            await Page.Locator("#providers-table")
                .GetByRole(AriaRole.Link, new() { Name = LastName })
                .ClickAsync());

        // 9. The whole record read back, the type by its list entry.
        await Step(Document, 9, "read the provider's record back", async () =>
        {
            await Expect(Page.Locator("#provider-heading")).ToHaveTextAsync("Edit provider");
            await Expect(Page.Locator("#provider-first-name")).ToHaveValueAsync(FirstName);
            await Expect(Page.Locator("#provider-last-name")).ToHaveValueAsync(LastName);
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Provider type", Exact = true }))
                .ToHaveTextAsync(ProviderType);
            await Expect(Page.Locator("#provider-phone")).ToHaveValueAsync(Phone);
            await Expect(Page.Locator("#provider-email")).ToHaveValueAsync(Email);
            await Expect(Active()).ToBeCheckedAsync();
        });

        // 10. Switch Active off and save.
        await Step(Document, 10, "switch Active off and press Save", async () =>
        {
            await Active().UncheckAsync();
            await Page.Locator("#provider-save").ClickAsync();
            await Expect(Page.Locator("#providers-heading")).ToHaveTextAsync("Providers");
        });

        // 11. Still listed, now inactive.
        await Step(Document, 11, "read the provider still listed, now inactive", async () =>
        {
            await Expect(Page.Locator("#providers-total")).ToHaveTextAsync("1 provider(s), showing 1.");
            await Expect(Row().Locator("[data-provider-active]")).ToHaveTextAsync("No");
        });
    }

    private ILocator Row() => Page.Locator($"#providers-table [data-provider='{LastName}']");

    /// <summary>The Active switch, by its label.</summary>
    private ILocator Active() => Page.GetByLabel("Active", new() { Exact = true });

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
