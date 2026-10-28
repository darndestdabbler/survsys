using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/configure-a-notification-guide.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It configures a guide of
/// its own, never <see cref="SurvsysHarness.SeededGuide"/>, and exports no
/// condition under it, so nothing the CDC walkthroughs send is changed. A
/// header variable's row is keyed by an id identity assigns, so the row is
/// found by its variable's name. The guides and the PHIN variable catalogue
/// are tables the harness clears, so this test adds no clearing of its own.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ConfigureANotificationGuideTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/configure-a-notification-guide.md";

    private const string PhinVar = "INV169";
    private const string Description = "Condition";
    private const string DataType = "CWE";
    private const string Guide = "Configured guide";
    private const string Version = "Generic_MMG_V2.0";
    private const string HeaderVariable = "sending_facility";
    private const string HeaderDefault = "CTDPH^2.16.840.1.114222.4.1.3650^ISO";
    private const string HeaderValue = "CTDPH_TRAINING^2.16.840.1.114222.4.1.3650^ISO";

    [Fact]
    public async Task ConfigureANotificationGuide()
    {
        // 1. Open the application tier's address and press PHIN variables.
        await Step(Document, 1, "open the application tier's address and press PHIN variables", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-phin-variables").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("PHIN variables — Survsys");
            await Expect(Page.Locator("#phin-variables-heading")).ToHaveTextAsync("PHIN variables");
        });

        // 2. The variable the guide will carry, added to the catalogue.
        await Step(Document, 2, "enter the identifier, description and data type, and press Save", async () =>
        {
            await Page.Locator("#phin-variables-phin-var").FillAsync(PhinVar);
            await Page.Locator("#phin-variables-description").FillAsync(Description);
            await Page.Locator("#phin-variables-data-type").FillAsync(DataType);
            await Page.Locator("#phin-variables-save").ClickAsync();
        });

        // 3. Its row in the catalogue.
        await Step(Document, 3, "read the variable's row", async () =>
        {
            await Expect(Page.Locator("#phin-variables-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#phin-variables-row-{PhinVar} td").Nth(1)).ToHaveTextAsync(Description);
            await Expect(Page.Locator($"#phin-variables-row-{PhinVar} td").Nth(2)).ToHaveTextAsync(DataType);
        });

        // 4. The Notification guides screen.
        await Step(Document, 4, "press Notification guides and read the screen", async () =>
        {
            await Page.Locator("#nav-guides").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Notification guides — Survsys");
            await Expect(Page.Locator("#guides-heading")).ToHaveTextAsync("Notification guides");
            await Expect(Page.Locator("#guides-table").GetByRole(AriaRole.Link, new() { Name = SurvsysHarness.SeededGuide }))
                .ToBeVisibleAsync();
        });

        // 5. A new guide, which opens in its editor.
        await Step(Document, 5, "enter the new guide's name and version and press Add", async () =>
        {
            await Page.Locator("#guides-new-name").FillAsync(Guide);
            await Page.Locator("#guides-new-version").FillAsync(Version);
            await Page.Locator("#guides-new-save").ClickAsync();
        });

        // 6. The guide's editor.
        await Step(Document, 6, "read the editor's browser tab, heading and version", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Notification guide — Survsys");
            await Expect(Page.Locator("#guide-heading")).ToHaveTextAsync(Guide);
            await Expect(Page.Locator("#guide-version")).ToHaveValueAsync(Version);
        });

        // 7. The PHIN variable, added and saved.
        await Step(Document, 7, "add the PHIN variable and press Save variables", async () =>
        {
            await Page.Locator("#guide-phin-variable-add").FillAsync(PhinVar);
            await Page.GetByRole(AriaRole.Option, new() { Name = $"{PhinVar} {Description}", Exact = true }).ClickAsync();
            await Expect(Page.Locator("#guide-phin-variable-add")).ToHaveValueAsync(PhinVar);
            await Page.Locator("#guide-phin-variable-add-button").ClickAsync();
            await Expect(Page.Locator($"#guide-phin-variable-{PhinVar}")).ToBeVisibleAsync();
            await Page.Locator("#guide-phin-variables-save").ClickAsync();
            await Expect(Page.Locator("#guide-saved")).ToHaveTextAsync("The guide's variables were saved.");
            await Expect(Page.Locator("#guide-error")).ToHaveCountAsync(0);
        });

        // 8. The guide's own sending facility, over the default.
        await Step(Document, 8, "set the guide's own sending_facility and press Save header", async () =>
        {
            var row = HeaderRow();

            await Expect(row.Locator("td").Nth(1)).ToHaveTextAsync(HeaderDefault);
            await row.Locator("input").FillAsync(HeaderValue);
            await Page.Locator("#guide-variables-save").ClickAsync();
            await Expect(Page.Locator("#guide-saved")).ToHaveTextAsync("The guide's header was saved.");
            await Expect(Page.Locator("#guide-error")).ToHaveCountAsync(0);
        });

        // 9. Back to every guide, and the guide opened again.
        await Step(Document, 9, "press All guides, then the guide's name", async () =>
        {
            await Page.Locator("#guide-back").ClickAsync();
            await Expect(Page.Locator("#guides-heading")).ToHaveTextAsync("Notification guides");
            await Page.Locator("#guides-table").GetByRole(AriaRole.Link, new() { Name = Guide }).ClickAsync();
        });

        // 10. What was saved, read back.
        await Step(Document, 10, "read the guide's variable and header back", async () =>
        {
            await Expect(Page.Locator("#guide-heading")).ToHaveTextAsync(Guide);
            await Expect(Page.Locator($"#guide-phin-variable-{PhinVar}")).ToHaveTextAsync(PhinVar);
            await Expect(HeaderRow().Locator("input")).ToHaveValueAsync(HeaderValue);
        });
    }

    /// <summary>The header variables' row for <see cref="HeaderVariable"/>.</summary>
    private ILocator HeaderRow() =>
        Page.Locator("#guide-variables tbody tr")
            .Filter(new() { Has = Page.Locator("td", new() { HasTextString = HeaderVariable }) });
}
