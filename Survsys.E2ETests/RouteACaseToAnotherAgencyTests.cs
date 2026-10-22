using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/route-a-case-to-another-agency.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class. The case is opened on <strong>Nguyen,
/// An</strong> for <strong>Malaria</strong>, a pair no other walkthrough
/// reports on, so its workflow history holds this walkthrough's one move and
/// nothing else; the history's newest row is its first, which is how the
/// document names it. Its cells are read in order, since the table gives them
/// no identifiers of their own and the document reads them left to right.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RouteACaseToAnotherAgencyTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/route-a-case-to-another-agency.md";

    private const string From = "Davis County";
    private const string To = "Utah County";
    private const string Note = "Patient lives in Utah County";

    /// <summary>The workflow history's newest row, the top one.</summary>
    private const string NewestMove = "#administrative-history-0 > td";

    [Fact]
    public async Task RouteACaseToAnotherAgency()
    {
        var reportAddress = string.Empty;

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

        // 3. One person found, and Create CMR on that row.
        await Step(Document, 3, "read the count line and the Nguyen, An row, and press Create CMR", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Nguyen, An"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. Step 2 of 2. Reporting on Nguyen, An.
        await Step(Document, 4, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Nguyen, An"));

        // 5. Malaria from the Disease list.
        await Step(Document, 5, "choose Malaria from Disease", async () =>
            await Choose("Disease", "Malaria"));

        // 6. Davis County, then Save & Continue.
        await Step(Document, 6, "choose Davis County from Investigating agency and press Save & Continue", async () =>
        {
            await Choose("Investigating agency", From);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 7. The report's form under Davis County, and its address kept for step 12.
        await Step(Document, 7, "read the report's header bar and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Nguyen, An");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Malaria");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(From);

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 8. Edit, and the Workflow Options panel offering the route.
        await Step(Document, 8, "press Edit and read the Workflow Options panel", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();

            await Expect(Page.Locator("#report-workflow")).ToContainTextAsync("Workflow Options");
            await Expect(Page.Locator("#report-workflow-state")).ToHaveTextAsync("Assigned to LHD");
            await Expect(Page.Locator("#report-workflow-route")).ToHaveTextAsync("Route to agency");
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Route to agency", Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page.Locator("#report-workflow-note")).ToBeVisibleAsync();
        });

        // 9. The note, and Utah County.
        await Step(Document, 9, "type the note and choose Utah County from Route to agency", async () =>
        {
            await Page.Locator("#report-workflow-note").FillAsync(Note);
            await Choose("Route to agency", To);
        });

        // 10. Route to agency: the message, the header's agency and the note emptied.
        await Step(Document, 10, "press the Route to agency button and read the message and the header bar", async () =>
        {
            await Page.Locator("#report-workflow-route").ClickAsync();

            await Expect(Page.Locator("#report-workflow-saved"))
                .ToHaveTextAsync("Route to agency was made: the report is now Assigned to LHD.");
            await Expect(Page.Locator("#report-workflow-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(To);
            await Expect(Page.Locator("#report-workflow-note")).ToHaveValueAsync(string.Empty);
        });

        // 11. The Administrative tab's workflow history.
        await Step(Document, 11, "press the Administrative tab and read the workflow history's newest row", async () =>
            await ExpectTheMoveInTheHistory());

        // 12. The report's address again, read afresh from the database.
        await Step(Document, 12, "open the report's address again, read the header's agency and the workflow history", async () =>
        {
            await Open(reportAddress);

            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(To);
            await ExpectTheMoveInTheHistory();
        });
    }

    /// <summary>
    /// Presses the Administrative tab and reads the workflow history's headings
    /// and its newest row: when, who, the move, from, to and the note.
    /// </summary>
    private async Task ExpectTheMoveInTheHistory()
    {
        await Page.Locator("#report-tab-administrative").ClickAsync();

        var history = Page.Locator("#administrative-history");

        await Expect(history).ToContainTextAsync("Workflow history");
        await Expect(history.Locator("thead th")).ToHaveTextAsync(["When", "Who", "Move", "From", "To", "Note"]);
        await Expect(Page.Locator(NewestMove)).ToHaveTextAsync(
        [
            new Regex(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$"),
            new Regex($"^{SurvsysHarness.AdministratorUserName}$"),
            new Regex("^Route to agency$"),
            new Regex("^Assigned to LHD$"),
            new Regex("^Assigned to LHD$"),
            new Regex($"^{Note}$"),
        ]);
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
