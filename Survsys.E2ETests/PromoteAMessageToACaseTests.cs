using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/promote-a-message-to-a-case.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// <c>staged-messages-*</c> on the queue, <c>person-search-*</c> on the
/// person search, <c>staged-message-promote-*</c> on the promote form,
/// <c>staged-message-promoted-*</c> on its result, and <c>report-header-*</c>
/// on the case it opens — never by a MudBlazor class or a cell's position.
///
/// It promotes the seeded message <c>elr-promote-example.hl7</c>, which no
/// other test touches, so it is queued when this test opens it however the
/// suite orders its tests. The record number the promotion assigns is read
/// from the result and carried to the report's header, as the assignment
/// walkthrough's test carries the one it notes.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class PromoteAMessageToACaseTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/promote-a-message-to-a-case.md";

    /// <summary>The queued message this walkthrough promotes, by its source.</summary>
    private const string PromoteSource = "elr-promote-example.hl7";

    /// <summary>The record number the promotion assigns, read from its result's link.</summary>
    private static readonly Regex OpenCase = new(@"Open case (\d+)");

    [Fact]
    public async Task PromoteAMessageToACase()
    {
        // 1. Open the application tier's address and press Staged messages.
        await Step(Document, 1, "open the application tier's address and press Staged messages", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-staged-messages").ClickAsync();
        });

        // 2. Filter to Queued for review and find the promote message's row.
        await Step(Document, 2, "choose Queued for review and find the elr-promote-example.hl7 row", async () =>
        {
            await Choose("Review state", "Queued for review");

            await Expect(Page.Locator("#staged-messages-table tbody tr").Filter(new() { HasText = PromoteSource }))
                .ToHaveCountAsync(1);
        });

        // 3. Open the message from its Source link.
        await Step(Document, 3, "press the elr-promote-example.hl7 Source link", async () =>
            await Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = PromoteSource })
                .Locator("[data-staged-message-source] a")
                .ClickAsync());

        // 4. The review screen, with its Promote to a case form below the
        //    message.
        await Step(Document, 4, "read the review heading and the Promote to a case form", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Staged message — Survsys");
            await Expect(Page.Locator("#staged-message-review-heading"))
                .ToHaveTextAsync(new Regex(@"^\s*Staged message \d+\s*$"));
            await Expect(Page.Locator("#staged-message-promote-heading")).ToHaveTextAsync("Promote to a case");
        });

        // 5. Okafor in Last name, Search, and the one row noted.
        var personId = string.Empty;

        await Step(Document, 5, "search Okafor and note the Okafor, Chidi row's ID", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Okafor");
            await Page.Locator("#person-search-search").ClickAsync();

            var okafor = Page.Locator("#person-search-results tbody tr")
                .Filter(new() { HasText = "Okafor, Chidi" });
            await Expect(okafor).ToHaveCountAsync(1);

            personId = (await okafor.Locator("[data-person-id]").InnerTextAsync()).Trim();
            Assert.True(
                int.TryParse(personId, out var identifier) && identifier > 0,
                $"The ID column of the Okafor, Chidi row reads \"{personId}\", which is not an "
                + "identifier. The document has its reader note that number and read it back on "
                + "the promote form, so a column that does not hold one is a defect in the "
                + "screen or in the document.");
        });

        // 6. Use this person: the promote form, naming the person.
        await Step(Document, 6, "press Use this person and read the Reporting on line", async () =>
        {
            await Page.Locator("#person-search-results [data-promote-person]").ClickAsync();

            await Expect(Page.Locator("#staged-message-promote-person"))
                .ToContainTextAsync($"Reporting on Okafor, Chidi (person {personId}");
        });

        // 7. The first-reported date.
        await Step(Document, 7, "enter the first-reported date", async () =>
            await Page.Locator("#staged-message-promote-first-reported").FillAsync("2026-03-12"));

        // 8. Listeriosis from the Disease list.
        await Step(Document, 8, "choose Listeriosis from Disease", async () =>
            await Choose("Disease", "Listeriosis"));

        // 9. Utah State from the Investigating agency list.
        await Step(Document, 9, "choose Utah State from Investigating agency", async () =>
            await Choose("Investigating agency", "Utah State"));

        // 10. Promote to a case.
        await Step(Document, 10, "press Promote to a case", async () =>
            await Page.Locator("#staged-message-promote-submit").ClickAsync());

        // 11. The result: the notification naming the record number, and the
        //     link to the new case. The record number is read here and carried
        //     to the report's header.
        var recordNumber = string.Empty;

        await Step(Document, 11, "read the notification and the Open case link, and note the record number", async () =>
        {
            await Expect(Page.Locator("#staged-message-promoted")).ToBeVisibleAsync();
            await Expect(Page.Locator("#staged-message-promote-error")).ToHaveCountAsync(0);

            var link = (await Page.Locator("#staged-message-promoted-link").InnerTextAsync()).Trim();
            var match = OpenCase.Match(link);
            Assert.True(
                match.Success,
                $"The Open case link reads \"{link}\", which does not name a record number. The "
                + "document has its reader note the number from this link and find the case by "
                + "it, so a link that does not carry one is a defect in the screen or in the "
                + "document.");
            recordNumber = match.Groups[1].Value;

            await Expect(Page.Locator("#staged-message-promoted-notification")).ToHaveTextAsync(
                $"Case {recordNumber} was created for Listeriosis; a notification has been "
                + "raised to public health.");
        });

        // 12. Open the case: the report's header, reading what was confirmed.
        await Step(Document, 12, "press Open case and read the report header", async () =>
        {
            await Page.Locator("#staged-message-promoted-link").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex(@"/event/edit/\d+$"));
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Okafor, Chidi");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Listeriosis");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Utah State");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(recordNumber);
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — even an empty one, as <strong>Review
    /// state</strong> opens on <strong>All</strong> — renders the input
    /// carrying that identifier as <c>type="hidden"</c> and shows a sibling box
    /// beside it. The hidden input cannot be pressed, and it is not what the
    /// reader presses: the box carrying the label is.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
