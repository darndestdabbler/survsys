using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/save-a-view.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the Saved views screen
/// carries — never by a MudBlazor class or a cell's position. The list's cells
/// carry no identifiers of their own, so the view's row is found by its
/// <c>data-saved-view</c> name and read by the words it must contain.
///
/// A saved view is a row left behind, so the harness clears the saved views
/// table before it seeds (see
/// <see cref="Survsys.Uat.WalkthroughDatabase.TablesInDeletionOrder"/>). This test shares the
/// signed-in administrator and the one seeded database with every other test
/// in the collection, and more than one of them saves a view, so it never
/// asserts how many views the list holds — only that its own view is there,
/// exactly once, which is what proves a save over replaced the view rather
/// than adding a second.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SaveAViewTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/save-a-view.md";

    /// <summary>The view's name, unique among this user's views in the run.</summary>
    private const string ViewName = "Malaria cases";

    /// <summary>The condition filter the view selects by, by name.</summary>
    private const string Condition = "Malaria";

    /// <summary>The agency filter the save over adds, by name.</summary>
    private const string Agency = "Davis County";

    /// <summary>The columns as the list reads them back: the catalogue's order.</summary>
    private const string Columns = "record_number, last_name, condition";

    [Fact]
    public async Task SaveAView()
    {
        // 1. Open the application tier's address and press Saved views.
        await Step(Document, 1, "open the application tier's address and press Saved views", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-saved-views").ClickAsync();
        });

        // 2. The Saved views screen, with the form ready for a new view.
        await Step(Document, 2, "read the browser tab, the heading and the form heading", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Saved views — Survsys");
            await Expect(Page.Locator("#saved-views-heading")).ToHaveTextAsync("Saved views");
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
        });

        // 3. Name the view.
        await Step(Document, 3, "type the view's name", async () =>
            await Page.Locator("#saved-view-name").FillAsync(ViewName));

        // 4. Tick the three columns the view exports.
        await Step(Document, 4, "tick the record_number, last_name and condition columns", async () =>
        {
            await Page.Locator("#saved-view-column-record_number").ClickAsync();
            await Page.Locator("#saved-view-column-last_name").ClickAsync();
            await Page.Locator("#saved-view-column-condition").ClickAsync();
        });

        // 5. Choose the one filter, leaving the rest at Any.
        await Step(Document, 5, "choose Malaria from Condition", async () =>
            await Choose("Condition", Condition));

        // 6. Save the view; the form clears and is ready for the next.
        await Step(Document, 6, "press Save view and see the form clear", async () =>
        {
            await Page.Locator("#saved-view-save").ClickAsync();
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
        });

        // 7. The view read back in the list: its columns in catalogue order,
        //    and its one filter by the condition's name.
        await Step(Document, 7, "read the saved view back in the list", async () =>
        {
            var row = Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row).ToContainTextAsync(Columns);
            await Expect(row).ToContainTextAsync(Condition);
        });

        // 8. Edit the view: the form fills with it.
        await Step(Document, 8, "press Edit on the view's row", async () =>
        {
            await Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']")
                .GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true })
                .ClickAsync();

            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync($"Edit {ViewName}");
        });

        // 9. Add the agency filter and save over.
        await Step(Document, 9, "choose Davis County from Investigating agency and save over", async () =>
        {
            await Choose("Investigating agency", Agency);
            await Page.Locator("#saved-view-save").ClickAsync();
            await Expect(Page.Locator("#saved-view-form-heading")).ToHaveTextAsync("New saved view");
        });

        // 10. Still one row for the view, now filtered by condition and agency.
        await Step(Document, 10, "read that the one row now filters by condition and agency", async () =>
        {
            var row = Page.Locator($"#saved-views-table [data-saved-view='{ViewName}']");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row).ToContainTextAsync($"{Condition}; {Agency}");
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — even an empty one, as <strong>Condition</strong>
    /// opens on <strong>Any</strong> — renders the input carrying that
    /// identifier as <c>type="hidden"</c> and shows a sibling box beside it.
    /// The hidden input cannot be pressed, and it is not what the reader
    /// presses: the box carrying the label is.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
