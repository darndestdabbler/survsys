using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/open-an-outbreak-and-link-cases.md</c>,
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
/// position. Every list is opened and its item chosen by the text the
/// document names, as the creation walkthrough's test does.
///
/// The outbreak's row is found by its name rather than by its id, since
/// identity assigns the ids; the suite empties the outbreaks on every run, so
/// the name is the one row it finds. The document has its reader note the
/// record number and the report's address at step 11, and this test keeps
/// both: the address is opened again at step 15, and the record number is
/// what the linked-cases row is asserted against at step 18.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class OpenAnOutbreakAndLinkCasesTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/open-an-outbreak-and-link-cases.md";

    /// <summary>The outbreak's name, as the document has the reader type it.</summary>
    private const string OutbreakName = "Provo church supper hepatitis A";

    /// <summary>The report's address, as the document has the reader note it.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/\d+$");

    [Fact]
    public async Task OpenAnOutbreakAndLinkCases()
    {
        // 1. Open the application tier's address and press Outbreaks.
        await Step(Document, 1, "open the application tier's address, press Outbreaks and read the screen", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-outbreaks").ClickAsync();

            await Expect(Page.Locator("#outbreaks-heading")).ToHaveTextAsync("Outbreaks");
            await Expect(Page.Locator("#outbreaks-new")).ToHaveTextAsync("New outbreak");
            await Expect(Page.Locator("#outbreaks-table thead th"))
                .ToHaveTextAsync(["Number", "Name", "Condition", "Owning agency", "Status"]);
        });

        // 2. New outbreak.
        await Step(Document, 2, "press New outbreak", async () =>
        {
            await Page.Locator("#outbreaks-new").ClickAsync();
            await Expect(Page.Locator("#outbreak-heading")).ToHaveTextAsync("New outbreak");
        });

        // 3. Number, Name and Outbreak date.
        await Step(Document, 3, "type the number and the name and enter the outbreak date", async () =>
        {
            await Page.Locator("#outbreak-number").FillAsync("OB-2026-014");
            await Page.Locator("#outbreak-name").FillAsync(OutbreakName);
            await Page.Locator("#outbreak-date").FillAsync("2026-09-12");
        });

        // 4. Hepatitis A from the Condition list.
        await Step(Document, 4, "choose Hepatitis A from Condition", async () =>
            await Choose("Condition", "Hepatitis A"));

        // 5. Utah County from the Owning agency list; Status left at Active.
        await Step(Document, 5, "choose Utah County from Owning agency and leave Status at Active", async () =>
        {
            await Choose("Owning agency", "Utah County");
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Status", Exact = true }))
                .ToHaveTextAsync("Active");
        });

        // 6. Save: back on the list, the outbreak's row.
        await Step(Document, 6, "press Save and read the outbreak's row on the Outbreaks screen", async () =>
        {
            await Page.Locator("#outbreak-save").ClickAsync();

            await Expect(Page.Locator("#outbreaks-heading")).ToHaveTextAsync("Outbreaks");
            await Expect(OutbreakRow().Locator("td")).ToHaveTextAsync(
                ["OB-2026-014", OutbreakName, "Hepatitis A", "Utah County", "Active"]);
        });

        // 7. New CMR, Nguyen in Last name, then Search.
        await Step(Document, 7, "press New CMR, type Nguyen in Last name and press Search", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Nguyen");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 8. One person found: Nguyen, An; Create CMR on that row.
        await Step(Document, 8, "read the count line and the Nguyen, An row, and press Create CMR", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Nguyen, An"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 9. Step 2 of 2. Reporting on Nguyen, An.
        await Step(Document, 9, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Nguyen, An"));

        // 10. Hepatitis A from Disease, Utah County from Investigating agency.
        await Step(Document, 10, "choose Hepatitis A from Disease and Utah County from Investigating agency", async () =>
        {
            await Choose("Disease", "Hepatitis A");
            await Choose("Investigating agency", "Utah County");
        });

        // 11. Save & Continue; note the record number and the address.
        var recordNumber = string.Empty;
        var reportAddress = string.Empty;

        await Step(Document, 11, "press Save & Continue, read the header and note the record number and the address", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Nguyen, An");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");
            await Expect(Page).ToHaveURLAsync(ReportAddress);

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
            reportAddress = Page.Url;
        });

        // 12. The Administrative tab and its Outbreak name list.
        await Step(Document, 12, "press the Administrative tab and find the Outbreak name list", async () =>
        {
            await Page.Locator("#report-tab-administrative").ClickAsync();

            await Expect(Page.Locator("#administrative-case")).ToContainTextAsync("Case / Outbreak");
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Outbreak name", Exact = true }))
                .ToBeVisibleAsync();
        });

        // 13. The outbreak from the Outbreak name list.
        await Step(Document, 13, "choose the outbreak from Outbreak name", async () =>
            await Choose("Outbreak name", OutbreakName));

        // 14. Save & Continue: the saved message.
        await Step(Document, 14, "press Save & Continue and read the saved message", async () =>
        {
            await Page.Locator("#administrative-save").ClickAsync();

            await Expect(Page.Locator("#administrative-saved"))
                .ToHaveTextAsync("The administrative data has been saved.");
        });

        // 15. The report's address again, read afresh from the database.
        await Step(Document, 15, "open the report's address again, press Administrative and read the Outbreak name", async () =>
        {
            await Open(reportAddress);
            await Page.Locator("#report-tab-administrative").ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Outbreak name", Exact = true }))
                .ToHaveTextAsync(OutbreakName);
        });

        // 16. Outbreaks.
        await Step(Document, 16, "press Outbreaks", async () =>
        {
            await Page.Locator("#nav-outbreaks").ClickAsync();
            await Expect(Page.Locator("#outbreaks-heading")).ToHaveTextAsync("Outbreaks");
        });

        // 17. The outbreak's name: its own screen.
        await Step(Document, 17, "press the outbreak's name and read the Edit outbreak screen", async () =>
        {
            await OutbreakRow().Locator("[data-outbreak-name] a").ClickAsync();

            await Expect(Page.Locator("#outbreak-heading")).ToHaveTextAsync("Edit outbreak");
            await Expect(Page.Locator("#outbreak-name")).ToHaveValueAsync(OutbreakName);
        });

        // 18. Linked cases: the one row.
        await Step(Document, 18, "read the one row under Linked cases", async () =>
        {
            await Expect(Page.Locator("#outbreak-cases")).ToContainTextAsync("Linked cases");
            await Expect(Page.Locator("#outbreak-cases-table thead th"))
                .ToHaveTextAsync(["Record number", "Person", "Condition"]);
            await Expect(Page.Locator("#outbreak-cases-table [data-case] td"))
                .ToHaveTextAsync([recordNumber, "Nguyen, An", "Hepatitis A"]);
        });
    }

    /// <summary>The outbreak's row on the Outbreaks screen, found by its name.</summary>
    private ILocator OutbreakRow() =>
        Page.Locator("#outbreaks-table [data-outbreak]").Filter(new() { HasText = OutbreakName });

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
