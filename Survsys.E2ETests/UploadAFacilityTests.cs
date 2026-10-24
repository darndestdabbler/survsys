using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// A facility an administrator puts on file by uploading a facility file: the
/// upload previewed and confirmed on the Facilities screen, and the new
/// facility then listed there with its agency, city and zip.
/// </summary>
/// <remarks>
/// <strong>This is not a walkthrough test</strong>, as
/// <see cref="CorrectATownByUploadTests"/> is not: the reference data screens
/// are this tier's own administration rather than a user story taken from the
/// legacy system. It drives the screen by its stable identifiers and the words
/// on it.
///
/// The row's id is blank, so the upload adds the facility. Its agency is Davis
/// County, the seeded agency whose id is 3. The harness clears the facilities
/// table before it seeds, so the database is reseeded before and after, which
/// takes the facility back off file whether the steps pass or not.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class UploadAFacilityTests(SurvsysHarness harness) : SurvsysPageTest
{
    private const string Name = "Itest Uploaded Laboratory";

    [Fact]
    public async Task UploadAFacility()
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
        // The facility file, uploaded and previewed: one facility added, nothing saved yet.
        await Open(harness.WebBaseAddress);
        await Page.Locator("#nav-facilities").ClickAsync();
        await Expect(Page.Locator("#facilities-heading")).ToHaveTextAsync("Facilities");

        await Page.Locator("#facilities-import").SetInputFilesAsync(new FilePayload
        {
            Name = "facilities.csv",
            MimeType = "text/csv",
            Buffer = Encoding.UTF8.GetBytes(
                "id,name,short_name,agency_id,street,city,state,zip,phone\r\n"
                + $",{Name},IUL,3,9 Lab Road,Clearfield,UT,84015,801-555-0199\r\n"),
        });

        await Expect(Page.Locator("#facilities-import-preview")).ToContainTextAsync("The file would add 1, change 0");
        await Expect(Page.Locator("#facilities-import-added-2")).ToHaveTextAsync($"Line 2 adds {Name}");
        await Expect(Page.Locator($"#facilities-table [data-facility='{Name}']")).ToHaveCountAsync(0);

        // Confirmed, and the facility listed with its agency by name, its city and its zip.
        await Page.Locator("#facilities-import-confirm").ClickAsync();

        await Expect(Page.Locator("#facilities-import-done")).ToContainTextAsync("Imported: 1 added, 0 changed, 0 refused.");

        var row = Page.Locator($"#facilities-table [data-facility='{Name}']");
        await Expect(row.Locator("[data-facility-name]")).ToHaveTextAsync(Name);
        await Expect(row.Locator("[data-facility-agency]")).ToHaveTextAsync("Davis County");
        await Expect(row.Locator("[data-facility-city]")).ToHaveTextAsync("Clearfield");
        await Expect(row.Locator("[data-facility-zip]")).ToHaveTextAsync("84015");
    }
}
