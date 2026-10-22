using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/link-an-eicr-to-an-existing-case.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed holds Amara Osei's
/// open Giardiasis case, <c>202600902</c>, and maps Giardiasis's code. Her
/// eICR carries her name, birth date, sex and address as the seed has them,
/// so the person match finds her exact and the intake links the eICR to her
/// open case. Step 6 waits on the link the way the document tells its reader
/// to — reloading the eICR's screen until its Open case link is there —
/// within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class LinkAnEicrToAnExistingCaseTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/link-an-eicr-to-an-existing-case.md";

    private const string EicrFile = "ecr-link-eicr.xml";

    private const string RrFile = "ecr-link-rr.xml";

    /// <summary>The eICR's id as the queue shows it: its root, a caret and its extension.</summary>
    private const string EicrId = "2.16.840.1.113883.9.9.9.9.9^e2e-link-eicr";

    /// <summary>The case <c>Fixtures/ecr-intake.sql</c> seeds, which the eICR is linked to.</summary>
    private const string RecordNumber = "202600902";

    private const string Patient = "Osei, Amara";

    private const string Condition = "Giardiasis";

    /// <summary>
    /// How long the intake is given: many times its interval, and bounded,
    /// because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task LinkAnEicrToAnExistingCase()
    {
        // 1. The Events screen, searched by the patient and the disease.
        await Step(Document, 1, "press Events, type Osei, choose Giardiasis and search", async () =>
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
        });

        // 3. The two documents, copied into the drop directory and taken.
        await Step(Document, 3, "copy the eICR and its RR into the eCR drop directory and see them taken", async () =>
        {
            foreach (var file in new[] { EicrFile, RrFile })
            {
                await Drop(file);
            }
        });

        // 4. The eCR queue.
        await Step(Document, 4, "press eCR and find the eICR's row", async () =>
        {
            await Page.Locator("#nav-ecr").ClickAsync();
            await Expect(Page.Locator("#ecr-queue-heading")).ToHaveTextAsync("eCR documents");
            await Expect(Row().Locator("td").Nth(0)).ToHaveTextAsync("eICR");
        });

        // 5. The eICR's own screen and its reportable condition.
        await Step(Document, 5, "press the eICR's id and read its condition", async () =>
        {
            await Row().GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page.Locator("#ecr-document-heading")).ToHaveTextAsync($"eICR {EicrId}");

            var cells = Page.Locator("#ecr-document-conditions tbody tr td");
            await Expect(cells.Nth(0)).ToHaveTextAsync("Giardiasis (58265007)");
            await Expect(cells.Nth(4)).ToHaveTextAsync(Condition);
            await Expect(cells.Nth(5)).ToHaveTextAsync("Reportable");
        });

        // 6. The case the eICR was written to, reloading until it is there.
        await Step(Document, 6, "read the one Open case link under Cases", async () =>
        {
            var waited = Stopwatch.StartNew();

            while (await Page.Locator("#ecr-document-no-cases").IsVisibleAsync())
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"No case was written from {EicrId} within {Patience.TotalSeconds:0} seconds of its RR arriving.");

                await Page.WaitForTimeoutAsync(1000);
                await Open(Page.Url);
            }

            var links = Page.Locator("#ecr-document-cases a");
            await Expect(links).ToHaveCountAsync(1);
            await Expect(links).ToHaveTextAsync("Open case");
        });

        // 7. The case it was linked to: the one already on file.
        await Step(Document, 7, "press Open case and read the header bar", async () =>
        {
            await Page.Locator("#ecr-document-cases a").ClickAsync();
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync(Patient);
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync(Condition);
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(RecordNumber);
        });

        // 8. Still the one case.
        await Step(Document, 8, "press Events, search Osei with Giardiasis again and read the one case", async () =>
        {
            await Page.Locator("#nav-events").ClickAsync();
            await Search();
            await ExpectTheOneCase();
        });
    }

    /// <summary>The queue's row for the eICR.</summary>
    private ILocator Row() =>
        Page.Locator("#ecr-queue-table tbody tr").Filter(new() { HasText = EicrId });

    /// <summary>Types the patient's last name, chooses the disease and presses Search.</summary>
    private async Task Search()
    {
        await Expect(Page.Locator("#events-heading")).ToHaveTextAsync("Events");
        await Page.Locator("#events-last-name").FillAsync("Osei");
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
    /// Copies one fixture into the eCR drop directory and waits for the intake
    /// to move it into the directory's <c>.processed</c> folder.
    /// </summary>
    private async Task Drop(string file)
    {
        var dropped = Path.Combine(harness.EcrDirectory, file);
        File.Copy(IntakeHarness.Document(file), dropped, overwrite: true);

        var waited = Stopwatch.StartNew();
        while (File.Exists(dropped))
        {
            Assert.True(
                waited.Elapsed < Patience,
                $"{file} was still in the eCR drop directory after {Patience.TotalSeconds:0} seconds: "
                + "the intake never read it.");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.True(
            File.Exists(Path.Combine(harness.EcrDirectory, ".processed", file)),
            $"{file} left the eCR drop directory but is not in its .processed folder, where the "
            + "document says the intake moves a document it has stored.");
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
