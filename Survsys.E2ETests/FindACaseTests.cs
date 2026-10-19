using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/find-a-case.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// never by a MudBlazor class or a cell's position. It opens the case it finds
/// through the New CMR screens, on <strong>Rodriguez, Carlos</strong> for
/// <strong>Giardiasis</strong>, a pair no other walkthrough reports on, and
/// finds its Events row by the report's own id, which the screen keys each row
/// by. The count line is read for its shape, as the document says it is, so
/// the row is what proves the case was found.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class FindACaseTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/find-a-case.md";

    private const string PersonName = "Rodriguez, Carlos";
    private const string Condition = "Giardiasis";
    private const string OtherCondition = "Malaria";
    private const string Agency = "Tooele County";
    private const string FirstReported = "2026-10-05";

    /// <summary>The report's address, whose last segment is the id its Events row is keyed by.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    [Fact]
    public async Task FindACase()
    {
        var reportId = string.Empty;
        var recordNumber = string.Empty;

        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
        });

        // 2. Search the person by last name.
        await Step(Document, 2, "type Rodriguez in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. Two people found; Create CMR on Carlos's row.
        await Step(Document, 3, "read the count line and press Create CMR on Rodriguez, Carlos", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("2 person(s) found, showing 2.");

            var row = Page.Locator("#person-search-results tr[data-person]")
                .Filter(new() { HasText = PersonName });

            await Expect(row).ToHaveCountAsync(1);
            await row.Locator("[data-new-report-for]").ClickAsync();
        });

        // 4. The Step 2 of 2 line names the person.
        await Step(Document, 4, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync($"Step 2 of 2. Reporting on {PersonName}"));

        // 5. The date first reported to public health.
        await Step(Document, 5, "enter the First reported to public health date", async () =>
            await Page.Locator("#report-first-reported").FillAsync(FirstReported));

        // 6. The disease.
        await Step(Document, 6, "choose Giardiasis from Disease", async () =>
            await Choose("Disease", Condition));

        // 7. The agency, then Save & Continue.
        await Step(Document, 7, "choose Tooele County from Investigating agency and save", async () =>
        {
            await Choose("Investigating agency", Agency);
            await Page.Locator("#report-save").ClickAsync();
        });

        // 8. The report's form, and the record number the case is found by.
        await Step(Document, 8, "read the report's header bar and note the record number", async () =>
        {
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await ExpectHeader(new Regex($"^{DateTime.Now.Year}\\d{{5}}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();

            var address = ReportAddress.Match(Page.Url);
            Assert.True(address.Success, $"The address {Page.Url} does not end in /event/edit/ and a number.");
            reportId = address.Groups[1].Value;
        });

        // 9. The Events screen, with nothing searched yet.
        await Step(Document, 9, "press Events and read the browser tab and heading", async () =>
        {
            await Page.Locator("#nav-events").ClickAsync();
            await Expect(Page).ToHaveTitleAsync("Events — Survsys");
            await Expect(Page.Locator("#events-heading")).ToHaveTextAsync("Events");
            await Expect(Page.Locator("#events-results")).ToHaveCountAsync(0);
        });

        // 10. The patient's name.
        await Step(Document, 10, "type Rodriguez in Last name and Carlos in First name", async () =>
        {
            await Page.Locator("#events-last-name").FillAsync("Rodriguez");
            await Page.Locator("#events-first-name").FillAsync("Carlos");
        });

        // 11. Malaria, which finds nothing.
        await Step(Document, 11, "choose Malaria from Disease, press Search and read No cases found.", async () =>
        {
            await Choose("Disease", OtherCondition);
            await Page.Locator("#events-search").ClickAsync();
            await Expect(Page.Locator("#events-none")).ToHaveTextAsync("No cases found.");
            await Expect(Page.Locator($"#events-row-{reportId}")).ToHaveCountAsync(0);
        });

        // 12. Giardiasis instead, and Search again.
        await Step(Document, 12, "choose Giardiasis from Disease and press Search", async () =>
        {
            await Choose("Disease", Condition);
            await Page.Locator("#events-search").ClickAsync();
        });

        // 13. The count line, and the case's own row.
        await Step(Document, 13, "read the count line and the case's row", async () =>
        {
            await Expect(Page.Locator("#events-total"))
                .ToHaveTextAsync(new Regex(@"^\s*(\d+) case\(s\) found, showing \1\.\s*$"));

            var row = $"#events-row-{reportId}";

            await Expect(Page.Locator(row)).ToHaveCountAsync(1);
            await Expect(Page.Locator($"{row}-record")).ToHaveTextAsync(recordNumber);
            await Expect(Page.Locator($"{row}-patient")).ToHaveTextAsync(PersonName);
            await Expect(Page.Locator($"{row}-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator($"{row}-agency")).ToHaveTextAsync(Agency);
            await Expect(Page.Locator($"{row}-workflow")).ToHaveTextAsync("Assigned to LHD");
            await Expect(Page.Locator($"{row}-status")).ToHaveTextAsync("Unknown");
            await Expect(Page.Locator($"{row}-mmwr")).ToHaveTextAsync(new Regex(@"^\d{1,2}/\d{4}$"));
            await Expect(Page.Locator($"{row}-onset")).ToHaveTextAsync(DateTime.Now.ToString("yyyy-MM-dd"));
        });

        // 14. The record number opens the case.
        await Step(Document, 14, "press the record number and read the report's header bar", async () =>
        {
            await Page.Locator($"#events-row-{reportId}-record a").ClickAsync();
            await Expect(Page).ToHaveURLAsync(ReportAddress);
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await ExpectHeader(new Regex($"^{Regex.Escape(recordNumber)}$"));
        });
    }

    /// <summary>
    /// The report's header bar, reading the case this walkthrough opened.
    /// </summary>
    private async Task ExpectHeader(Regex recordNumber)
    {
        await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(PersonName);
        await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
        await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(Agency);
        await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(recordNumber);
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the other walkthrough
    /// tests in this suite do it. The label is matched exactly, so the box
    /// pressed and read back is the one named.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
