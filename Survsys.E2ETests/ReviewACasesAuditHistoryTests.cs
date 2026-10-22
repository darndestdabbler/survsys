using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/review-a-cases-audit-history.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry. It
/// creates a case of its own, so its history holds only what this test saved:
/// the insert, and the update that set the event name. A save may write the
/// case more than once, so the update is found by its values — the event name
/// empty before and set after — rather than by its position.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ReviewACasesAuditHistoryTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-a-cases-audit-history.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Lyme disease";
    private const string Agency = "Davis County";
    private const string EventName = "Church picnic";

    private static readonly Regex Second = new(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$");

    [Fact]
    public async Task ReviewACasesAuditHistory()
    {
        var caseId = string.Empty;

        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Search the person by last name.
        await Step(Document, 2, "type Okafor in Last name and press Search", async () =>
        {
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
            await Page.Locator("#person-search-last-name").FillAsync(Person);
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. Create the report on the one row.
        await Step(Document, 3, "press Create CMR on the Okafor, Chidi row", async () =>
        {
            await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync([PersonName]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 4. The report's disease and agency, then Save & Continue.
        await Step(Document, 4, "choose the disease and agency, and save", async () =>
        {
            await Choose("Disease", Condition);
            await Choose("Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 5. The report's form.
        await Step(Document, 5, "read the report's header bar", async () =>
        {
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
        });

        // 6. The event name on the Administrative tab, saved.
        await Step(Document, 6, "press Edit, then Administrative, type the event name and save", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await Page.Locator("#report-tab-administrative").ClickAsync();
            await Page.Locator("#administrative-event-name").FillAsync(EventName);
            await Page.Locator("#administrative-save").ClickAsync();

            await Expect(Page.Locator("#administrative-saved"))
                .ToHaveTextAsync("The administrative data has been saved.");
        });

        // 7. Done, then the History of changes link.
        await Step(Document, 7, "press Done, then History of changes", async () =>
        {
            await Page.Locator("#report-done").ClickAsync();
            await Expect(Page.Locator("#report-edit")).ToBeVisibleAsync();
            await Page.Locator("#report-history").ClickAsync();
        });

        // 8. The Audit screen at the case's own row.
        await Step(Document, 8, "read the Audit screen's table, key and count", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Audit — Survsys");
            await Expect(Page.Locator("#audit-heading")).ToHaveTextAsync("Audit");
            await Expect(Page.Locator("#audit-table")).ToHaveValueAsync("person_conditions");
            await Expect(Page.Locator("#audit-key")).ToHaveValueAsync(new Regex(@"^\d+$"));
            await Expect(Page.Locator("#audit-total")).ToHaveTextAsync(new Regex(@"^\s*(\d+) change\(s\), showing \1\.\s*$"));

            caseId = await Page.Locator("#audit-key").InputValueAsync();
        });

        // 9. The update that set the event name, with who made it and when.
        await Step(Document, 9, "find the Update row with the event name before and after", async () =>
        {
            var row = Rows("Update")
                .Filter(new() { Has = Page.Locator("[data-audit-old]", new() { HasTextString = "\"event_name\":null" }) })
                .Filter(new() { Has = Page.Locator("[data-audit-new]", new() { HasTextString = $"\"event_name\":\"{EventName}\"" }) });

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-audit-user]")).ToHaveTextAsync(SurvsysHarness.AdministratorUserName);
            await Expect(row.Locator("[data-audit-key]")).ToHaveTextAsync(caseId);
            await Expect(row.Locator("[data-audit-when]")).ToHaveTextAsync(Second);
        });

        // 10. The insert that created the case.
        await Step(Document, 10, "find the Insert row, with nothing before", async () =>
        {
            var row = Rows("Insert");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-audit-old]")).ToHaveTextAsync("—");
            await Expect(row.Locator("[data-audit-user]")).ToHaveTextAsync(SurvsysHarness.AdministratorUserName);
        });
    }

    /// <summary>The audit's rows whose Operation reads <paramref name="operation"/>.</summary>
    private ILocator Rows(string operation) =>
        Page.Locator("#audit-results [data-audit]")
            .Filter(new() { Has = Page.Locator("[data-audit-operation]", new() { HasTextRegex = new Regex($"^{operation}$") }) });

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
