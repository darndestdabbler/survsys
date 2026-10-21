using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/release-a-graylisted-message.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed holds Owen Brennan
/// with no case and a Lyme disease rule that opens none. It drops its message
/// as the document's reader does, opens the case through the New CMR screens,
/// and waits on the assignment pass — which graylists and releases — the way
/// the document tells its reader to: choosing All and then the state again
/// until the row is there, within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class ReleaseAGraylistedMessageTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/release-a-graylisted-message.md";

    /// <summary>The message this walkthrough drops, by its file name and so its source.</summary>
    private const string Source = "elr-graylist-example.hl7";

    private const string Patient = "Brennan, Owen";

    private const string Condition = "Lyme disease";

    /// <summary>
    /// How long the intake and the assignment pass are given: many times their
    /// interval, and bounded, because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ReleaseAGraylistedMessage()
    {
        // 1. Copy the message into the drop directory, and see the intake take
        //    it out again.
        await Step(Document, 1, "copy elr-graylist-example.hl7 into the drop directory and see it taken", async () =>
        {
            var dropped = Path.Combine(harness.IntakeDirectory, Source);
            File.Copy(IntakeHarness.Message(Source), dropped, overwrite: true);

            var waited = Stopwatch.StartNew();
            while (File.Exists(dropped))
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"{Source} was still in the drop directory after {Patience.TotalSeconds:0} seconds: "
                    + "the intake never read it.");
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.True(
                File.Exists(Path.Combine(harness.IntakeDirectory, ".processed", Source)),
                $"{Source} left the drop directory but is not in its .processed folder, where the "
                + "document says the intake moves a file it has read.");
        });

        // 2. The Staged messages queue, filtered to Graylisted.
        await Step(Document, 2, "open the application tier's address, press Staged messages and choose Graylisted", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-staged-messages").ClickAsync();
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");
            await Choose("Review state", "Graylisted");
        });

        // 3. The message graylisted, with the reason it waits.
        await Step(Document, 3, "read the graylisted message's row", async () =>
        {
            var row = await WaitForRow("Graylisted");

            await Expect(row.Locator("[data-staged-message-state]")).ToHaveTextAsync("Graylisted");
            await Expect(row.Locator("[data-staged-message-exception]")).ToHaveTextAsync(
                "Graylisted: the condition's case-management rule opens no case for this result, "
                + "so it waits for a case to attach to.");
        });

        // 4. New CMR, searched by the patient's last name.
        await Step(Document, 4, "press New CMR, type Brennan in Last name and press Search", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Brennan");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 5. The one person, and Create CMR on that row.
        await Step(Document, 5, "read the Brennan, Owen row, press Create CMR and read the Step 2 of 2 line", async () =>
        {
            await Expect(Page.Locator("#person-search-total")).ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]")).ToHaveTextAsync([Patient]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync($"Step 2 of 2. Reporting on {Patient}");
        });

        // 6. The disease and the agency, then Save & Continue.
        await Step(Document, 6, "choose Lyme disease and Salt Lake County and press Save & Continue", async () =>
        {
            await Choose("Disease", Condition);
            await Choose("Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();
        });

        // 7. The case's header, and the record number the release names.
        var recordNumber = string.Empty;

        await Step(Document, 7, "read the report's header bar and note the record number", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(
                new System.Text.RegularExpressions.Regex(@"^\d{9}$"));

            recordNumber = await Page.Locator("#report-header-record-number").InnerTextAsync();
        });

        // 8. The message released, naming the case.
        var released = string.Empty;

        await Step(Document, 8, "press Staged messages, choose Held for review and read the released message's row", async () =>
        {
            released = $"Released from the graylist: case {recordNumber} has appeared for this patient.";

            await Page.Locator("#nav-staged-messages").ClickAsync();
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");
            await Choose("Review state", "Held for review");

            var row = await WaitForRow("Held for review");

            await Expect(row.Locator("[data-staged-message-state]")).ToHaveTextAsync("Held for review");
            await Expect(row.Locator("[data-staged-message-exception]")).ToHaveTextAsync(released);
        });

        // 9. The message's own screen, and the person it is proposed for.
        await Step(Document, 9, "press the message's Source link and read its state, exception and proposed match", async () =>
        {
            await Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = Source })
                .Locator("[data-staged-message-source] a")
                .ClickAsync();

            await Expect(Page).ToHaveTitleAsync("Staged message — Survsys");
            await Expect(Page.Locator("#staged-message-review-state")).ToHaveTextAsync("Held for review");
            await Expect(Page.Locator("#staged-message-review-exception")).ToHaveTextAsync(released);
            await Expect(Page.Locator("#staged-message-match-person")).ToContainTextAsync($"Proposed match: {Patient}");
        });
    }

    /// <summary>
    /// The message's row in the queue filtered to <paramref name="state"/>,
    /// choosing All and then that state again — which reads the queue afresh
    /// — until the pass has put it there, within <see cref="Patience"/>.
    /// </summary>
    private async Task<ILocator> WaitForRow(string state)
    {
        var row = Page.Locator("#staged-messages-table tbody tr").Filter(new() { HasText = Source });
        var waited = Stopwatch.StartNew();

        while (await row.CountAsync() == 0)
        {
            Assert.True(
                waited.Elapsed < Patience,
                $"{Source} did not reach {state} within {Patience.TotalSeconds:0} seconds.");

            await Page.WaitForTimeoutAsync(1000);
            await Choose("Review state", "All");
            await Choose("Review state", state);
        }

        return row;
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
