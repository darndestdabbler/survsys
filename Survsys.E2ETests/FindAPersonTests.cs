using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/find-a-person.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order, one after another with nothing in between, and asserts at
/// each one what the document tells its reader they will see. It selects by
/// the stable identifiers the people screen and the search panel carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position — so the document's wording and this test point at the same
/// control. The person IDs are identity-assigned, so neither the document
/// nor this test says what they read.
///
/// The harness is already running both tiers over a seeded database and is
/// already recording video. The first step opens the screen through
/// <see cref="SurvsysPageTest.Open"/>, which waits for the circuit the
/// buttons need; every later step presses into a screen that is already
/// interactive.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class FindAPersonTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/find-a-person.md";

    /// <summary>
    /// The two rows the document's step 6 prints, in the order it prints
    /// them: by last name and then first name, which is the order the screen
    /// shows.
    /// </summary>
    private static readonly (string Name, string BirthDate, string BirthSex)[] Rodriguezes =
    [
        ("Rodriguez, Carlos", "1960-01-20", "M"),
        ("Rodriguez, Maria", "1985-06-15", "F"),
    ];

    /// <summary>
    /// The seven rows the document's step 8 prints, in the order it prints
    /// them. The em dash is what the screen shows for a date of birth nobody
    /// has given, and the document says so in those words.
    /// </summary>
    private static readonly (string Name, string BirthDate, string BirthSex)[] Everyone =
    [
        ("Abbott, Jane", "—", "F"),
        ("Doe", "—", "UNK"),
        ("Nguyen, An", "1992-02-29", "M"),
        ("Okafor, Chidi", "1978-11-03", "M"),
        ("Rodriguez, Carlos", "1960-01-20", "M"),
        ("Rodriguez, Maria", "1985-06-15", "F"),
        ("Zimmerman, Paul", "1950-07-04", "M"),
    ];

    [Fact]
    public async Task FindAPerson()
    {
        // 1. Open the application tier's address and press People in the
        //    application bar.
        await Step(Document, 1, "open the application tier's address and press People", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-people").ClickAsync();
        });

        // 2. The browser tab reads People — Survsys; the heading reads People.
        await Step(Document, 2, "read the browser tab and the heading", async () =>
        {
            await Expect(Page).ToHaveTitleAsync("People — Survsys");
            await Expect(Page.Locator("#people-heading")).ToHaveTextAsync("People");
        });

        // 3. The lead-in directly under the heading.
        await Step(Document, 3, "read the lead-in under the heading", async () =>
            await Expect(Page.Locator("#people-lead-in")).ToHaveTextAsync(
                "Search for a person by name, date of birth, telephone or birth sex. "
                + "Leave every box empty to list everyone on file."));

        // 4. Rodriguez in Last name, then Search.
        await Step(Document, 4, "type Rodriguez in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 5. The count line above the table.
        await Step(Document, 5, "read the count line", async () =>
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("2 person(s) found, showing 2."));

        // 6. The four column headings, and the two rows in order.
        await Step(Document, 6, "read the column headings and the two Rodriguez rows", async () =>
        {
            await Expect(Page.Locator("#person-search-results thead th"))
                .ToHaveTextAsync(["ID", "Last, First", "Birth date", "Birth sex"]);
            await ExpectRows(Rodriguezes);
        });

        // 7. Clear empties the box and takes the count line and the table away.
        await Step(Document, 7, "press Clear", async () =>
        {
            await Page.Locator("#person-search-clear").ClickAsync();

            await Expect(Page.Locator("#person-search-last-name")).ToHaveValueAsync("");
            await Expect(Page.Locator("#person-search-total")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#person-search-results")).ToHaveCountAsync(0);
        });

        // 8. Search with every box empty: everyone on file, in order, with an
        //    em dash and UNK where nobody has said.
        await Step(Document, 8, "press Search with every box empty and read everyone on file", async () =>
        {
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("7 person(s) found, showing 7.");
            await ExpectRows(Everyone);
        });

        // 9. Nobody in Last name, then Search: no people found.
        await Step(Document, 9, "type Nobody in Last name, press Search, and read No people found.", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Nobody");
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-none")).ToHaveTextAsync("No people found.");
            await Expect(Page.Locator("#person-search-results")).ToHaveCountAsync(0);
        });
    }

    /// <summary>
    /// The rows the document prints, read down each named column: exactly
    /// these, in this order, and no others.
    /// </summary>
    private async Task ExpectRows((string Name, string BirthDate, string BirthSex)[] rows)
    {
        await Expect(Page.Locator("#person-search-results tbody tr")).ToHaveCountAsync(rows.Length);
        await Expect(Page.Locator("#person-search-results [data-person-name]"))
            .ToHaveTextAsync(rows.Select(row => row.Name).ToArray());
        await Expect(Page.Locator("#person-search-results [data-person-birth-date]"))
            .ToHaveTextAsync(rows.Select(row => row.BirthDate).ToArray());
        await Expect(Page.Locator("#person-search-results [data-person-birth-gender]"))
            .ToHaveTextAsync(rows.Select(row => row.BirthSex).ToArray());
    }
}
