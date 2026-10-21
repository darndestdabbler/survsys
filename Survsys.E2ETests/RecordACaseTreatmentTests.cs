using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/record-a-case-treatment.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It adds the treatment it
/// records, <strong>Amoxicillin</strong>, through the Treatments screen, with
/// a name no other walkthrough uses, so its row on that screen is found by its
/// name whatever other treatments the run has added. The case is opened on
/// <strong>Rodriguez, Maria</strong> for <strong>Lyme disease</strong>, a pair
/// no other walkthrough reports on, so its Treatments tab holds this
/// walkthrough's one record and nothing else.
///
/// The report's address is kept from step 7 and opened again at step 11,
/// which is how the document has its reader prove the treatment reached the
/// database rather than the screen alone.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RecordACaseTreatmentTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/record-a-case-treatment.md";

    private const string Treatment = "Amoxicillin";
    private const string Condition = "Lyme disease";
    private const string PersonName = "Rodriguez, Maria";
    private const string Dose = "500 mg three times daily";

    /// <summary>The one treatment record's row, as the document promises it at steps 10 and 11.</summary>
    private static readonly string[] RecordRow = [Treatment, "2026-10-08", "2026-10-29", Dose, ""];

    [Fact]
    public async Task RecordACaseTreatment()
    {
        var reportAddress = string.Empty;

        // 1. Treatments, then New treatment.
        await Step(Document, 1, "press Treatments, then New treatment", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-treatments").ClickAsync();
            await Page.Locator("#treatments-new").ClickAsync();

            await Expect(Page.Locator("#treatment-heading")).ToHaveTextAsync("New treatment");
        });

        // 2. Amoxicillin, listed for Lyme disease.
        await Step(Document, 2, "type Amoxicillin in Name and tick Lyme disease", async () =>
        {
            await Page.Locator("#treatment-name").FillAsync(Treatment);
            await Expect(TreatmentBox("Active")).ToBeCheckedAsync();
            await Expect(TreatmentBox("Default")).Not.ToBeCheckedAsync();
            await TreatmentBox(Condition).CheckAsync();
        });

        // 3. Save: the treatment's row.
        await Step(Document, 3, "press Save and read the treatment's row", async () =>
        {
            await Page.Locator("#treatment-save").ClickAsync();

            await Expect(Page.Locator("#treatments-heading")).ToHaveTextAsync("Treatments");
            await Expect(Page.Locator("#treatments-table [data-treatment]").Filter(new() { HasText = Treatment })
                .Locator("td")).ToHaveTextAsync([Treatment, "Yes", "No", "1"]);
        });

        // 4. New CMR, and Rodriguez searched.
        await Step(Document, 4, "press New CMR, type Rodriguez in Last name and press Search", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 5. Two found; Create CMR on Maria's row.
        await Step(Document, 5, "read the count line and press Create CMR on Rodriguez, Maria", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("2 person(s) found, showing 2.");

            var row = Page.Locator("#person-search-results tr[data-person]").Filter(new() { HasText = PersonName });

            await Expect(row).ToHaveCountAsync(1);
            await row.Locator("[data-new-report-for]").ClickAsync();
        });

        // 6. Lyme disease and Wasatch County, then Save & Continue.
        await Step(Document, 6, "choose Lyme disease and Wasatch County and press Save & Continue", async () =>
        {
            await Choose("Disease", Condition);
            await Choose("Investigating agency", "Wasatch County");
            await Page.Locator("#report-save").ClickAsync();
        });

        // 7. The report's form, and its address kept for step 11.
        await Step(Document, 7, "read the report's header bar and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 8. Edit, then the Treatments tab with nothing recorded.
        await Step(Document, 8, "press Edit, then the Treatments tab, and read the empty table and the form", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await OpenTreatmentsTab();

            await Expect(Page.Locator("#treatments-tab-list")).ToContainTextAsync("Treatments");
            await Expect(Page.Locator("#treatments-tab-table thead th"))
                .ToHaveTextAsync(["Treatment", "Started", "Stopped", "Dose", "Comment", ""]);
            await Expect(Page.Locator("#treatments-tab-empty")).ToHaveTextAsync("No treatments are recorded for this case.");
            await Expect(Page.Locator("#treatments-tab-form")).ToContainTextAsync("Add a treatment");
        });

        // 9. The treatment, its dates and its dose.
        await Step(Document, 9, "choose Amoxicillin from Treatment and enter its dates and dose", async () =>
        {
            await Choose("Treatment", Treatment);
            await Page.Locator("#treatments-tab-start").FillAsync("2026-10-08");
            await Page.Locator("#treatments-tab-stop").FillAsync("2026-10-29");
            await Page.Locator("#treatments-tab-dose").FillAsync(Dose);
        });

        // 10. Add treatment: the message, the emptied form and the one row.
        await Step(Document, 10, "press Add treatment and read the message, the emptied form and the one row", async () =>
        {
            await Page.Locator("#treatments-tab-save").ClickAsync();

            await Expect(Page.Locator("#treatments-tab-saved")).ToHaveTextAsync("The treatment record has been saved.");
            await Expect(Page.Locator("#treatments-tab-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#treatments-tab-dose")).ToHaveValueAsync(string.Empty);
            await ExpectTheRecord();
        });

        // 11. The report's address again, read afresh from the database.
        await Step(Document, 11, "open the report's address again, press Treatments and read the row back", async () =>
        {
            await Open(reportAddress);
            await OpenTreatmentsTab();

            await ExpectTheRecord();
        });
    }

    /// <summary>The Treatments tab, pressed, and its table waited for.</summary>
    private async Task OpenTreatmentsTab()
    {
        await Page.Locator("#report-tab-treatments").ClickAsync();
        await Expect(Page.Locator("#treatments-tab-table")).ToBeVisibleAsync();
    }

    /// <summary>
    /// The one treatment record, its cells but the last, which holds the Edit
    /// and Remove buttons rather than anything recorded.
    /// </summary>
    private async Task ExpectTheRecord()
    {
        var rows = Page.Locator("#treatments-tab-table [data-treatment]");

        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.Locator("td:not(:last-child)")).ToHaveTextAsync(RecordRow);
        await Expect(rows.Locator("td:last-child")).ToContainTextAsync("Edit");
        await Expect(rows.Locator("td:last-child")).ToContainTextAsync("Remove");
    }

    /// <summary>The treatment screen's tick box labelled <paramref name="label"/>.</summary>
    private ILocator TreatmentBox(string label) =>
        Page.Locator("#treatment-form").GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });

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
