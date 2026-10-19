using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/enter-a-laboratory-result.md</c>,
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
/// position. The two lists on the creation screen are opened and their item
/// chosen by the text the document names, as the creation walkthrough's test
/// does; the test block and the result row are reached by the position the
/// Laboratory tab puts in their identifiers, which is how the document names
/// them too: the one test, the one result under it.
///
/// The report's address is kept from step 9 and opened again at step 15,
/// which is how the document has its reader prove the specimen reached the
/// database rather than the screen alone.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class EnterALaboratoryResultTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/enter-a-laboratory-result.md";

    /// <summary>
    /// The one specimen row as the document promises it at steps 14 and 15:
    /// the accession number, the collection date, and a dash in each of the
    /// six columns this walkthrough leaves empty.
    /// </summary>
    private static readonly string[] SpecimenRow =
        ["A2026-0417", "2026-10-03", Serum, "—", "—", "—", "—", "—"];

    /// <summary>
    /// The number Serum has on the specimen source list, which is what the
    /// specimen stores and the Source column shows: the seed fixes it.
    /// </summary>
    private const string Serum = "558";

    /// <summary>
    /// The specimen row's cells but its last, which holds the Edit and Delete
    /// buttons rather than anything recorded.
    /// </summary>
    private const string SpecimenCells = "#lab-specimen-0 > td:not(:last-child)";

    [Fact]
    public async Task EnterALaboratoryResult()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Nguyen in Last name, then Search.
        await Step(Document, 2, "type Nguyen in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Nguyen");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found: Nguyen, An.
        await Step(Document, 3, "read the count line and the Nguyen, An row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Nguyen, An"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Nguyen, An.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Nguyen, An"));

        // 6. Hepatitis A from the Disease list.
        await Step(Document, 6, "choose Hepatitis A from Disease", async () =>
            await Choose("Disease", "Hepatitis A"));

        // 7. Salt Lake County from the Investigating agency list.
        await Step(Document, 7, "choose Salt Lake County from Investigating agency", async () =>
            await Choose("Investigating agency", "Salt Lake County"));

        // 8. Save & Continue.
        await Step(Document, 8, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 9. The report's form: the header names him and the disease, and
        //    the eleven tabs are there. Its address is kept for step 15.
        string reportAddress = string.Empty;

        await Step(Document, 9, "read the report form's header and tab strip, and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Nguyen, An");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");
            await Expect(Page.Locator("[id^='report-tab-']")).ToHaveTextAsync(
                ["Demographic", "Clinical", "Laboratory", "Investigation", "Administrative", "Forms", "Vaccines", "Treatments", "Encounters", "Contacts", "Notes"]);

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 10. The Laboratory tab: no specimens yet, and the empty entry form.
        await Step(Document, 10, "press the Laboratory tab and read the empty Specimens table and the entry form", async () =>
        {
            await OpenLaboratoryTab();

            await Expect(Page.Locator("#lab-specimens thead th")).ToHaveTextAsync(
                ["Accession", "Collected", "Source", "Lab facility", "Provider", "Diagnostic", "Reporting", "Sent to state", "Actions"]);
            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveTextAsync(["No specimens recorded yet."]);

            await Expect(Page.Locator("#lab-entry")).ToBeVisibleAsync();
            await Expect(Page.Locator("#lab-entry-test-add")).ToHaveTextAsync("Add test");
            await Expect(Page.Locator("#lab-entry-test-0")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#lab-entry-resist-add")).ToHaveTextAsync("Add susceptibility");
            await Expect(Page.Locator("#lab-entry-record")).ToHaveTextAsync("Record specimen");
        });

        // 11. The specimen's accession number, collection date and source.
        await Step(Document, 11, "type A2026-0417 in Accession number, enter 2026-10-03 in Collection date and choose Serum from Specimen source", async () =>
        {
            await Page.Locator("#lab-entry-accession-no").FillAsync("A2026-0417");
            await Page.Locator("#lab-entry-collection-date").FillAsync("2026-10-03");
            await Choose("Specimen source", "Serum");
        });

        // 12. Add test, then the test's type from its list, its LOINC code
        //     and its date.
        await Step(Document, 12, "press Add test, choose IgM Antibody from Test type and fill the test's LOINC code and Test date", async () =>
        {
            await Page.Locator("#lab-entry-test-add").ClickAsync();

            await Expect(Page.Locator("#lab-entry-test-0")).ToBeVisibleAsync();
            await Expect(Page.Locator("#lab-entry-test-0-result-add")).ToHaveTextAsync("Add result");
            await Expect(Page.Locator("#lab-entry-test-0-result-0")).ToHaveCountAsync(0);

            await Choose("Test type", "IgM Antibody");
            await Page.Locator("#lab-entry-test-0-loinc-code").FillAsync("20575-7");
            await Page.Locator("#lab-entry-test-0-lab-test-date").FillAsync("2026-10-04");
        });

        // 13. Add result, then the result's value, marked primary.
        await Step(Document, 13, "press Add result, type Positive in Value and tick Primary", async () =>
        {
            await Page.Locator("#lab-entry-test-0-result-add").ClickAsync();

            await Expect(Page.Locator("#lab-entry-test-0-result-0")).ToBeVisibleAsync();

            await Page.Locator("#lab-entry-test-0-result-0-result-value").FillAsync("Positive");
            await Page.Locator("#lab-entry-test-0-result-0-primary").CheckAsync();
        });

        // 14. Record specimen: the message, the one row, and the form emptied.
        await Step(Document, 14, "press Record specimen and read the recorded message, the one row and the emptied form", async () =>
        {
            await Page.Locator("#lab-entry-record").ClickAsync();

            await Expect(Page.Locator("#lab-entry-saved")).ToContainTextAsync("The specimen has been recorded.");
            await Expect(Page.Locator("#lab-entry-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#lab-entry-complaint")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator(SpecimenCells)).ToHaveTextAsync(SpecimenRow);

            await Expect(Page.Locator("#lab-entry-accession-no")).ToHaveValueAsync(string.Empty);
            await Expect(Page.Locator("#lab-entry-test-0")).ToHaveCountAsync(0);
        });

        // 15. The report's address again, read afresh from the database.
        await Step(Document, 15, "open the report's address again, press Laboratory and read the row back", async () =>
        {
            await Open(reportAddress);
            await OpenLaboratoryTab();

            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator(SpecimenCells)).ToHaveTextAsync(SpecimenRow);
        });
    }

    /// <summary>
    /// Presses the Laboratory tab and waits for its table, which is what the
    /// reader sees once the tab is open — and what keeps the next step from
    /// typing into a form that is not yet there.
    /// </summary>
    private async Task OpenLaboratoryTab()
    {
        await Page.Locator("#report-tab-laboratory").ClickAsync();
        await Expect(Page.Locator("#lab-specimens")).ToBeVisibleAsync();
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
