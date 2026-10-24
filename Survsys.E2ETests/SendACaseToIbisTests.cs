using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/send-a-case-to-ibis.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry. The
/// harness points the API tier's IBIS endpoint at a stand-in on the loopback
/// that accepts every case, which is the IBIS the document's reader is told to
/// point theirs at, so the send is real and nothing leaves the host.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class SendACaseToIbisTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/send-a-case-to-ibis.md";

    private const string Person = "Okafor";
    private const string PersonName = "Okafor, Chidi";
    private const string Condition = "Malaria";
    private const string Agency = "Davis County";

    [Fact]
    public async Task SendACaseToIbis()
    {
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

        // 6. Edit, then Send to IBIS.
        await Step(Document, 6, "press Edit, then Send to IBIS in the IBIS panel", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await Expect(Page.Locator("#report-ibis")).ToContainTextAsync("IBIS");
            await Page.Locator("#report-ibis-send").ClickAsync();
        });

        // 7. The date IBIS accepted it.
        await Step(Document, 7, "read the date IBIS accepted the case", async () =>
        {
            await Expect(Page.Locator("#report-ibis-sent"))
                .ToHaveTextAsync($"IBIS accepted this case on {DateTime.Now:yyyy-MM-dd}.");
            await Expect(Page.Locator("#report-ibis-error")).ToHaveCountAsync(0);
        });
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
