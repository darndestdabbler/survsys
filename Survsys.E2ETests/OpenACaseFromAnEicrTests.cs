using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/open-a-case-from-an-eicr.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed maps Hepatitis A's
/// code and has no Tomas Lindqvist on file, so the person match finds no one
/// and the intake opens a person and a case for him. Step 4 waits on the case
/// the way the document tells its reader to — reloading the eICR's screen
/// until its Open case link is there — within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class OpenACaseFromAnEicrTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/open-a-case-from-an-eicr.md";

    private const string EicrFile = "ecr-open-eicr.xml";

    private const string RrFile = "ecr-open-rr.xml";

    /// <summary>The eICR's id as the queue shows it: its root, a caret and its extension.</summary>
    private const string EicrId = "2.16.840.1.113883.9.9.9.9.9^e2e-open-eicr";

    /// <summary>
    /// How long the intake is given: many times its interval, and bounded,
    /// because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task OpenACaseFromAnEicr()
    {
        // 1. The two documents, copied into the drop directory and taken.
        await Step(Document, 1, "copy the eICR and its RR into the eCR drop directory and see them taken", async () =>
        {
            foreach (var file in new[] { EicrFile, RrFile })
            {
                await Drop(file);
            }
        });

        // 2. The eCR queue.
        await Step(Document, 2, "press eCR and find the eICR's row", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-ecr").ClickAsync();
            await Expect(Page.Locator("#ecr-queue-heading")).ToHaveTextAsync("eCR documents");
            await Expect(Row().Locator("td").Nth(0)).ToHaveTextAsync("eICR");
        });

        // 3. The eICR's own screen and its reportable condition.
        await Step(Document, 3, "press the eICR's id and read its condition", async () =>
        {
            await Row().GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page.Locator("#ecr-document-heading")).ToHaveTextAsync($"eICR {EicrId}");

            var cells = Page.Locator("#ecr-document-conditions tbody tr td");
            await Expect(cells.Nth(0)).ToHaveTextAsync("Hepatitis A (40468003)");
            await Expect(cells.Nth(4)).ToHaveTextAsync("Hepatitis A");
            await Expect(cells.Nth(5)).ToHaveTextAsync("Reportable");
        });

        // 4. The case the eICR was written to, reloading until it is there.
        await Step(Document, 4, "read the one Open case link under Cases", async () =>
        {
            var links = Page.Locator("#ecr-document-cases a");
            var waited = Stopwatch.StartNew();

            while (await Page.Locator("#ecr-document-no-cases").IsVisibleAsync())
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"No case was written from {EicrId} within {Patience.TotalSeconds:0} seconds of its RR arriving.");

                await Page.WaitForTimeoutAsync(1000);
                await Open(Page.Url);
            }

            await Expect(links).ToHaveCountAsync(1);
            await Expect(links).ToHaveTextAsync("Open case");
        });

        // 5. The new case.
        await Step(Document, 5, "press Open case and read the header bar", async () =>
        {
            await Page.Locator("#ecr-document-cases a").ClickAsync();
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Lindqvist, Tomas");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");
            await Expect(Page.Locator("#report-header-agency")).ToHaveTextAsync("Salt Lake County");
            await Expect(Page.Locator("#report-header-record-number")).ToHaveTextAsync(
                new Regex($"^{DateTime.Now.Year}\\d{{5}}$"));
        });
    }

    /// <summary>The queue's row for the eICR.</summary>
    private ILocator Row() =>
        Page.Locator("#ecr-queue-table tbody tr").Filter(new() { HasText = EicrId });

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
}
