using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/review-a-staged-message.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the staged-message
/// screens carry — <c>staged-messages-*</c> on the queue and
/// <c>staged-message-review-*</c> on the detail — and never by a MudBlazor
/// class or a cell's position. It reads and changes nothing, so it leaves the
/// queue as it found it and is independent of the order the suite runs its
/// tests in.
///
/// It finds its message by its source, <c>elr-exception-example.hl7</c>,
/// which is the one stable thing a staged row carries: identity assigns the
/// ids, so the source is the handle, as <see cref="SurvsysHarness.SeededStagedMessageSources"/>
/// says. The exception message is the one it opens, because only the seeded
/// exception carries a review state, a QA value, an exception detail and a raw
/// message all at once, and no other test puts a message into the exception
/// state, so the Exception filter finds exactly it.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class ReviewAStagedMessageTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-a-staged-message.md";

    /// <summary>The message this walkthrough opens, by its source.</summary>
    private const string ExceptionSource = "elr-exception-example.hl7";

    /// <summary>The message whose QA flag the queue shows, by its source.</summary>
    private const string QaSource = "elr-qa-example.hl7";

    /// <summary>The parser's account of why the exception message was held.</summary>
    private const string ExceptionDetail = "The message could not be parsed as HL7 v2.";

    [Fact]
    public async Task ReviewAStagedMessage()
    {
        // 1. Open the application tier's address and press Staged messages.
        await Step(Document, 1, "open the application tier's address and press Staged messages", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-staged-messages").ClickAsync();
        });

        // 2. The queue: its tab, heading, lead-in, the filter on All and the
        //    table's columns.
        await Step(Document, 2, "read the Staged messages heading, the filter on All and the table's columns", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Staged messages — Survsys");
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Review state", Exact = true }))
                .ToHaveTextAsync("All");
            await Expect(Page.Locator("#staged-messages-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#staged-messages-table thead th")).ToHaveTextAsync(
                ["Source", "Received", "Review state", "QA", "Exception"]);
        });

        // 3. The exception row and the QA-flagged row, read in the whole
        //    queue before it is filtered.
        await Step(Document, 3, "read the exception row and the QA-flagged row in the queue", async () =>
        {
            var exception = Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = ExceptionSource });
            await Expect(exception.Locator("[data-staged-message-state]")).ToHaveTextAsync("Exception");
            await Expect(exception.Locator("[data-staged-message-qa]")).ToHaveTextAsync("—");
            await Expect(exception.Locator("[data-staged-message-exception]")).ToHaveTextAsync(ExceptionDetail);

            var flagged = Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = QaSource });
            await Expect(flagged.Locator("[data-staged-message-qa]")).ToHaveTextAsync("Flagged");
        });

        // 4. Filter to Exception: the one row held as an exception, and its id
        //    noted for the heading the detail screen shows.
        var reviewId = string.Empty;

        await Step(Document, 4, "choose Exception from Review state and read the one exception row", async () =>
        {
            await Choose("#staged-messages-filter", "Review state", "Exception");

            var rows = Page.Locator("#staged-messages-table tbody tr");
            await Expect(rows).ToHaveCountAsync(1);
            await Expect(rows.Locator("[data-staged-message-source]")).ToHaveTextAsync(ExceptionSource);

            reviewId = (await rows.First.GetAttributeAsync("data-staged-message"))!;
            Assert.False(
                string.IsNullOrWhiteSpace(reviewId),
                $"The one exception row carries no data-staged-message id. The document has its "
                + "reader open the message from this row and read its id in the heading, so a "
                + "row that carries no id is a defect in the screen or in the document.");
        });

        // 5. Open the message from its Source link.
        await Step(Document, 5, "press the exception message's Source link", async () =>
            await Page.Locator("#staged-messages-table tbody tr [data-staged-message-source] a").ClickAsync());

        // 6. The review screen: its tab, the heading naming the id, and the
        //    Back to the queue button.
        await Step(Document, 6, "read the review screen's tab, heading with the id, and Back button", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Staged message — Survsys");
            await Expect(Page).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex($"/staged-messages/{reviewId}$"));
            await Expect(Page.Locator("#staged-message-review-heading")).ToHaveTextAsync($"Staged message {reviewId}");
            await Expect(Page.Locator("#staged-message-review-back")).ToHaveTextAsync("Back to the queue");
        });

        // 7. The message's summary, read field by field.
        await Step(Document, 7, "read the message's source, received time, review state, QA and exception", async () =>
        {
            await Expect(Page.Locator("#staged-message-review-source")).ToHaveTextAsync(ExceptionSource);
            await Expect(Page.Locator("#staged-message-review-received")).ToHaveTextAsync("2026-10-15 07:45");
            await Expect(Page.Locator("#staged-message-review-state")).ToHaveTextAsync("Exception");
            await Expect(Page.Locator("#staged-message-review-qa")).ToHaveTextAsync("Not flagged");
            await Expect(Page.Locator("#staged-message-review-exception")).ToHaveTextAsync(ExceptionDetail);
        });

        // 8. The raw message, as it arrived.
        await Step(Document, 8, "read the raw message", async () =>
            await Expect(Page.Locator("#staged-message-review-raw"))
                .ToContainTextAsync("GARBLED|this is not|an HL7 v2 message|no MSH segment here"));

        // 9. Back to the queue, filtered to Queued for review: the QA-flagged
        //    message's row read there.
        await Step(Document, 9, "go back, choose Queued for review and read the QA-flagged row", async () =>
        {
            await Page.Locator("#staged-message-review-back").ClickAsync();

            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");
            await Choose("#staged-messages-filter", "Review state", "Queued for review");

            var flagged = Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = QaSource });
            await Expect(flagged).ToHaveCountAsync(1);
            await Expect(flagged.Locator("[data-staged-message-state]")).ToHaveTextAsync("Queued for review");
            await Expect(flagged.Locator("[data-staged-message-qa]")).ToHaveTextAsync("Flagged");
        });
    }

    /// <summary>
    /// Opens the list whose box carries <paramref name="list"/>, chooses the
    /// item reading <paramref name="item"/> by the words on it, and reads the
    /// box labelled <paramref name="label"/>, which now shows what was chosen
    /// — the same way the other walkthroughs' tests open a MudBlazor list.
    /// </summary>
    private async Task Choose(string list, string label, string item)
    {
        await Page.Locator(list).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true }))
            .ToHaveTextAsync(item);
    }
}
