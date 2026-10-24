using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/settle-a-held-eicr.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed maps Malaria's code
/// and puts Henrik Dahl on file twice, both records exact matches for the
/// eICR's patient, so the intake holds the eICR rather than choosing. Step 4
/// waits on the hold the way the document tells its reader to — reloading the
/// eICR's screen until its notice is there — within a bound.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class SettleAHeldEicrTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/settle-a-held-eicr.md";

    private const string EicrFile = "ecr-held-eicr.xml";

    private const string RrFile = "ecr-held-rr.xml";

    /// <summary>The eICR's id as the queue shows it: its root, a caret and its extension.</summary>
    private const string EicrId = "2.16.840.1.113883.9.9.9.9.9^e2e-held-eicr";

    /// <summary>
    /// One proposed person as step 4 reads them: the name, the person number
    /// the document leaves as "a number", the birth date and the verdict.
    /// </summary>
    private static readonly Regex ProposedDahl =
        new(@"^Dahl, Henrik \(person (\d+)\), born 1985-06-30 — Exact match$");

    /// <summary>
    /// How long the intake is given: many times its interval, and bounded,
    /// because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task SettleAHeldEicr()
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
            await Expect(cells.Nth(0)).ToHaveTextAsync("Malaria (61462000)");
            await Expect(cells.Nth(4)).ToHaveTextAsync("Malaria");
            await Expect(cells.Nth(5)).ToHaveTextAsync("Reportable");
        });

        // 4. The hold, reloading until it is there, and the two people it
        //    may be, the first with the lower person number.
        await Step(Document, 4, "read the hold, the two proposed people and Create new person", async () =>
        {
            var waited = Stopwatch.StartNew();

            while (await Page.Locator("#ecr-document-no-cases").IsVisibleAsync())
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"{EicrId} was neither held nor written within {Patience.TotalSeconds:0} seconds of its RR arriving.");

                await Page.WaitForTimeoutAsync(1000);
                await Open(Page.Url);
            }

            await Expect(Page.Locator("#ecr-document-case-review")).ToHaveTextAsync(
                "Held for review: 2 people on file are exact matches for the eICR's patient.");

            var numbers = new List<int>();

            for (var m = 0; m < 2; m++)
            {
                var name = Page.Locator($"#ecr-document-match-{m}-name");
                await Expect(name).ToBeVisibleAsync();

                var read = Regex.Replace(await name.InnerTextAsync(), @"\s+", " ").Trim();
                var proposed = ProposedDahl.Match(read);
                Assert.True(proposed.Success, $"Proposed person {m} reads \"{read}\".");
                numbers.Add(int.Parse(proposed.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

                await Expect(Page.Locator($"#ecr-document-match-{m}-use")).ToHaveTextAsync("Use this person");
            }

            await Expect(Page.Locator("#ecr-document-match-2")).ToHaveCountAsync(0);
            Assert.True(numbers[0] < numbers[1], $"The first proposed person is {numbers[0]}, the second {numbers[1]}.");

            await Expect(Page.Locator("#ecr-document-create-person")).ToHaveTextAsync("Create new person");
        });

        // 5. Use this person, beside the first.
        await Step(Document, 5, "press Use this person beside the first and read the one Open case link", async () =>
        {
            await Page.Locator("#ecr-document-match-0-use").ClickAsync();

            await Expect(Page.Locator("#ecr-document-case-review")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#ecr-document-create-person")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#ecr-document-settle-error")).ToHaveCountAsync(0);

            var cases = Page.Locator("#ecr-document-cases");
            await Expect(cases).ToContainTextAsync("The eICR was written to these cases:");
            await Expect(cases.Locator("a")).ToHaveCountAsync(1);
            await Expect(cases.Locator("a")).ToHaveTextAsync("Open case");
        });

        // 6. The new case.
        await Step(Document, 6, "press Open case and read the header bar", async () =>
        {
            await Page.Locator("#ecr-document-cases a").ClickAsync();
            await Expect(Page.Locator("#report-heading")).ToHaveTextAsync("Case morbidity report");
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Dahl, Henrik");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Malaria");
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
