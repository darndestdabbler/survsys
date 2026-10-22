using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/correct-a-laboratory-result.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry — never
/// by a MudBlazor class or a cell's position. The specimen, its one test and
/// that test's one result are reached by the position the Laboratory tab puts
/// in their identifiers, which is how the document names them: the one
/// specimen, the one test, the one result under it. The report is opened on
/// <strong>Abbott, Jane</strong>, whom the search finds alone.
///
/// The report's address is kept from step 7 and opened again at step 15,
/// which is how the document has its reader prove the correction reached the
/// database rather than the screen alone.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class CorrectALaboratoryResultTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/correct-a-laboratory-result.md";

    private const string Accession = "B2026-0522";
    private const string Negative = "Negative / Non-reactive";
    private const string Positive = "Positive / Reactive";

    /// <summary>The one result row, in the one test, of the specimen open in the entry form.</summary>
    private const string Result = "#lab-entry-test-0-result-0";

    [Fact]
    public async Task CorrectALaboratoryResult()
    {
        var reportAddress = string.Empty;

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

        // 3. One person found, and Create CMR on that row.
        await Step(Document, 3, "read the count line and the Abbott, Jane row, and press Create CMR", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Abbott, Jane"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. Step 2 of 2. Reporting on Abbott, Jane.
        await Step(Document, 4, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Abbott, Jane"));

        // 5. Hepatitis A from the Disease list.
        await Step(Document, 5, "choose Hepatitis A from Disease", async () =>
            await Choose("Disease", "Hepatitis A"));

        // 6. Salt Lake County, then Save & Continue.
        await Step(Document, 6, "choose Salt Lake County from Investigating agency and press Save & Continue", async () =>
        {
            await Choose("Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();
        });

        // 7. The report's form, and its address kept for step 15.
        await Step(Document, 7, "read the report's header bar and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 8. Edit, then the Laboratory tab: no specimens, and the entry form.
        await Step(Document, 8, "press Edit, then the Laboratory tab, and read the empty Specimens table and the entry form", async () =>
        {
            await EditLaboratory();
            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveTextAsync(["No specimens recorded yet."]);
            await Expect(Page.Locator("#lab-entry")).ToContainTextAsync("Record a specimen");
        });

        // 9. The specimen.
        await Step(Document, 9, "type the accession number, enter the collection date and choose Serum", async () =>
        {
            await Page.Locator("#lab-entry-accession-no").FillAsync(Accession);
            await Page.Locator("#lab-entry-collection-date").FillAsync("2026-10-06");
            await Choose("Specimen source", "Serum");
        });

        // 10. The test.
        await Step(Document, 10, "press Add test, choose IgM Antibody and enter the test date", async () =>
        {
            await Page.Locator("#lab-entry-test-add").ClickAsync();
            await Choose("Test type", "IgM Antibody");
            await Page.Locator("#lab-entry-test-0-lab-test-date").FillAsync("2026-10-07");
        });

        // 11. The result, as first reported.
        await Step(Document, 11, "press Add result, type Negative, choose Negative / Non-reactive and tick Primary", async () =>
        {
            await Page.Locator("#lab-entry-test-0-result-add").ClickAsync();
            await Page.Locator($"{Result}-result-value").FillAsync("Negative");
            await Choose("Result code", Negative);
            await Page.Locator($"{Result}-primary").CheckAsync();
        });

        // 12. Record specimen.
        await Step(Document, 12, "press Record specimen and read the message and the one row", async () =>
        {
            await Page.Locator("#lab-entry-record").ClickAsync();

            await Expect(Page.Locator("#lab-entry-saved")).ToContainTextAsync("The specimen has been recorded.");
            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator("#lab-specimen-0 > td").Nth(0)).ToHaveTextAsync(Accession);
            await Expect(Page.Locator("#lab-specimen-0 > td").Nth(1)).ToHaveTextAsync("2026-10-06");
        });

        // 13. Edit on the row: the result as recorded.
        await Step(Document, 13, "press Edit on the specimen's row and read the result as recorded", async () =>
        {
            await Page.Locator("#lab-specimen-0-edit").ClickAsync();

            await Expect(Page.Locator("#lab-entry-edit-heading")).ToHaveTextAsync("Edit specimen");
            await Expect(Page.Locator("#lab-entry-accession-no")).ToHaveValueAsync(Accession);
            await ExpectResult("Negative", Negative);
        });

        // 14. The correction, saved.
        await Step(Document, 14, "replace the value with Positive, choose Positive / Reactive and press Save changes", async () =>
        {
            await Page.Locator($"{Result}-result-value").FillAsync("Positive");
            await Choose("Result code", Positive);
            await Page.Locator("#lab-entry-save").ClickAsync();

            await Expect(Page.Locator("#lab-entry-saved")).ToContainTextAsync("The specimen has been saved.");
            await Expect(Page.Locator("#lab-entry-edit-heading")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#lab-entry")).ToContainTextAsync("Record a specimen");
            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator("#lab-specimen-0 > td").Nth(0)).ToHaveTextAsync(Accession);
        });

        // 15. The report's address again, read afresh from the database.
        await Step(Document, 15, "open the report's address again, press Edit, Laboratory and the row's Edit, and read the corrected result", async () =>
        {
            await Open(reportAddress);
            await EditLaboratory();
            await Page.Locator("#lab-specimen-0-edit").ClickAsync();

            await Expect(Page.Locator("#lab-entry-edit-heading")).ToHaveTextAsync("Edit specimen");
            await ExpectResult("Positive", Positive);

            await Page.Locator("#lab-entry-cancel").ClickAsync();
            await Expect(Page.Locator("#lab-entry-edit-heading")).ToHaveCountAsync(0);
        });
    }

    /// <summary>
    /// Presses the report's Edit, then the Laboratory tab, and waits for its
    /// table, which is what the reader sees once the tab is open.
    /// </summary>
    private async Task EditLaboratory()
    {
        await Page.Locator("#report-edit").ClickAsync();
        await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
        await Page.Locator("#report-tab-laboratory").ClickAsync();
        await Expect(Page.Locator("#lab-specimens")).ToBeVisibleAsync();
    }

    /// <summary>The one result in the form, reading this value and code, and primary.</summary>
    private async Task ExpectResult(string value, string code)
    {
        await Expect(Page.Locator($"{Result}-result-value")).ToHaveValueAsync(value);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Result code", Exact = true }))
            .ToHaveTextAsync(code);
        await Expect(Page.Locator($"{Result}-primary")).ToBeCheckedAsync();
    }

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
