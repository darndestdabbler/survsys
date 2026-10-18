using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/record-a-treatment.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position. A condition's tick box carries the condition's id, which
/// identity assigns, so it is reached by its label, which is the condition's
/// name the document gives.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RecordATreatmentTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/record-a-treatment.md";

    /// <summary>The conditions the document has the reader tick.</summary>
    private static readonly string[] Listed = ["Bartonellosis", "Lyme disease"];

    [Fact]
    public async Task RecordATreatment()
    {
        // 1. Open the application tier's address and press Treatments.
        await Step(Document, 1, "open the application tier's address, press Treatments and read the screen", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-treatments").ClickAsync();

            await Expect(Page.Locator("#treatments-heading")).ToHaveTextAsync("Treatments");
            await Expect(Page.Locator("#treatments-new")).ToHaveTextAsync("New treatment");
            await Expect(Page.Locator("#treatments-table thead th"))
                .ToHaveTextAsync(["Name", "Active", "Default", "Conditions"]);
        });

        // 2. New treatment: Active ticked, Default not, seven conditions unticked.
        await Step(Document, 2, "press New treatment and read its flags and its seven unticked conditions", async () =>
        {
            await Page.Locator("#treatments-new").ClickAsync();

            await Expect(Page.Locator("#treatment-heading")).ToHaveTextAsync("New treatment");
            await Expect(Box("Active")).ToBeCheckedAsync();
            await Expect(Box("Default")).Not.ToBeCheckedAsync();

            foreach (var condition in SurvsysHarness.SeededConditionsByName)
            {
                await Expect(Box(condition)).Not.ToBeCheckedAsync();
            }
        });

        // 3. Doxycycline in Name.
        await Step(Document, 3, "type Doxycycline in Name", async () =>
            await Page.Locator("#treatment-name").FillAsync("Doxycycline"));

        // 4. Bartonellosis and Lyme disease ticked.
        await Step(Document, 4, "tick Bartonellosis and Lyme disease", async () =>
        {
            foreach (var condition in Listed)
            {
                await Box(condition).CheckAsync();
                await Expect(Box(condition)).ToBeCheckedAsync();
            }
        });

        // 5. Save: back on the list, the treatment's row.
        await Step(Document, 5, "press Save and read the treatment's row on the Treatments screen", async () =>
        {
            await Page.Locator("#treatment-save").ClickAsync();

            await Expect(Page.Locator("#treatments-heading")).ToHaveTextAsync("Treatments");
            await Expect(Row().Locator("td")).ToHaveTextAsync(["Doxycycline", "Yes", "No", "2"]);
        });

        // 6. The treatment's own screen, read afresh from the database.
        await Step(Document, 6, "press Doxycycline and read its name, its flags and its two ticked conditions", async () =>
        {
            await Row().Locator("[data-treatment-name] a").ClickAsync();

            await Expect(Page.Locator("#treatment-heading")).ToHaveTextAsync("Edit treatment");
            await Expect(Page.Locator("#treatment-name")).ToHaveValueAsync("Doxycycline");
            await Expect(Box("Active")).ToBeCheckedAsync();
            await Expect(Box("Default")).Not.ToBeCheckedAsync();

            foreach (var condition in SurvsysHarness.SeededConditionsByName)
            {
                if (Listed.Contains(condition))
                {
                    await Expect(Box(condition)).ToBeCheckedAsync();
                }
                else
                {
                    await Expect(Box(condition)).Not.ToBeCheckedAsync();
                }
            }
        });
    }

    /// <summary>The treatment screen's tick box labelled <paramref name="label"/>.</summary>
    private ILocator Box(string label) =>
        Page.Locator("#treatment-form").GetByRole(AriaRole.Checkbox, new() { Name = label, Exact = true });

    /// <summary>The treatment's row on the Treatments screen, found by its name.</summary>
    private ILocator Row() =>
        Page.Locator("#treatments-table [data-treatment]").Filter(new() { HasText = "Doxycycline" });
}
