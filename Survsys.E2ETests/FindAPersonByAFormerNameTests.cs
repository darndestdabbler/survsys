using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/find-a-person-by-a-former-name.md</c>,
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
/// The walkthrough renames a seeded person, and other walkthroughs list the
/// seeded people by name, so the database is reseeded before the steps and
/// again after them, whether they pass or not — the reseeding the document
/// tells its reader to do, and the one <see cref="SurvsysHarnessTests"/>
/// already performs between walkthroughs. The former name is dated the day
/// it changed, which is today, so the date is read off the clock here rather
/// than written down.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class FindAPersonByAFormerNameTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/find-a-person-by-a-former-name.md";

    [Fact]
    public async Task FindAPersonByAFormerName()
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
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Abbott in Last name, then Search.
        await Step(Document, 2, "type Abbott in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Abbott");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found: Abbott, Jane.
        await Step(Document, 3, "read the count line and the Abbott, Jane row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Abbott, Jane"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Abbott, Jane.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Abbott, Jane"));

        // 6. Giardiasis and Utah State.
        await Step(Document, 6, "choose Giardiasis from Disease and Utah State from Investigating agency", async () =>
        {
            await Choose("Disease", "Giardiasis");
            await Choose("Investigating agency", "Utah State");
        });

        // 7. Save & Continue, landing on the report's form at its Demographic
        //    tab.
        await Step(Document, 7, "press Save & Continue and read the report form's header", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#demographics-name")).ToBeVisibleAsync();
        });

        // 8. Edit, and the last name changed to Kowalski.
        await Step(Document, 8, "press Edit and change Last name to Kowalski", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();

            await Expect(Page.Locator("#demographics-last-name")).ToHaveValueAsync("Abbott");
            await Page.Locator("#demographics-last-name").FillAsync("Kowalski");
        });

        // 9. Save & Continue, the saved message, and the old name kept under
        //    Former names, dated today.
        await Step(Document, 9, "press Save & Continue and read the saved message and Former names", async () =>
        {
            await Page.Locator("#demographics-save").ClickAsync();

            await Expect(Page.Locator("#demographics-saved")).ToContainTextAsync("The demographics have been saved.");
            await Expect(Page.Locator("#demographics-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#demographics-former-names [id^='demographics-former-name-']"))
                .ToHaveTextAsync([$"Abbott, Jane Marie — changed {today}"]);
        });

        // 10. People, landing on the People screen.
        await Step(Document, 10, "press People and read its heading", async () =>
        {
            await Page.Locator("#nav-people").ClickAsync();
            await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");
        });

        // 11. Abbott in Last name, then Search.
        await Step(Document, 11, "type Abbott in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Abbott");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 12. One person found, listed by her current name, scored against it.
        await Step(Document, 12, "read the count line and the Kowalski, Jane row with its score", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Kowalski, Jane"]);
            await Expect(Page.Locator("#person-search-results [data-person-score]"))
                .ToHaveTextAsync(["0 (None)"]);
            await Expect(Page.Locator("#person-search-results [data-person-why]"))
                .ToHaveTextAsync(["Last name differs"]);
        });

        // 13. Her name on the row opens her own screen: her current name, her
        //     former name until today, and the report under Cases.
        await Step(Document, 13, "press Kowalski, Jane and read her own screen", async () =>
        {
            await Page.Locator("#person-search-results [data-person-name] a").ClickAsync();

            await Expect(Page.Locator("#person-heading")).ToHaveTextAsync("Kowalski, Jane");
            await Expect(Page.Locator("#person-name")).ToHaveTextAsync("Kowalski, Jane");
            await Expect(Page.Locator("#person-former-names")).ToHaveTextAsync($"Abbott, Jane (until {today})");
            await Expect(Page.Locator("#person-cases-table tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator("#person-cases-table [id$='-disease']")).ToHaveTextAsync("Giardiasis");
        });
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
