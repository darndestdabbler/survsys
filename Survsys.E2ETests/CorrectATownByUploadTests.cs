using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// A Connecticut town an administrator puts on file by uploading a town file,
/// used by the next address saved with it: the upload previewed and confirmed
/// on the Towns and zip codes screen, a Connecticut address saved with that
/// town alone on a report's Demographic tab, and its county and planning
/// region read back by name on the Demographic tab, the person screen and the
/// case search.
/// </summary>
/// <remarks>
/// <strong>This is not a walkthrough test</strong>, as
/// <see cref="SurvsysHarnessTests"/> is not: no document under
/// <c>docs/walkthroughs/</c> describes it, because the reference data screens
/// are this tier's own administration rather than a user story taken from the
/// legacy system. It drives the screens as a walkthrough would, by their
/// stable identifiers and the words on them.
///
/// The town is one the seeded towns do not hold, so the upload adds it — the
/// correction an administrator makes when the town list is missing one. The
/// report is on Paul Zimmerman, who has no address on file, so the address
/// saved is the one the test types. The database is reseeded before and after,
/// and the town deleted after, whether the steps pass or not.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class CorrectATownByUploadTests(SurvsysHarness harness) : SurvsysPageTest
{
    private const string Town = "Itestford";

    private const string County = "Hartford";

    private const string PlanningRegion = "Capitol Planning Region";

    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    [Fact]
    public async Task CorrectATownByUpload()
    {
        harness.Seed();

        try
        {
            await PerformTheSteps();
        }
        finally
        {
            harness.Seed();
            ForgetTheTown();
        }
    }

    private async Task PerformTheSteps()
    {
        // The town file, uploaded and previewed: one town added, nothing saved yet.
        await Open(harness.WebBaseAddress);
        await Page.Locator("#nav-places").ClickAsync();
        await Expect(Page.Locator("#places-heading")).ToHaveTextAsync("Towns and zip codes");

        await Page.Locator("#ct-towns-import").SetInputFilesAsync(new FilePayload
        {
            Name = "towns.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes($"town,county,planning_region\r\n{Town},09003,09110\r\n"),
        });

        await Expect(Page.Locator("#ct-towns-import-preview")).ToContainTextAsync("The file would add 1, change 0");
        await Expect(Page.Locator("#ct-towns-import-added-2")).ToHaveTextAsync($"Line 2 adds {Town}");
        await Expect(Page.Locator("#town-row-itestford")).ToHaveCountAsync(0);

        // Confirmed, and the town listed with its county and planning region by name.
        await Page.Locator("#ct-towns-import-confirm").ClickAsync();

        await Expect(Page.Locator("#ct-towns-import-done")).ToContainTextAsync("Imported: 1 added, 0 changed, 0 refused.");
        await Expect(Page.Locator("#town-row-itestford td")).ToHaveTextAsync([Town, County, PlanningRegion, "Edit"]);

        // A report on Paul Zimmerman, who has no address on file.
        await Page.Locator("#nav-new-report").ClickAsync();
        await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
        await Page.Locator("#person-search-search").ClickAsync();
        await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync(["Zimmerman, Paul"]);
        await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();

        await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "Disease", Exact = true }), "Giardiasis");
        await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "Investigating agency", Exact = true }), "Utah State");
        await Page.Locator("#report-save").ClickAsync();
        await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");

        var reportAddress = Page.Url;
        var match = ReportAddress.Match(reportAddress);
        Assert.True(match.Success, $"The address {reportAddress} does not end in /event/edit/ and a number.");
        var reportId = match.Groups[1].Value;

        // A Connecticut address with its town alone, typed in lower case, and saved.
        await Page.Locator("#report-edit").ClickAsync();
        await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();

        await Page.Locator("#demographics-address-add").ClickAsync();
        await Page.Locator("#demographics-address-0-street").FillAsync("12 Elm Street");
        await Page.Locator("#demographics-address-0-city").FillAsync(Town.ToLowerInvariant());
        await Choose(Page.GetByRole(AriaRole.Combobox, new() { Name = "State", Exact = true }), "Connecticut");

        await Page.Locator("#demographics-save").ClickAsync();
        await Expect(Page.Locator("#demographics-saved")).ToContainTextAsync("The demographics have been saved.");
        await Expect(Page.Locator("#demographics-error")).ToHaveCountAsync(0);

        // The report opened afresh: the county and planning region filled in from the town.
        await Open(reportAddress);

        await Expect(Page.Locator("#demographics-address-0-city")).ToHaveValueAsync(Town);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "County", Exact = true })).ToHaveTextAsync(County);
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Planning region", Exact = true }))
            .ToHaveTextAsync(PlanningRegion);

        // The person screen's address row, read left to right, the counties by name.
        await Page.Locator("#nav-people").ClickAsync();
        await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
        await Page.Locator("#person-search-search").ClickAsync();
        await Page.Locator("#person-search-results [data-person-name] a").ClickAsync();
        await Expect(Page.Locator("#person-heading")).ToHaveTextAsync("Zimmerman, Paul");

        await Expect(Page.Locator("#person-addresses-table tbody tr td"))
            .ToHaveTextAsync(["12 Elm Street", "—", Town, "CT", "—", County, PlanningRegion]);

        // The case search's row for the report, the counties by name.
        await Page.Locator("#nav-events").ClickAsync();
        await Page.Locator("#events-last-name").FillAsync("Zimmerman");
        await Page.Locator("#events-search").ClickAsync();

        await Expect(Page.Locator($"#events-row-{reportId}-county")).ToHaveTextAsync(County);
        await Expect(Page.Locator($"#events-row-{reportId}-county-equivalent")).ToHaveTextAsync(PlanningRegion);
    }

    /// <summary>
    /// Opens <paramref name="box"/>, chooses the item reading
    /// <paramref name="item"/> by the words on it, and reads the box, which
    /// now shows what was chosen — as the walkthrough tests do it.
    /// </summary>
    private async Task Choose(ILocator box, string item)
    {
        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }

    /// <summary>The uploaded town taken back off file: reseeding leaves the towns as they are.</summary>
    private void ForgetTheTown()
    {
        using var connection = new SqlConnection(harness.ConnectionString);
        connection.Open();

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM ct_towns WHERE town = @town";
        delete.Parameters.AddWithValue("@town", Town);
        delete.ExecuteNonQuery();
    }
}
