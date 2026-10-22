using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/merge-duplicate-persons.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position, save the duplicates row and the merge screen's two columns,
/// whose cells and headings the document reads left to right.
///
/// The two people's identifiers are read off the duplicates row's own
/// identifier at step 8, because the database assigns them and no document
/// can name them; the lower is the record on file, which the document keeps.
/// The kept person's cases are compared as a set, because the document names
/// the two reports and not the order the screen lists them in.
///
/// The walkthrough adds a person and merges them away, and other
/// walkthroughs list the people on file and their cases, so the database is
/// reseeded before the steps and again after them, whether they pass or not
/// — the reseeding the document tells its reader to do.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class MergeDuplicatePersonsTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/merge-duplicate-persons.md";

    [Fact]
    public async Task MergeDuplicatePersons()
    {
        harness.Seed();

        try
        {
            await PerformTheSteps();
        }
        finally
        {
            harness.Seed();
        }
    }

    private async Task PerformTheSteps()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Zimmerman, Paul searched for, found once, and Create CMR.
        await Step(Document, 2, "search for Zimmerman, Paul, read the one row and press Create CMR", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-first-name").FillAsync("Paul");
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Zimmerman, Paul"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 3. Hepatitis A, Salt Lake County, and the report saved.
        await Step(Document, 3, "choose Hepatitis A and Salt Lake County, press Save & Continue and read the header", async () =>
        {
            await Choose("Disease", "Hepatitis A");
            await Choose("Investigating agency", "Salt Lake County");
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Hepatitis A");
        });

        // 4. New CMR again. Its heading is read before anything is typed, so
        //    no box is filled while the report's form is still on the page.
        await Step(Document, 4, "press New CMR again and read the creation screen's heading", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Expect(Page.Locator("#new-report-heading")).ToHaveTextAsync("New case morbidity report");
        });

        // 5. The search with his middle initial and date of birth, which
        //    finds nobody and offers Create person and CMR.
        await Step(Document, 5, "search for Zimmerman, Paul J born 1950-07-04 and read No people found.", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-first-name").FillAsync("Paul");
            await Page.Locator("#person-search-middle-name").FillAsync("J");
            await Page.Locator("#person-search-birth-date").FillAsync("1950-07-04");
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-none")).ToHaveTextAsync("No people found.");
            await Expect(Page.Locator("#new-report-create-person")).ToHaveTextAsync("Create person and CMR");
        });

        // 6. Create person and CMR: step two, for the second record.
        await Step(Document, 6, "press Create person and CMR and read the Step 2 of 2 line", async () =>
        {
            await Page.Locator("#new-report-create-person").ClickAsync();
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Zimmerman, Paul");
        });

        // 7. Giardiasis, Utah State, and the second report saved.
        await Step(Document, 7, "choose Giardiasis and Utah State, press Save & Continue and read the header", async () =>
        {
            await Choose("Disease", "Giardiasis");
            await Choose("Investigating agency", "Utah State");
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Giardiasis");
        });

        // 8. Duplicate persons: the one pair, read left to right, and the two
        //    identifiers kept off the row's own identifier.
        int onFile = 0, created = 0;

        await Step(Document, 8, "press Duplicate persons and read the one pair", async () =>
        {
            await Page.Locator("#nav-duplicate-persons").ClickAsync();
            await Expect(Page.Locator("#duplicates-heading")).ToHaveTextAsync("Duplicate persons");

            var row = Page.Locator("#duplicates-table tbody tr");

            await Expect(row).ToHaveCountAsync(1);
            await Expect(row.Locator("td")).ToHaveTextAsync(
            [
                "Zimmerman, Paul", "1950-07-04", "Zimmerman, Paul", "1950-07-04", "80", "Probable",
                "Last name matches; First name matches; Birth date matches",
            ]);

            var ids = (await row.GetAttributeAsync("id"))!["duplicate-row-".Length..].Split('-');

            onFile = int.Parse(ids[0]);
            created = int.Parse(ids[1]);
            Assert.True(onFile < created, $"The pair lists {onFile} before {created}; the record on file is the lower.");
        });

        // 9. The name in the Person column opens the merge screen, the two
        //    records side by side.
        await Step(Document, 9, "press Zimmerman, Paul in the Person column and read the merge screen", async () =>
        {
            await Page.Locator("#duplicates-table tbody tr td").First.GetByRole(AriaRole.Link).ClickAsync();

            await Expect(Page.Locator("#merge-heading")).ToHaveTextAsync("Merge persons");
            await Expect(Page.Locator($"#merge-person-{onFile} h6")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator($"#merge-person-{created} h6")).ToHaveTextAsync("Zimmerman, Paul J");
            await Expect(Page.Locator("[id^='merge-person-']").First).ToHaveIdAsync($"merge-person-{onFile}");
        });

        // 10. Keep this record under the record on file.
        await Step(Document, 10, "press Keep this record under Zimmerman, Paul", async () =>
        {
            await Page.Locator($"#merge-keep-{onFile}").ClickAsync();
            await Expect(Page.Locator($"#merge-keep-{onFile}")).ToHaveTextAsync("Keeping this record");
        });

        // 11. Merge, landing on the kept person's own screen with both reports.
        await Step(Document, 11, "press Merge and read the kept person's screen", async () =>
        {
            await Page.Locator("#merge-submit").ClickAsync();

            await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex($"/people/{onFile}$"));
            await Expect(Page.Locator("#person-heading")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#person-middle-name")).ToHaveTextAsync("—");
            await Expect(Page.Locator("#person-cases-table tbody tr")).ToHaveCountAsync(2);

            var diseases = await Page.Locator("#person-cases-table [id$='-disease']").AllInnerTextsAsync();

            Assert.Equal(["Giardiasis", "Hepatitis A"], diseases.Select(disease => disease.Trim()).Order());
        });

        // 12. Duplicate persons again: no pair left.
        await Step(Document, 12, "press Duplicate persons again and read that none are found", async () =>
        {
            await Page.Locator("#nav-duplicate-persons").ClickAsync();
            await Expect(Page.Locator("#duplicates-none")).ToHaveTextAsync("No duplicate persons were found.");
        });
    }

    /// <summary>
    /// Opens the list labelled <paramref name="label"/>, chooses the item
    /// reading <paramref name="item"/> by the words on it, and reads that box,
    /// which now shows what was chosen — the same way the creation
    /// walkthrough's test does it, and for the same reasons.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
