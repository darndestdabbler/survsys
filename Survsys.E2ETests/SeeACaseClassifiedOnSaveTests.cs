using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/see-a-case-classified-on-save.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// No fixture seeds a chlamydia condition, so the test adds one, with Culture
/// approved for it, between two reseeds, which clear it again with everything
/// the case gathered — what the document's reader does on the Conditions and
/// Code lists screens before step 1. The page is marked before the save and
/// read after it, which is what proves the header changed without a reload.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SeeACaseClassifiedOnSaveTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/see-a-case-classified-on-save.md";

    private const string Chlamydia = "Chlamydia trachomatis infection";

    private const string Finding = "Chlamydia: a positive Chlamydia trachomatis lab confirms the case.";

    [Fact]
    public async Task SeeACaseClassifiedOnSave()
    {
        harness.Seed();

        try
        {
            AddChlamydia();
            await PerformTheSteps();
        }
        finally
        {
            harness.Seed();
        }
    }

    private async Task PerformTheSteps()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Search the person, and create the report on the one row.
        await Step(Document, 2, "search Nguyen and press Create CMR on the Nguyen, An row", async () =>
        {
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
            await Page.Locator("#person-search-last-name").FillAsync("Nguyen");
            await Page.Locator("#person-search-search").ClickAsync();
            await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync(["Nguyen, An"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 3. The disease and agency, then Save & Continue.
        await Step(Document, 3, "choose the disease and Salt Lake County, and save", async () =>
        {
            await Choose(Page, "Disease", Chlamydia);
            await Choose(Page, "Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();
        });

        // 4. A chlamydia case with no status.
        await Step(Document, 4, "read the disease and the Unknown case status in the header", async () =>
        {
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Chlamydia);
            await Expect(Page.Locator("#report-header-case-status")).ToHaveTextAsync("Unknown");
        });

        // 5. A positive culture, entered on the Laboratory tab.
        await Step(Document, 5, "press Edit, then Laboratory, and enter a positive Chlamydia trachomatis culture", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await Page.Locator("#report-tab-laboratory").ClickAsync();
            await Expect(Page.Locator("#lab-specimens")).ToBeVisibleAsync();

            await Page.Locator("#lab-entry-test-add").ClickAsync();
            var test = Page.Locator("#lab-entry-test-0");
            await Choose(test, "Test type", "Culture");

            await Page.Locator("#lab-entry-test-0-result-add").ClickAsync();
            var result = Page.Locator("#lab-entry-test-0-result-0");
            await Choose(result, "Organism", "Chlamydia trachomatis");
            await Choose(result, "Result code", "Positive / Reactive");
        });

        // 6. The save, and without a reload, the case confirmed and the finding beside it.
        await Step(Document, 6, "press Record specimen and, without a reload, read C in the header and the finding beside the save", async () =>
        {
            await Page.EvaluateAsync("() => window.notReloaded = true");

            await Page.Locator("#lab-entry-record").ClickAsync();

            await Expect(Page.Locator("#lab-entry-saved")).ToHaveTextAsync("The specimen has been recorded.");
            await Expect(Page.Locator("#report-header-case-status")).ToHaveTextAsync("C");
            await Expect(Page.Locator("#lab-classification-findings")).ToContainTextAsync(Finding);
            Assert.True(await Page.EvaluateAsync<bool>("() => window.notReloaded === true"));
        });
    }

    /// <summary>The chlamydia condition, with Culture approved for it.</summary>
    private void AddChlamydia()
    {
        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        using var insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO conditions (name, active) VALUES (@name, 1);
            INSERT INTO code_value_conditions (code_value_id, condition_id)
            SELECT code.id, SCOPE_IDENTITY()
            FROM code_values code
            WHERE code.list = N'test_type' AND code.code = N'Culture';
            """;
        insert.Parameters.AddWithValue("@name", Chlamydia);
        insert.ExecuteNonQuery();
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/> within
    /// <paramref name="scope"/>, chooses the item reading
    /// <paramref name="item"/>, and reads that box, which now shows it.
    /// </summary>
    private async Task Choose(IPage scope, string label, string item) =>
        await Choose(scope.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }), item);

    private async Task Choose(ILocator scope, string label, string item) =>
        await Choose(scope.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }), item);

    private async Task Choose(ILocator box, string item)
    {
        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
