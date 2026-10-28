using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/map-a-reportable-condition.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose seed maps no code for
/// Bartonellosis, so the RR it drops has a condition waiting for review until
/// the walkthrough maps it. The mapping it adds is cleared with the other eCR
/// tables when the harness next seeds.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class MapAReportableConditionTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/map-a-reportable-condition.md";

    private const string RrFile = "ecr-map-rr.xml";

    /// <summary>The RR's id as the queue shows it: its root, a caret and its extension.</summary>
    private const string RrId = "2.16.840.1.113883.9.9.9.9.8^e2e-map-rr";

    private const string Snomed = "2.16.840.1.113883.6.96";

    private const string Code = "50694005";

    private const string Condition = "Bartonellosis";

    /// <summary>
    /// How long the intake is given: many times its interval, and bounded,
    /// because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    /// <remarks>
    /// A window taller than Playwright's default 720 pixels. The condition
    /// list sits low on the mapping screen, so in a shorter window it opens
    /// upward, and its first conditions — Bartonellosis among them — open
    /// under the application bar, where no click reaches them.
    /// </remarks>
    public override BrowserNewContextOptions ContextOptions()
    {
        var options = base.ContextOptions();
        options.ViewportSize = new() { Width = 1280, Height = 1000 };

        return options;
    }

    [Fact]
    public async Task MapAReportableCondition()
    {
        // 1. The RR, copied into the drop directory and taken.
        await Step(Document, 1, "copy the RR into the eCR drop directory and see it taken", async () =>
        {
            var dropped = Path.Combine(harness.EcrDirectory, RrFile);
            File.Copy(IntakeHarness.Document(RrFile), dropped, overwrite: true);

            var waited = Stopwatch.StartNew();
            while (File.Exists(dropped))
            {
                Assert.True(
                    waited.Elapsed < Patience,
                    $"{RrFile} was still in the eCR drop directory after {Patience.TotalSeconds:0} seconds: "
                    + "the intake never read it.");
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            Assert.True(
                File.Exists(Path.Combine(harness.EcrDirectory, ".processed", RrFile)),
                $"{RrFile} left the eCR drop directory but is not in its .processed folder, where the "
                + "document says the intake moves a document it has stored.");
        });

        // 2. The queue, narrowed to what needs review.
        await Step(Document, 2, "press eCR, tick Needs review and read the RR's row", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-ecr").ClickAsync();
            await Expect(Page.Locator("#ecr-queue-heading")).ToHaveTextAsync("eCR documents");
            await Page.GetByLabel("Needs review").CheckAsync();

            var row = Row();
            await Expect(row.Locator("td").Nth(0)).ToHaveTextAsync("RR");
            await Expect(row.Locator("[data-ecr-review]")).ToHaveTextAsync("1");
        });

        // 3. The RR, its condition unmapped and waiting for review.
        await Step(Document, 3, "press the RR's id and read its condition waiting for review", async () =>
        {
            await Row().GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page.Locator("#ecr-document-heading")).ToHaveTextAsync($"RR {RrId}");
            await Expect(Page.Locator("#ecr-document-no-eicr")).ToHaveTextAsync(
                "The eICR this response is for has not been received.");

            await ExpectTheCondition(mapsTo: "—", routing: "Needs review");
        });

        // 4. The code, mapped.
        await Step(Document, 4, "press eCR conditions, add the code mapped to Bartonellosis and save", async () =>
        {
            await Page.Locator("#nav-ecr-condition-mappings").ClickAsync();
            await Expect(Page.Locator("#ecr-mappings-heading")).ToHaveTextAsync("eCR condition mapping");

            await Page.Locator("#ecr-mapping-code-system").FillAsync(Snomed);
            await Page.Locator("#ecr-mapping-code").FillAsync(Code);
            await Page.Locator("#ecr-mapping-display-name").FillAsync(Condition);
            await Choose("Condition (empty for unmapped)", Condition);
            await Page.Locator("#ecr-mapping-save").ClickAsync();
        });

        // 5. The mapping's row.
        await Step(Document, 5, "read the code's row in the mapping table", async () =>
        {
            await Expect(Page.Locator("#ecr-mapping-code")).ToHaveValueAsync("");

            var row = Page.Locator("#ecr-mappings-table tbody tr").Filter(new() { HasText = Code });
            await Expect(row).ToHaveCountAsync(1);

            var cells = row.Locator("td");
            await Expect(cells.Nth(0)).ToHaveTextAsync(Snomed);
            await Expect(cells.Nth(1)).ToHaveTextAsync(Code);
            await Expect(cells.Nth(2)).ToHaveTextAsync(Condition);
            await Expect(cells.Nth(3)).ToHaveTextAsync(Condition);
        });

        // 6. The queue again: nothing of the RR's left to review.
        await Step(Document, 6, "press eCR and read the RR's row with nothing to review", async () =>
        {
            await Page.Locator("#nav-ecr").ClickAsync();
            await Expect(Page.Locator("#ecr-queue-heading")).ToHaveTextAsync("eCR documents");
            await Expect(Row().Locator("[data-ecr-review]")).ToHaveTextAsync("0");
        });

        // 7. The RR, its condition mapped and reportable.
        await Step(Document, 7, "press the RR's id and read its condition mapped and reportable", async () =>
        {
            await Row().GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page.Locator("#ecr-document-heading")).ToHaveTextAsync($"RR {RrId}");

            await ExpectTheCondition(mapsTo: Condition, routing: "Reportable");
        });
    }

    /// <summary>The queue's row for the RR.</summary>
    private ILocator Row() =>
        Page.Locator("#ecr-queue-table tbody tr").Filter(new() { HasText = RrId });

    /// <summary>The RR's one condition, mapped to <paramref name="mapsTo"/> and routed <paramref name="routing"/>.</summary>
    private async Task ExpectTheCondition(string mapsTo, string routing)
    {
        var conditions = Page.Locator("#ecr-document-conditions tbody tr");
        await Expect(conditions).ToHaveCountAsync(1);

        var cells = conditions.Locator("td");
        await Expect(cells.Nth(0)).ToHaveTextAsync($"{Condition} ({Code})");
        await Expect(cells.Nth(1)).ToContainTextAsync("Reportable (RRVS1)");
        await Expect(cells.Nth(4)).ToHaveTextAsync(mapsTo);
        await Expect(cells.Nth(5)).ToHaveTextAsync(routing);
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
