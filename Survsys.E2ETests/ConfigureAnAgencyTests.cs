using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/configure-an-agency.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the Agencies and Edit
/// agency screens carry — never by a MudBlazor class or a cell's position.
///
/// The agency it configures, Davis County, is one of the sixteen the database
/// migration seeds, and the harness does not clear or reseed those between
/// runs. So this test never asserts what the agency's Short name and
/// Jurisdiction zip codes boxes hold before it writes them — on a second run
/// they would hold what the first run saved. It overwrites both (a fill
/// replaces a box's contents) and asserts the saved result, which a save
/// makes the same however the boxes started: that is what makes configuring a
/// seeded agency idempotent, as the session plan promised.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ConfigureAnAgencyTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/configure-an-agency.md";

    /// <summary>The seeded agency this walkthrough configures.</summary>
    private const string Agency = "Davis County";

    /// <summary>The abbreviation the walkthrough sets.</summary>
    private const string ShortName = "Davis";

    /// <summary>
    /// What the reader types into the Jurisdiction zip codes box: two zip
    /// codes on the first line, a ZIP+4 on the second, and a repeat of the
    /// first line's second zip on the third.
    /// </summary>
    private const string TypedZipCodes = "84037, 84010\n84025-6789\n84010";

    /// <summary>
    /// What the box reads back after the save: the ZIP+4 cut to its zip, the
    /// duplicate stored once, and the list in ascending order however it was
    /// typed. The newline between the three is whatever the box renders, so
    /// the test matches any run of line breaks between them.
    /// </summary>
    private static readonly Regex SavedZipCodes =
        new(@"^84010[\r\n]+84025[\r\n]+84037$");

    [Fact]
    public async Task ConfigureAnAgency()
    {
        // 1. Open the application tier's address and press Agencies.
        await Step(Document, 1, "open the application tier's address and press Agencies", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-agencies").ClickAsync();
        });

        // 2. The browser tab and the heading say this is the Agencies screen.
        await Step(Document, 2, "read the browser tab, the heading and the New agency button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Agencies — Survsys");
            await Expect(Page.Locator("#agencies-heading")).ToHaveTextAsync("Agencies");
            await Expect(Page.Locator("#agencies-new")).ToContainTextAsync("New agency");
        });

        // 3. The single Name column, with Davis County among the agencies.
        await Step(Document, 3, "read that Davis County is in the Name column", async () =>
        {
            await Expect(Page.Locator("#agencies-table thead th")).ToHaveTextAsync(["Name"]);
            await Expect(Page.Locator($"#agencies-table [data-agency='{Agency}']")).ToHaveCountAsync(1);
        });

        // 4. Press Davis County.
        await Step(Document, 4, "press Davis County", async () =>
            await Page.Locator("#agencies-table")
                .GetByRole(AriaRole.Link, new() { Name = Agency })
                .ClickAsync());

        // 5. The Edit agency screen for Davis County. Its Name box names the
        //    agency opened; the other two boxes hold whatever it was last
        //    configured with, which this test does not read because a second
        //    run would find what the first run saved.
        await Step(Document, 5, "read the Edit agency heading and the Name box", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Edit agency — Survsys");
            await Expect(Page.Locator("#agency-heading")).ToHaveTextAsync("Edit agency");
            await Expect(Page.Locator("#agency-name")).ToHaveValueAsync(Agency);
        });

        // 6. Type the short name, which replaces whatever the box held.
        await Step(Document, 6, "type Davis in Short name", async () =>
            await Page.Locator("#agency-short-name").FillAsync(ShortName));

        // 7. Type the jurisdiction zip codes, unsorted, with a ZIP+4 and a
        //    duplicate, which the save reads before it stores.
        await Step(Document, 7, "type the jurisdiction zip codes", async () =>
            await Page.Locator("#agency-zip-codes").FillAsync(TypedZipCodes));

        // 8. Press Save and land back on the Agencies list.
        await Step(Document, 8, "press Save and return to the Agencies list", async () =>
        {
            await Page.Locator("#agency-save").ClickAsync();
            await Expect(Page.Locator("#agencies-heading")).ToHaveTextAsync("Agencies");
        });

        // 9. Press Davis County again, to read back from the database.
        await Step(Document, 9, "press Davis County again", async () =>
            await Page.Locator("#agencies-table")
                .GetByRole(AriaRole.Link, new() { Name = Agency })
                .ClickAsync());

        // 10. The save wrote the short name and the normalised zip codes.
        await Step(Document, 10, "read back the saved short name and zip codes", async () =>
        {
            await Expect(Page.Locator("#agency-heading")).ToHaveTextAsync("Edit agency");
            await Expect(Page.Locator("#agency-short-name")).ToHaveValueAsync(ShortName);
            await Expect(Page.Locator("#agency-zip-codes")).ToHaveValueAsync(SavedZipCodes);
        });
    }
}
