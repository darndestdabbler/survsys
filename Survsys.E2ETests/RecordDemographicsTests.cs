using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/record-demographics.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position. The two lists are opened and their item chosen by the text the
/// document names, and the chosen words read back from the labelled box, as
/// the creation walkthrough's test does.
///
/// The current age is counted here from the date of birth the document
/// prints to today, in the same way the screen counts it, rather than
/// written down as a number the document would have to change every June.
/// The report's address is kept from step 9 and opened again at step 19,
/// which is how the document has its reader prove the save reached the
/// database.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RecordDemographicsTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/record-demographics.md";

    /// <summary>The date of birth the document prints at step 11.</summary>
    private static readonly DateOnly BornOn = new(1985, 6, 15);

    [Fact]
    public async Task RecordDemographics()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Rodriguez in Last name, Maria in First name, then Search.
        await Step(Document, 2, "type Rodriguez in Last name and Maria in First name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Rodriguez");
            await Page.Locator("#person-search-first-name").FillAsync("Maria");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found: Rodriguez, Maria.
        await Step(Document, 3, "read the count line and the Rodriguez, Maria row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Rodriguez, Maria"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Rodriguez, Maria.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Rodriguez, Maria"));

        // 6. Giardiasis from the Disease list.
        await Step(Document, 6, "choose Giardiasis from Disease", async () =>
            await Choose("Disease", "Giardiasis"));

        // 7. Utah State from the Investigating agency list.
        await Step(Document, 7, "choose Utah State from Investigating agency", async () =>
            await Choose("Investigating agency", "Utah State"));

        // 8. Save & Continue.
        await Step(Document, 8, "press Save & Continue", async () =>
            await Page.Locator("#report-save").ClickAsync());

        // 9. The report's form: the header names her and the disease, and
        //    the nine tabs are there, Demographic open. Its address is kept
        //    for step 19.
        string reportAddress = string.Empty;

        await Step(Document, 9, "read the report form's header and tab strip, and note its address", async () =>
        {
            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Rodriguez, Maria");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Giardiasis");
            await Expect(Page.Locator("[id^='report-tab-']")).ToHaveTextAsync(
                ["Demographic", "Clinical", "Laboratory", "Investigation", "Administrative", "Forms", "Vaccines", "Contacts", "Notes"]);
            await Expect(Page.Locator("#demographics-name")).ToBeVisibleAsync();

            reportAddress = Page.Url;
            Assert.Contains("/event/edit/", reportAddress);
        });

        // 10. The Name section.
        await Step(Document, 10, "read the Name section", async () =>
        {
            await Expect(Page.Locator("#demographics-last-name")).ToHaveValueAsync("Rodriguez");
            await Expect(Page.Locator("#demographics-first-name")).ToHaveValueAsync("Maria");
            await Expect(Page.Locator("#demographics-middle-name")).ToHaveValueAsync("Elena");
            await Expect(Page.Locator("#demographics-guardian-0")).ToHaveValueAsync("Carlos Rodriguez");
            await Expect(Page.Locator("#demographics-guardian-1")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#demographics-alias-0")).ToHaveValueAsync("Maria Elena Garcia");
            await Expect(Page.Locator("#demographics-alias-1")).ToHaveCountAsync(0);
        });

        // 11. The Age section: the date of birth, the age counted from it to
        //     today, and the age at onset — the same number, because the
        //     report's event date is the day it was opened.
        await Step(Document, 11, "read the Age section", async () =>
        {
            var age = AgeToday().ToString();

            await Expect(Page.Locator("#demographics-birth-date")).ToHaveValueAsync("1985-06-15");
            await Expect(Page.Locator("#demographics-current-age")).ToHaveTextAsync(age);
            await Expect(Page.Locator("#demographics-age-at-onset")).ToHaveTextAsync(age);
        });

        // 12. The one address.
        await Step(Document, 12, "read the Addresses section", async () =>
        {
            await Expect(Page.Locator("#demographics-address-0-street")).ToHaveValueAsync("1234 Main Street");
            await Expect(Page.Locator("#demographics-address-0-unit-number")).ToHaveValueAsync("Apt 5B");
            await Expect(Page.Locator("#demographics-address-0-city")).ToHaveValueAsync("Salt Lake City");
            await Expect(Page.Locator("#demographics-address-0-state")).ToHaveValueAsync("UT");
            await Expect(Page.Locator("#demographics-address-0-postal-code")).ToHaveValueAsync("84101");
            await Expect(Page.Locator("#demographics-address-0-county")).ToHaveValueAsync("SL");
            await Expect(Page.Locator("#demographics-address-1-street")).ToHaveCountAsync(0);
        });

        // 13. The one telephone and the one email address.
        await Step(Document, 13, "read the Telephones and Email Addresses sections", async () =>
        {
            await Expect(Page.Locator("#demographics-telephone-0-area-code")).ToHaveValueAsync("801");
            await Expect(Page.Locator("#demographics-telephone-0-phone-number")).ToHaveValueAsync("5551234");
            await Expect(Page.Locator("#demographics-telephone-1-area-code")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#demographics-email-0-address")).ToHaveValueAsync("maria.rodriguez@example.com");
            await Expect(Page.Locator("#demographics-email-1-address")).ToHaveCountAsync(0);
        });

        // 14. The Demographics section, with the two races by code.
        await Step(Document, 14, "read the Demographics section", async () =>
        {
            await Expect(Page.Locator("#demographics-birth-gender")).ToHaveValueAsync("F");
            await Expect(Page.Locator("#demographics-current-gender")).ToHaveValueAsync("F");
            await Expect(Page.Locator("#demographics-language")).ToHaveValueAsync("es");
            await Expect(Page.Locator("#demographics-ethnicity")).ToHaveValueAsync("H");
            await Expect(Page.Locator("#demographics-country-of-birth")).ToHaveValueAsync("MEX");
            await Expect(Page.Locator("#demographics-race-0")).ToHaveValueAsync("AA");
            await Expect(Page.Locator("#demographics-race-1")).ToHaveValueAsync("W");
            await Expect(Page.Locator("#demographics-race-2")).ToHaveCountAsync(0);
        });

        // 15. The unit number, changed.
        await Step(Document, 15, "change Unit number to Apt 7C", async () =>
            await Page.Locator("#demographics-address-0-unit-number").FillAsync("Apt 7C"));

        // 16. A second telephone.
        await Step(Document, 16, "press Add telephone and fill the new row's Area and Phone", async () =>
        {
            await Page.Locator("#demographics-telephone-add").ClickAsync();
            await Page.Locator("#demographics-telephone-1-area-code").FillAsync("801");
            await Page.Locator("#demographics-telephone-1-phone-number").FillAsync("5559876");
        });

        // 17. A second email address.
        await Step(Document, 17, "press Add email address and fill the new row's Email address", async () =>
        {
            await Page.Locator("#demographics-email-add").ClickAsync();
            await Page.Locator("#demographics-email-1-address").FillAsync("maria.e.rodriguez@example.com");
        });

        // 18. Save & Continue, and the message that the save succeeded.
        await Step(Document, 18, "press Save & Continue and read the saved message", async () =>
        {
            await Page.Locator("#demographics-save").ClickAsync();
            await Expect(Page.Locator("#demographics-saved")).ToContainTextAsync("The demographics have been saved.");
            await Expect(Page.Locator("#demographics-error")).ToHaveCountAsync(0);
        });

        // 19. The report's address again, read afresh from the database.
        await Step(Document, 19, "open the report's address again and read the saved values back", async () =>
        {
            await Open(reportAddress);

            await Expect(Page.Locator("#report-tab-demographic")).ToHaveTextAsync("Demographic");
            await Expect(Page.Locator("#demographics-address-0-unit-number")).ToHaveValueAsync("Apt 7C");

            await Expect(Page.Locator("#demographics-telephone-0-area-code")).ToHaveValueAsync("801");
            await Expect(Page.Locator("#demographics-telephone-0-phone-number")).ToHaveValueAsync("5551234");
            await Expect(Page.Locator("#demographics-telephone-1-area-code")).ToHaveValueAsync("801");
            await Expect(Page.Locator("#demographics-telephone-1-phone-number")).ToHaveValueAsync("5559876");
            await Expect(Page.Locator("#demographics-telephone-2-area-code")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#demographics-email-0-address")).ToHaveValueAsync("maria.rodriguez@example.com");
            await Expect(Page.Locator("#demographics-email-1-address")).ToHaveValueAsync("maria.e.rodriguez@example.com");
            await Expect(Page.Locator("#demographics-email-2-address")).ToHaveCountAsync(0);
        });
    }

    /// <summary>
    /// Her age in whole years today, counted as a birthday is: one more each
    /// time the day and month of her birth come round.
    /// </summary>
    private static int AgeToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var years = today.Year - BornOn.Year;

        if (today < BornOn.AddYears(years))
        {
            years--;
        }

        return years;
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
