using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/create-a-case-morbidity-report.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the three screens
/// carry for the words the document names — never by a MudBlazor class or a
/// cell's position. The two lists are opened and their item chosen by the
/// text the document names, which is what a reader does.
///
/// The record number is asserted against its shape and not its value, as the
/// document promises it: nine digits beginning with the current year, where
/// the sequence depends on how many reports this run has already opened. The
/// event date is the day the report was opened, so it is computed here rather
/// than written down.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class CreateACaseMorbidityReportTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/create-a-case-morbidity-report.md";

    [Fact]
    public async Task CreateACaseMorbidityReport()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. The browser tab, the heading, and the Step 1 of 2 lead-in.
        await Step(Document, 2, "read the browser tab, the heading and the Step 1 of 2 lead-in", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("New case morbidity report — Survsys");
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
            await Expect(Page.Locator("#new-report-lead-in")).ToContainTextAsync("Step 1 of 2.");
        });

        // 3. Zimmerman in Last name, then Search.
        await Step(Document, 3, "type Zimmerman in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 4. One person found, the row Zimmerman, Paul, and its Create CMR button.
        await Step(Document, 4, "read the count line, the Zimmerman, Paul row and its Create CMR button", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Zimmerman, Paul"]);
            await Expect(Page.Locator("#person-search-results [data-new-report-for]"))
                .ToHaveTextAsync("Create CMR");
        });

        // 5. Create CMR on that row.
        await Step(Document, 5, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 6. The same heading; the line now reads Step 2 of 2. Reporting on
        //    Zimmerman, Paul, followed by an ID the document does not print.
        await Step(Document, 6, "read the heading and the Step 2 of 2 line", async () =>
        {
            await Expect(Page.Locator("#new-report-details-heading")).ToHaveTextAsync("New case morbidity report");
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Zimmerman, Paul");
        });

        // 7. The date first reported to public health.
        await Step(Document, 7, "enter 2026-10-01 in First reported to public health", async () =>
            await Page.Locator("#report-first-reported").FillAsync("2026-10-01"));

        // 8. Malaria from the Disease list; the box then reads Malaria.
        await Step(Document, 8, "choose Malaria from Disease", async () =>
            await Choose("Disease", "Malaria"));

        // 9. Salt Lake County from the Investigating agency list; the box
        //    then reads Salt Lake County.
        await Step(Document, 9, "choose Salt Lake County from Investigating agency", async () =>
            await Choose("Investigating agency", "Salt Lake County"));

        // 10. Save & Continue.
        await Step(Document, 10, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 11. The report's own form: heading, tab, and the eight header fields.
        await Step(Document, 11, "read the report form's heading, browser tab and header bar", async () =>
        {
            var year = DateTime.Today.Year.ToString("D4", CultureInfo.InvariantCulture);
            var today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page).ToHaveTitleAsync(new Regex($"^Case morbidity report {year}\\d{{5}} — Survsys$"));

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(new Regex($"^{year}\\d{{5}}$"));
            await Expect(Page.Locator("#report-header-event-type")).ToHaveTextAsync("Morbidity");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Malaria");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Salt Lake County");
            await Expect(Page.Locator("#report-header-event-date")).ToHaveTextAsync(today);
            await Expect(Page.Locator("#report-header-mmwr")).ToHaveTextAsync(new Regex(@"^Week \d{1,2}, \d{4}$"));
            await Expect(Page.Locator("#report-header-case-status")).ToHaveTextAsync("Unknown");
        });

        // 12. The two panels, then the tab strip's eleven tabs.
        await Step(Document, 12, "read the Edit, Delete and Print buttons, the Case status and CDC notification panels and the tab strip", async () =>
        {
            await Expect(Page.Locator("#report-edit")).ToHaveTextAsync("Edit");
            await Expect(Page.Locator("#report-delete")).ToHaveTextAsync("Delete");
            await Expect(Page.Locator("#report-print")).ToHaveTextAsync("Print");
            await Expect(Page.Locator("#report-status")).ToContainTextAsync("Case status");
            await Expect(Page.Locator("#report-notify")).ToContainTextAsync("CDC notification");
            await Expect(Page.Locator("[id^='report-tab-']")).ToHaveTextAsync(
                ["Demographic", "Clinical", "Laboratory", "Investigation", "Administrative", "Forms", "Vaccines", "Treatments", "Encounters", "Contacts", "Notes"]);
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/>, as a reader does — by the words on it,
    /// exactly — and then reads that box, which now shows what was chosen.
    /// The reading is what the document promises at that step, and it is also
    /// what keeps the next step from pressing into a list that is still
    /// closing.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — even an empty one — renders the input carrying
    /// that identifier as <c>type="hidden"</c>, holding the raw value, and
    /// shows a sibling box beside it. The hidden input cannot be pressed, and
    /// it is not what the reader presses or reads: the box carrying the label
    /// is.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
