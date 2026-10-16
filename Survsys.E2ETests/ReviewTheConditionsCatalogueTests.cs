using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/review-the-conditions-catalogue.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: this is the whole of the
/// walkthrough contract's second clause, and the reason this file is named
/// after the document rather than after a source file.
///
/// The one test method below performs the document's numbered steps in their
/// written order, one after another with nothing in between, and asserts at
/// each one what the document tells its reader they will see. It selects by
/// the stable identifiers the conditions screen carries for the words the
/// document names — never by a MudBlazor class or a cell's position — so the
/// document's wording and this test point at the same control.
///
/// The harness is already running both tiers over a seeded database and is
/// already recording video, because <see cref="SurvsysPageTest"/> turns
/// recording on for every test in this suite. Nothing here adds a fixture or
/// a recording of its own.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ReviewTheConditionsCatalogueTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs. Named once, and put into every
    /// failure by <see cref="Step"/>, because the suite's contract is that a
    /// failure names the walkthrough and the step number that could not be
    /// performed.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-the-conditions-catalogue.md";

    /// <summary>
    /// The seven rows the document's step 6 prints, in the order it prints
    /// them: alphabetical by name, which is the order the screen shows. The
    /// em dash is what the screen shows for a condition with no CDC code, and
    /// the document says so in those words.
    /// </summary>
    private static readonly (string Name, string CdcCode, string Active)[] Catalogue =
    [
        ("Bartonellosis", "—", "Yes"),
        ("Giardiasis", "11570", "Yes"),
        ("Hepatitis A", "10110", "Yes"),
        ("Influenza, animal isolates", "11070", "No"),
        ("Listeriosis", "10640", "Yes"),
        ("Lyme disease", "11080", "Yes"),
        ("Malaria", "10130", "Yes"),
    ];

    [Fact]
    public async Task ReviewTheConditionsCatalogue()
    {
        // 1. Open your browser and go to the application tier's address.
        await Step(Document, 1, "open the application tier's address", async () =>
            await Page.GotoAsync(harness.WebBaseAddress));

        // 2. The application bar reads Survsys; the browser tab reads
        //    Conditions — Survsys.
        await Step(Document, 2, "read the application bar and the browser tab", async () =>
        {
            await Expect(Page.GetByRole(AriaRole.Banner)).ToContainTextAsync("Survsys");
            await Expect(Page).ToHaveTitleAsync("Conditions — Survsys");
        });

        // 3. The heading Conditions, once the screen has the catalogue. The
        //    web-first assertion is the wait the document describes in words:
        //    it retries while the spinner is still there.
        await Step(Document, 3, "wait for the heading Conditions", async () =>
            await Expect(Page.Locator("#conditions-heading")).ToHaveTextAsync("Conditions"));

        // 4. The count line directly under the heading.
        await Step(Document, 4, "read the count line under the heading", async () =>
            await Expect(Page.Locator("#conditions-total"))
                .ToHaveTextAsync($"{Catalogue.Length} condition(s), showing {Catalogue.Length}."));

        // 5. The three column headings, left to right.
        await Step(Document, 5, "read the column headings", async () =>
            await Expect(Page.Locator("#conditions-table thead th"))
                .ToHaveTextAsync(["Name", "CDC code", "Active"]));

        // 6. Seven rows, in alphabetical order by name, with the CDC code and
        //    the Active the document prints for each.
        await Step(Document, 6, "read the seven rows of the catalogue", async () =>
        {
            await Expect(Page.Locator("#conditions-table tbody tr"))
                .ToHaveCountAsync(Catalogue.Length);
            await Expect(Page.Locator("#conditions-table [data-condition-name]"))
                .ToHaveTextAsync(Catalogue.Select(row => row.Name).ToArray());
            await Expect(Page.Locator("#conditions-table [data-condition-cdc-code]"))
                .ToHaveTextAsync(Catalogue.Select(row => row.CdcCode).ToArray());
            await Expect(Page.Locator("#conditions-table [data-condition-active]"))
                .ToHaveTextAsync(Catalogue.Select(row => row.Active).ToArray());
        });

        // 7. Malaria: the ordinary case, reportable and nationally notifiable.
        await Step(Document, 7, "read across the Malaria row", async () =>
        {
            var malaria = Page.Locator("[data-condition='Malaria']");

            await Expect(malaria.Locator("[data-condition-cdc-code]")).ToHaveTextAsync("10130");
            await Expect(malaria.Locator("[data-condition-active]")).ToHaveTextAsync("Yes");
        });

        // 8. Bartonellosis: reportable, and with no CDC code, which the screen
        //    shows as an em dash rather than as an empty cell.
        await Step(Document, 8, "read across the Bartonellosis row", async () =>
        {
            var bartonellosis = Page.Locator("[data-condition='Bartonellosis']");

            await Expect(bartonellosis.Locator("[data-condition-cdc-code]")).ToHaveTextAsync("—");
            await Expect(bartonellosis.Locator("[data-condition-active]")).ToHaveTextAsync("Yes");
        });

        // 9. Influenza, animal isolates: in the catalogue, with a CDC code,
        //    and not available for a new report.
        await Step(Document, 9, "read across the Influenza, animal isolates row", async () =>
        {
            var influenza = Page.Locator("[data-condition='Influenza, animal isolates']");

            await Expect(influenza.Locator("[data-condition-active]")).ToHaveTextAsync("No");
            await Expect(influenza.Locator("[data-condition-cdc-code]")).ToHaveTextAsync("11070");
        });
    }
}
