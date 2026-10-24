using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/see-a-case-shared-read-only.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The document signs out and back in as the user it adds, and so does this
/// test, in its own browser context: the sign-out clears that context's cookie
/// alone, so the administrator's signed-in state the other tests open with is
/// untouched. The role and the user it adds are cleared by
/// <see cref="SurvsysHarness.Seed"/> on the next run, with everything else a
/// walkthrough fills.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SeeACaseSharedReadOnlyTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/see-a-case-shared-read-only.md";

    private const string RoleName = "Davis County investigator";

    private const string UserName = "davis.investigator";

    private const string DisplayName = "Davis Investigator";

    /// <summary>A password made up for this walkthrough's user, and no credential of anything else.</summary>
    private const string Password = "davis-walkthrough-password";

    [Fact]
    public async Task SeeACaseSharedReadOnly()
    {
        // 1. The Roles screen.
        await Step(Document, 1, "press Roles and read the New role form", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-roles").ClickAsync();

            await Expect(Page.Locator("#roles-heading")).ToHaveTextAsync("Roles");
            await Expect(Page.Locator("#role-form-heading")).ToHaveTextAsync("New role");
        });

        // 2. The role, holding view_cases and edit_cases.
        await Step(Document, 2, "name the role, tick view_cases and edit_cases, save, and read its row", async () =>
        {
            await Page.Locator("#role-name").FillAsync(RoleName);
            await Box("view_cases").CheckAsync();
            await Box("edit_cases").CheckAsync();
            await Page.Locator("#role-save").ClickAsync();

            await Expect(Page.Locator("#roles-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#roles-table tr[data-role='{RoleName}'] td").Nth(0)).ToHaveTextAsync(RoleName);
            await Expect(Page.Locator($"#roles-table tr[data-role='{RoleName}'] td").Nth(1))
                .ToHaveTextAsync("view_cases, edit_cases");
        });

        // 3. The user, holding the role and working for Davis County.
        await Step(Document, 3, "add the user with the role and Davis County, save, and read its row", async () =>
        {
            await Page.Locator("#nav-users").ClickAsync();
            await Expect(Page.Locator("#users-heading")).ToHaveTextAsync("Users");
            await Expect(Page.Locator("#user-form-heading")).ToHaveTextAsync("New user");

            await Page.Locator("#user-user-name").FillAsync(UserName);
            await Page.Locator("#user-display-name").FillAsync(DisplayName);
            await Page.Locator("#user-password").FillAsync(Password);
            await Expect(Box("May sign in")).ToBeCheckedAsync();
            await Box(RoleName).CheckAsync();
            await Box("Davis County").CheckAsync();
            await Page.Locator("#user-save").ClickAsync();

            await Expect(Page.Locator("#users-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator($"#users-table tr[data-user='{UserName}'] td")).ToHaveTextAsync(
                [UserName, DisplayName, RoleName, "Davis County", "Yes", "Edit"]);
        });

        // 4. The report, under Salt Lake County, its id and record number noted.
        var reportId = string.Empty;
        var recordNumber = string.Empty;

        await Step(Document, 4, "open a Listeriosis report on Abbott, Jane under Salt Lake County and note its id and record number", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Abbott");
            await Page.Locator("#person-search-search").ClickAsync();
            await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync(["Abbott, Jane"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();

            await Choose("Disease", "Listeriosis");
            await Choose("Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\d{9}$"));

            reportId = Page.Url[(Page.Url.LastIndexOf('/') + 1)..];
            Assert.Matches(@"^\d+$", reportId);
            recordNumber = (await Page.Locator("#report-header-record-number").InnerTextAsync()).Trim();
        });

        // 5. The share, from the Agency board.
        await Step(Document, 5, "share the report read-only with Davis County on the Agency board", async () =>
        {
            await Page.Locator("#nav-agency-queue").ClickAsync();
            await Expect(Page.Locator("#agency-queue-heading")).ToHaveTextAsync("Agency board");

            await Page.Locator("#agency-queue-share-report").FillAsync(reportId);
            await Choose("Share with agency", "Davis County");
            await Page.Locator("#agency-queue-share-save").ClickAsync();

            await Expect(Page.Locator("#agency-queue-share-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#agency-queue-share-saved"))
                .ToHaveTextAsync($"Report {reportId} is shared read-only with Davis County.");
        });

        // 6. Signed out, and signed in again as the user.
        await Step(Document, 6, "sign out, sign in as davis.investigator and read the name in the application bar", async () =>
        {
            await Page.Locator("#nav-sign-out").ClickAsync();
            await Expect(Page.Locator("#sign-in-heading")).ToHaveTextAsync("Sign in");

            await Page.Locator("#sign-in-user-name").FillAsync(UserName);
            await Page.Locator("#sign-in-password").FillAsync(Password);
            await Page.Locator("#sign-in-submit").ClickAsync();

            await Page.WaitForURLAsync(url => !url.Contains("/sign-in", StringComparison.Ordinal));
            await Expect(Page.Locator("#nav-signed-in-as")).ToHaveTextAsync(DisplayName);
        });

        // 7. Davis County's board, and the shared row on it.
        await Step(Document, 7, "press Agency board, choose Davis County and read the shared row", async () =>
        {
            // The sign-in lands on a page loaded afresh; opening it again waits,
            // as every walkthrough's first screen does, for the circuit that
            // answers the Agency list.
            await Open(Page.Url);
            await Page.Locator("#nav-agency-queue").ClickAsync();
            await Expect(Page.Locator("#agency-queue-heading")).ToHaveTextAsync("Agency board");

            await Choose("Agency", "Davis County");

            var cells = Page.Locator($"#queue-row-{reportId} td");
            await Expect(cells.Nth(0)).ToHaveTextAsync(recordNumber);
            await Expect(cells.Nth(1)).ToHaveTextAsync("Listeriosis");
            await Expect(cells.Nth(5)).ToHaveTextAsync("Shared read-only");
        });

        // 8. The report, read-only.
        await Step(Document, 8, "press the record number and read the report read-only, with no Edit or Delete", async () =>
        {
            await Page.Locator($"#queue-row-{reportId}-record a").ClickAsync();

            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Listeriosis");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Salt Lake County");
            await Expect(Page.Locator("#report-shared-read-only")).ToHaveTextAsync(
                "This case is shared with your agency read-only: you may read it, and change nothing on it.");

            await Expect(Page.Locator("#report-print")).ToBeVisibleAsync();
            await Expect(Page.Locator("#report-edit")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#report-delete")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#report-restore")).ToHaveCountAsync(0);
        });
    }

    /// <summary>The checkbox labelled <paramref name="label"/>, by the words on it.</summary>
    private ILocator Box(string label) =>
        Page.GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });

    /// <summary>
    /// Opens the list whose box is labelled <paramref name="label"/>, chooses
    /// the item reading <paramref name="item"/>, and reads that box back.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
