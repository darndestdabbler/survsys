using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/maintain-a-loinc-mapping.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/> because that harness's seed holds
/// a sending lab, which a child LOINC must name; the main harness seeds none,
/// and a facility added there would be one the add-a-facility walkthrough's
/// count did not describe. It needs no intake. It deletes what it adds, last,
/// so the vocabulary the other intake walkthroughs translate through is as the
/// seed left it whichever order the collection runs them in. Its rows are
/// found by their codes, which no other walkthrough uses.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class MaintainALoincMappingTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/maintain-a-loinc-mapping.md";

    private const string Loinc = "88017-0";

    private const string Concept = "Listeria monocytogenes DNA [Presence] in Specimen";

    private const string LabCode = "LISPCR";

    /// <summary>The master LOINC codes <c>Fixtures/elr-intake.sql</c> seeds, in the table's order.</summary>
    private static readonly string[] Seeded = ["22314-9", "5061-8", "6463-4"];

    [Fact]
    public async Task MaintainALoincMapping()
    {
        // 1. Open the application tier's address and press Vocabulary.
        await Step(Document, 1, "open the application tier's address and press Vocabulary", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-vocabulary").ClickAsync();
        });

        // 2. The screen, its tabs, and the seeded master LOINC codes.
        await Step(Document, 2, "read the browser tab, heading, tabs, the seeded codes and the form's heading", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("Vocabulary — Survsys");
            await Expect(Page.Locator("#vocabulary-heading")).ToHaveTextAsync("Vocabulary");
            await Expect(Page.GetByRole(AriaRole.Tab)).ToHaveTextAsync(
                ["Master LOINC", "Child LOINC", "Organisms", "Specimens", "Results"]);
            await ExpectSeededMasters();
            await Expect(Page.Locator("#master-loinc-form")).ToContainTextAsync("New master LOINC");
        });

        // 3. The master LOINC, mapped to Listeriosis, saved.
        await Step(Document, 3, "fill the master LOINC's form, choose Listeriosis and press Save", async () =>
        {
            await Page.Locator("#master-loinc-loinc").FillAsync(Loinc);
            await Page.Locator("#master-loinc-concept").FillAsync(Concept);
            await Choose("Condition", new Regex(@"^Listeriosis \(#\d+\)$"));
            await Page.Locator("#master-loinc-save").ClickAsync();
        });

        // 4. Its row, with no lab code yet.
        await Step(Document, 4, "read the new master LOINC's row", async () =>
        {
            var cells = MasterRow().Locator("td");
            await Expect(cells.Nth(0)).ToHaveTextAsync(Loinc);
            await Expect(cells.Nth(1)).ToHaveTextAsync(Concept);
            await Expect(cells.Nth(2)).ToHaveTextAsync("Listeriosis");
            await Expect(cells.Nth(4)).ToHaveTextAsync("");
        });

        // 5. The lab's own code, mapped to that master, saved.
        await Step(Document, 5, "press Child LOINC, fill the child LOINC's form and press Save", async () =>
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = "Child LOINC", Exact = true }).ClickAsync();
            await Expect(Page.Locator("#child-loinc-form")).ToContainTextAsync("New child LOINC");
            await Choose("Sending lab", new Regex(@"^ARUP Laboratories \(ARUP_LAB\)$"));
            await Page.Locator("#child-loinc-code").FillAsync(LabCode);
            await Choose("Master LOINC", new Regex($"^{Regex.Escape(Loinc)}"));
            await Page.Locator("#child-loinc-concept").FillAsync("Listeria PCR");
            await Page.Locator("#child-loinc-save").ClickAsync();
        });

        // 6. Its row.
        await Step(Document, 6, "read the child LOINC's row", async () =>
            await ExpectChildRow("Listeria PCR"));

        // 7. The master's Lab codes now naming the lab's code.
        await Step(Document, 7, "press Master LOINC and read the master's Lab codes", async () =>
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = "Master LOINC", Exact = true }).ClickAsync();
            await Expect(MasterRow().Locator("td").Nth(4)).ToHaveTextAsync($"ARUP_LAB: {LabCode}");
        });

        // 8. The child LOINC edited and saved over.
        await Step(Document, 8, "press Child LOINC, Edit the LISPCR row, change its concept and press Save", async () =>
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = "Child LOINC", Exact = true }).ClickAsync();
            await ChildRow().GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();

            await Expect(Page.Locator("#child-loinc-form")).ToContainTextAsync("Edit child LOINC");
            await Expect(Page.Locator("#child-loinc-code")).ToHaveValueAsync(LabCode);

            await Page.Locator("#child-loinc-concept").FillAsync("Listeria monocytogenes PCR");
            await Page.Locator("#child-loinc-save").ClickAsync();

            await Expect(Page.Locator("#child-loinc-form")).ToContainTextAsync("New child LOINC");
            await ExpectChildRow("Listeria monocytogenes PCR");
        });

        // 9. The child LOINC deleted.
        await Step(Document, 9, "press Delete on the LISPCR row", async () =>
        {
            await ChildRow().GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Expect(ChildRow()).ToHaveCountAsync(0);
        });

        // 10. The master LOINC deleted, and the seeded codes left.
        await Step(Document, 10, "press Master LOINC, Delete the 88017-0 row and read the codes left", async () =>
        {
            await Page.GetByRole(AriaRole.Tab, new() { Name = "Master LOINC", Exact = true }).ClickAsync();
            await MasterRow().GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
            await Expect(MasterRow()).ToHaveCountAsync(0);
            await ExpectSeededMasters();
        });
    }

    private ILocator MasterRow() =>
        Page.Locator("#master-loinc-table tbody tr").Filter(new() { HasText = Loinc });

    private ILocator ChildRow() =>
        Page.Locator("#child-loinc-table tbody tr").Filter(new() { HasText = LabCode });

    private async Task ExpectSeededMasters() =>
        await Expect(Page.Locator("#master-loinc-table tbody tr td:first-child")).ToHaveTextAsync(Seeded);

    /// <summary>The child LOINC's one row, read cell by cell.</summary>
    private async Task ExpectChildRow(string concept)
    {
        await Expect(ChildRow()).ToHaveCountAsync(1);

        var cells = ChildRow().Locator("td");
        await Expect(cells.Nth(0)).ToHaveTextAsync("ARUP_LAB");
        await Expect(cells.Nth(1)).ToHaveTextAsync(LabCode);
        await Expect(cells.Nth(2)).ToHaveTextAsync(Loinc);
        await Expect(cells.Nth(3)).ToHaveTextAsync(concept);
        await Expect(cells.Nth(4)).ToHaveTextAsync("No");
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// whose words match <paramref name="item"/>, and reads that box, which now
    /// shows what was chosen. Matched rather than named exactly, because these
    /// lists show more than the document names: a condition with its number,
    /// a lab with its short name, a LOINC with its concept.
    /// </summary>
    private async Task Choose(string label, Regex item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { NameRegex = item }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
