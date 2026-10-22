using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/update-an-existing-case-from-a-laboratory-message.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed holds Leila Haddad's
/// open Giardiasis case, <c>202600901</c>, with no laboratory result. It drops
/// its message as the document's reader does, by copying the file into the
/// drop directory, and step 6 waits on the assignment pass the way the
/// document tells its reader to — choosing All and then the state again until
/// the row is there — within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class UpdateAnExistingCaseFromALaboratoryMessageTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/update-an-existing-case-from-a-laboratory-message.md";

    /// <summary>The message this walkthrough drops, by its file name and so its source.</summary>
    private const string Source = "elr-update-example.hl7";

    /// <summary>The case <c>Fixtures/elr-intake.sql</c> seeds, which the message updates.</summary>
    private const string RecordNumber = "202600901";

    private const string Patient = "Haddad, Leila";

    private const string Condition = "Giardiasis";

    /// <summary>
    /// How long the intake and the assignment pass are given: many times their
    /// interval, and bounded, because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task UpdateAnExistingCaseFromALaboratoryMessage()
    {
        // 1. The Events screen, searched by the patient and the disease.
        await Step(Document, 1, "press Events, type Haddad, choose Giardiasis and search", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-events").ClickAsync();
            await Search();
        });

        // 2. The one case on file.
        await Step(Document, 2, "read the count line and the case's row", async () =>
        {
            await ExpectTheOneCase();

            var id = (await Page.Locator("#events-results tbody tr").GetAttributeAsync("id"))!;
            await Expect(Page.Locator($"#{id}-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator($"#{id}-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator($"#{id}-agency")).ToHaveTextAsync("Salt Lake County");
            await Expect(Page.Locator($"#{id}-onset")).ToHaveTextAsync("2026-09-28");
        });

        // 3. The case, opened from its record number; its address kept for
        //    step 7.
        var reportAddress = string.Empty;

        await Step(Document, 3, "press the record number, read the header bar and note the address", async () =>
        {
            await Page.Locator("#events-results tbody tr [id$='-record'] a").ClickAsync();
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(RecordNumber);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 4. No specimen on the case yet.
        await Step(Document, 4, "press Edit, then the Laboratory tab, and read No specimens recorded yet.", async () =>
        {
            await OpenLaboratoryTab();
            await Expect(Page.Locator("#lab-specimens tbody tr")).ToHaveTextAsync(["No specimens recorded yet."]);
        });

        // 5. Copy the message into the drop directory, and see the intake take
        //    it out again.
        await Step(Document, 5, "copy elr-update-example.hl7 into the drop directory and see it taken", async () =>
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

        // 6. The message assigned, choosing All and then assigned again until
        //    the pass has run.
        await Step(Document, 6, "press Staged messages, choose assigned and read the message's row", async () =>
        {
            await Page.Locator("#nav-staged-messages").ClickAsync();
            await Expect(Page.Locator("#staged-messages-heading")).ToHaveTextAsync("Staged messages");

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

        // 7. The case's address again, read afresh: the message's specimen on
        //    its Laboratory tab.
        await Step(Document, 7, "go back to the report's address, open the Laboratory tab and read the specimen", async () =>
        {
            await Open(reportAddress);
            await OpenLaboratoryTab();

            var rows = Page.Locator("#lab-specimens tbody tr");
            await Expect(rows).ToHaveCountAsync(1);
            await Expect(rows.Locator("td").Nth(0)).ToHaveTextAsync("OBR86002");
            await Expect(rows.Locator("td").Nth(1)).ToHaveTextAsync("2026-10-05");
        });

        // 8. Still the one case.
        await Step(Document, 8, "press Events, search Haddad with Giardiasis again and read the one case", async () =>
        {
            await Page.Locator("#nav-events").ClickAsync();
            await Search();
            await ExpectTheOneCase();
        });
    }

    /// <summary>Types the patient's last name, chooses the disease and presses Search.</summary>
    private async Task Search()
    {
        await Expect(Page.Locator("#events-heading")).ToHaveTextAsync("Events");
        await Page.Locator("#events-last-name").FillAsync("Haddad");
        await Choose("Disease", Condition);
        await Page.Locator("#events-search").ClickAsync();
    }

    /// <summary>The count line and the one row, case <see cref="RecordNumber"/>.</summary>
    private async Task ExpectTheOneCase()
    {
        await Expect(Page.Locator("#events-total")).ToHaveTextAsync("1 case(s) found, showing 1.");

        var rows = Page.Locator("#events-results tbody tr");
        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.Locator("[id$='-record']")).ToHaveTextAsync(RecordNumber);
    }

    /// <summary>
    /// Presses Edit, then the Laboratory tab, and waits for its table, which
    /// is what the reader sees once the tab is open.
    /// </summary>
    private async Task OpenLaboratoryTab()
    {
        await Page.Locator("#report-edit").ClickAsync();
        await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
        await Page.Locator("#report-tab-laboratory").ClickAsync();
        await Expect(Page.Locator("#lab-specimens")).ToBeVisibleAsync();
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
