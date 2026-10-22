using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// A case classified on save, seen without a reload: a positive
/// <i>Chlamydia trachomatis</i> culture recorded on the Laboratory tab of a
/// chlamydia case with no status turns the header's case status to Confirmed,
/// and the findings of that save are shown beside it.
/// </summary>
/// <remarks>
/// No walkthrough describes this yet — the Phase 12 walkthroughs will — so
/// <see cref="Document"/> names the session that asked for it. No fixture
/// seeds a chlamydia condition, so the test adds one, with Culture approved
/// for it, between two reseeds, which clear it again with everything the
/// case gathered. The page is marked before the save and read after it, which
/// is what proves the header changed without a reload.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SeeACaseClassifiedOnSaveTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>What a failure names, in place of a walkthrough document.</summary>
    private const string Document = "session 97: a case classified on save";

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
        await Step(Document, 1, "open a chlamydia case for Nguyen, An with no status", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Nguyen");
            await Page.Locator("#person-search-search").ClickAsync();
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
            await Choose(Page, "Disease", Chlamydia);
            await Choose(Page, "Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Chlamydia);
            await Expect(Page.Locator("#report-header-case-status")).ToHaveTextAsync("Unknown");
        });

        await Step(Document, 2, "press Edit, then Laboratory, and enter a positive Chlamydia trachomatis culture", async () =>
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

        await Step(Document, 3, "press Record specimen and, without a reload, read Confirmed in the header and the findings beside the save", async () =>
        {
            await Page.EvaluateAsync("() => window.notReloaded = true");

            await Page.Locator("#lab-entry-record").ClickAsync();

            await Expect(Page.Locator("#lab-entry-saved")).ToContainTextAsync("The specimen has been recorded.");
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
