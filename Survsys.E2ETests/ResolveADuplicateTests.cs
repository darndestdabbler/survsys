using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/resolve-a-duplicate.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry —
/// <c>staged-messages-*</c> on the queue, <c>person-search-*</c> and
/// <c>staged-message-promote-*</c> on the promote form, and
/// <c>staged-message-promoted-*</c> on its result — never by a MudBlazor
/// class or a cell's position.
///
/// It promotes the seeded message <c>elr-duplicate-example.hl7</c>, which no
/// other test touches, then reopens it. The guard it proves is the shipped
/// one: a message already promoted carries the terminal review state, the
/// review screen shows the already-promoted notice, and no promote form or
/// button is offered — so no second case can be opened from the message. The
/// 409 the application tier would answer a second promotion with is the guard
/// behind that absent button; the screen never asks for one.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ResolveADuplicateTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/resolve-a-duplicate.md";

    /// <summary>The message this walkthrough promotes and then revisits, by its source.</summary>
    private const string DuplicateSource = "elr-duplicate-example.hl7";

    /// <summary>The record number the promotion assigns, read from its result's link.</summary>
    private static readonly Regex OpenCase = new(@"Open case (\d+)");

    [Fact]
    public async Task ResolveADuplicate()
    {
        // 1. Open the application tier's address and press Staged messages.
        await Step(Document, 1, "open the application tier's address and press Staged messages", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-staged-messages").ClickAsync();
        });

        // 2. Filter to Queued for review and open the duplicate message.
        await Step(Document, 2, "choose Queued for review and open the elr-duplicate-example.hl7 message", async () =>
        {
            await Choose("Review state", "Queued for review");

            var row = Page.Locator("#staged-messages-table tbody tr").Filter(new() { HasText = DuplicateSource });
            await Expect(row).ToHaveCountAsync(1);
            await row.Locator("[data-staged-message-source] a").ClickAsync();
        });

        // 3. The promote form: find Zimmerman and use that person.
        await Step(Document, 3, "search Zimmerman and press Use this person", async () =>
        {
            await Expect(Page.Locator("#staged-message-promote-heading")).ToHaveTextAsync("Promote to a case");

            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-search").ClickAsync();

            var zimmerman = Page.Locator("#person-search-results tbody tr")
                .Filter(new() { HasText = "Zimmerman, Paul" });
            await Expect(zimmerman).ToHaveCountAsync(1);
            await zimmerman.Locator("[data-promote-person]").ClickAsync();

            await Expect(Page.Locator("#staged-message-promote-form")).ToBeVisibleAsync();
        });

        // 4. Hepatitis A, Utah State, Promote to a case.
        await Step(Document, 4, "choose Hepatitis A and Utah State and press Promote to a case", async () =>
        {
            await Choose("Disease", "Hepatitis A");
            await Choose("Investigating agency", "Utah State");
            await Page.Locator("#staged-message-promote-submit").ClickAsync();
        });

        // 5. The result: the notification and the one record number.
        var recordNumber = string.Empty;

        await Step(Document, 5, "read the notification and note the one record number", async () =>
        {
            await Expect(Page.Locator("#staged-message-promoted")).ToBeVisibleAsync();
            await Expect(Page.Locator("#staged-message-promote-error")).ToHaveCountAsync(0);

            var link = (await Page.Locator("#staged-message-promoted-link").InnerTextAsync()).Trim();
            var match = OpenCase.Match(link);
            Assert.True(
                match.Success,
                $"The Open case link reads \"{link}\", which does not name a record number. The "
                + "document has its reader note the one case's record number from this link, so "
                + "a link that does not carry one is a defect in the screen or in the document.");
            recordNumber = match.Groups[1].Value;

            await Expect(Page.Locator("#staged-message-promoted-notification")).ToHaveTextAsync(
                $"Case {recordNumber} was created for Hepatitis A; a notification has been "
                + "raised to public health.");
        });

        // 6. Back to the queue: the one duplicate row, now assigned.
        await Step(Document, 6, "return to the queue and read the one assigned duplicate row", async () =>
        {
            await Page.Locator("#nav-staged-messages").ClickAsync();
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");

            var row = Page.Locator("#staged-messages-table tbody tr").Filter(new() { HasText = DuplicateSource });
            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("[data-staged-message-state]")).ToHaveTextAsync("assigned");
        });

        // 7. Reopen the message.
        await Step(Document, 7, "open the elr-duplicate-example.hl7 message again", async () =>
            await Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = DuplicateSource })
                .Locator("[data-staged-message-source] a")
                .ClickAsync());

        // 8. Already a case: the notice, and no promote form or button to open
        //    a second one.
        await Step(Document, 8, "read the already-promoted notice and that no promote control is offered", async () =>
        {
            await Expect(Page.Locator("#staged-message-promote-done"))
                .ToHaveTextAsync("This message has already been promoted to a case.");
            await Expect(Page.Locator("#staged-message-promote-heading")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#staged-message-promote-form")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#staged-message-promote-submit")).ToHaveCountAsync(0);
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
