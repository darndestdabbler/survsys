using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/find-a-message-assigned-automatically.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, against the tiers whose intake
/// and assignment pass are on, and drops its message as the document's reader
/// does: by copying the file into the drop directory. The pass runs every
/// <see cref="IntakeHarness.PollIntervalSeconds"/> seconds, so step 3 does
/// what the document tells its reader to do when the row is not there yet —
/// choose All and then the state again, which reads the queue afresh — until
/// it is, within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class FindAMessageAssignedAutomaticallyTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/find-a-message-assigned-automatically.md";

    /// <summary>The message this walkthrough drops, by its file name and so its source.</summary>
    private const string Source = "elr-assign-example.hl7";

    private const string Patient = "CASTILLO, ROSA";

    private const string Condition = "Hepatitis A";

    private const string Agency = "Salt Lake County";

    /// <summary>
    /// How long the intake and the assignment pass are given: many times their
    /// interval, and bounded, because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    [Fact]
    public async Task FindAMessageAssignedAutomatically()
    {
        // 1. Copy the message into the drop directory, and see the intake take
        //    it out again.
        await Step(Document, 1, "copy elr-assign-example.hl7 into the drop directory and see it taken", async () =>
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
        });

        // 2. The Staged messages queue.
        await Step(Document, 2, "open the application tier's address and press Staged messages", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-staged-messages").ClickAsync();
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");
        });

        // 3. Filtered to assigned: the message's row, pressing Staged messages
        //    and choosing again until the pass has run.
        await Step(Document, 3, "choose assigned from Review state and read the message's row", async () =>
        {
            var row = Page.Locator("#staged-messages-table tbody tr").Filter(new() { HasText = Source });
            var waited = Stopwatch.StartNew();

            await Choose("Review state", "assigned");

            while (await row.CountAsync() == 0)
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"{Source} was not assigned within {Patience.TotalSeconds:0} seconds of being queued.");

                await Page.WaitForTimeoutAsync(1000);
                await Choose("Review state", "All");
                await Choose("Review state", "assigned");
            }

            await Expect(row.Locator("[data-staged-message-state]")).ToHaveTextAsync("assigned");
            await Expect(row.Locator("[data-staged-message-exception]")).ToHaveTextAsync("—");
        });

        // 4. Open the message from its Source link.
        await Step(Document, 4, "press the message's Source link", async () =>
            await Page.Locator("#staged-messages-table tbody tr")
                .Filter(new() { HasText = Source })
                .Locator("[data-staged-message-source] a")
                .ClickAsync());

        // 5. The message's own screen: assigned, the raw patient line, and no
        //    promotion offered.
        await Step(Document, 5, "read the message's state, raw patient line and that it is already a case", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Staged message — Survsys");
            await Expect(Page.Locator("#staged-message-review-source")).ToHaveTextAsync(Source);
            await Expect(Page.Locator("#staged-message-review-state")).ToHaveTextAsync("assigned");
            await Expect(Page.Locator("#staged-message-review-exception")).ToHaveTextAsync("—");
            await Expect(Page.Locator("#staged-message-review-raw"))
                .ToContainTextAsync("PID|1||ASSIGNPAT001^^^ARUP_LAB^MR||CASTILLO^ROSA||19941205|F");
            await Expect(Page.Locator("#staged-message-promote-done"))
                .ToHaveTextAsync("This message has already been promoted to a case.");
        });

        // 6. The Events screen, searched by the patient and the disease.
        await Step(Document, 6, "press Events, type Castillo, choose Hepatitis A and search", async () =>
        {
            await Page.Locator("#nav-events").ClickAsync();
            await Expect(Page.Locator("#events-heading")).ToHaveTextAsync("Events");
            await Page.Locator("#events-last-name").FillAsync("Castillo");
            await Choose("Disease", Condition);
            await Page.Locator("#events-search").ClickAsync();
        });

        // 7. The one case the message opened.
        await Step(Document, 7, "read the count line and the case's row", async () =>
        {
            await Expect(Page.Locator("#events-total")).ToHaveTextAsync("1 case(s) found, showing 1.");

            var row = Page.Locator("#events-results tbody tr");
            await Expect(row).ToHaveCountAsync(1);

            var id = (await row.GetAttributeAsync("id"))!;
            await Expect(Page.Locator($"#{id}-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator($"#{id}-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator($"#{id}-agency")).ToHaveTextAsync(Agency);
            await Expect(Page.Locator($"#{id}-workflow")).ToHaveTextAsync("Assigned to LHD");
            await Expect(Page.Locator($"#{id}-onset")).ToHaveTextAsync(DateTime.Now.ToString("yyyy-MM-dd"));
            await Expect(Page.Locator($"#{id}-record")).ToHaveTextAsync(new Regex($@"^\s*{DateTime.Now.Year}\d{{5}}\s*$"));
        });

        // 8. The case, opened from its record number.
        await Step(Document, 8, "press the record number and read the report's header bar", async () =>
        {
            await Page.Locator("#events-results tbody tr [id$='-record'] a").ClickAsync();
            await Expect(Page).ToHaveURLAsync(ReportAddress);
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync(Agency);
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
