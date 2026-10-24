using System.Globalization;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/remove-an-attachment.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// Step 1 writes the file the document has its reader write, into a folder of
/// its own under the run's temporary directory, removed however the
/// walkthrough ends, as <see cref="AttachADocumentTests"/> does. The box that
/// asks before the delete is found by its role, and its own Delete within it,
/// because the row's Delete reads the same.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class RemoveAnAttachmentTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/remove-an-attachment.md";

    /// <summary>The file the document has its reader write, by name.</summary>
    private const string Filename = "contact-list.txt";

    /// <summary>The one line in that file, with no trailing newline.</summary>
    private const string Line = "Household contacts: two adults, one child.";

    [Fact]
    public async Task RemoveAnAttachment()
    {
        // 1. The file, written where the picker can offer it.
        var folder = Path.Combine(Path.GetTempPath(), $"survsys-remove-an-attachment-{Guid.NewGuid():N}");
        var file = Path.Combine(folder, Filename);

        try
        {
            await Step(Document, 1, "write contact-list.txt with the document's one line in it", async () =>
            {
                Directory.CreateDirectory(folder);
                await File.WriteAllTextAsync(file, Line, new UTF8Encoding(false));

                Assert.Equal(42, new FileInfo(file).Length);
            });

            // 2. Open the application tier's address and press New CMR.
            await Step(Document, 2, "open the application tier's address and press New CMR", async () =>
            {
                await Open(harness.WebBaseAddress);
                await Page.Locator("#nav-new-report").ClickAsync();
            });

            // 3. Abbott in Last name, Search, and the one person found.
            await Step(Document, 3, "type Abbott in Last name, press Search and read the Abbott, Jane row", async () =>
            {
                await Page.Locator("#person-search-last-name").FillAsync("Abbott");
                await Page.Locator("#person-search-search").ClickAsync();

                await Expect(Page.Locator("#person-search-total"))
                    .ToHaveTextAsync("1 person(s) found, showing 1.");
                await Expect(Page.Locator("#person-search-results [data-person-name]"))
                    .ToHaveTextAsync(["Abbott, Jane"]);
            });

            // 4. Create CMR, Listeriosis and Salt Lake County.
            await Step(Document, 4, "press Create CMR and choose Listeriosis and Salt Lake County", async () =>
            {
                await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
                await Choose("Disease", "Listeriosis");
                await Choose("Investigating agency", "Salt Lake County");
            });

            // 5. Save & Continue, the header, and the address kept for step 10.
            var reportAddress = string.Empty;

            await Step(Document, 5, "press Save & Continue, read the header and note the address", async () =>
            {
                await Page.Locator("#report-save").ClickAsync();

                await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
                await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Listeriosis");

                reportAddress = Page.Url;
                Assert.Contains("/event/edit/", reportAddress);
            });

            // 6. Edit, then Notes: no attachments yet.
            await Step(Document, 6, "press Edit, then the Notes tab, and read No attachments found.", async () =>
            {
                await Page.Locator("#report-edit").ClickAsync();
                await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
                await OpenNotesTab();

                await Expect(Page.Locator("#attachments-none")).ToHaveTextAsync("No attachments found.");
            });

            // 7. The file attached under Correspondence, its row ending in Delete.
            await Step(Document, 7, "choose the file, press Attach file and read its row", async () =>
            {
                await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Category", Exact = true }))
                    .ToHaveTextAsync("Correspondence");

                await Page.Locator("#attachments-entry-file").SetInputFilesAsync(file);
                await Page.Locator("#attachments-entry-upload").ClickAsync();

                await Expect(Page.Locator("#attachments-entry-saved"))
                    .ToContainTextAsync($"{Filename} has been attached.");
                await Expect(Page.Locator("#attachments-files tbody tr")).ToHaveCountAsync(1);
                await Expect(Page.Locator("#attachments-file-0 td")).ToHaveTextAsync(
                [
                    Filename,
                    "Correspondence",
                    "42 B",
                    "text/plain",
                    DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    "Delete",
                ]);
            });

            // 8. The row's Delete, and the box that asks first.
            await Step(Document, 8, "press Delete on the row and read the box that asks first", async () =>
            {
                await Page.Locator("#attachments-file-0-delete").ClickAsync();

                var box = Page.GetByRole(AriaRole.Dialog);
                await Expect(box).ToContainTextAsync("Delete this attachment?");
                await Expect(box).ToContainTextAsync($"{Filename} and its file are removed, and cannot be restored.");
                await Expect(box.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeVisibleAsync();
                await Expect(box.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true })).ToBeVisibleAsync();
            });

            // 9. Delete in the box: the table gone.
            await Step(Document, 9, "press Delete in the box and read No attachments found.", async () =>
            {
                var box = Page.GetByRole(AriaRole.Dialog);
                await box.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();

                await Expect(box).ToHaveCountAsync(0);
                await Expect(Page.Locator("#attachments-delete-error")).ToHaveCountAsync(0);
                await Expect(Page.Locator("#attachments-files")).ToHaveCountAsync(0);
                await Expect(Page.Locator("#attachments-none")).ToHaveTextAsync("No attachments found.");
            });

            // 10. The report's address again, read afresh from the database.
            await Step(Document, 10, "open the report's address again, press Notes and read No attachments found.", async () =>
            {
                await Open(reportAddress);
                await OpenNotesTab();

                await Expect(Page.Locator("#attachments-none")).ToHaveTextAsync("No attachments found.");
                await Expect(Page.Locator("#attachments-files")).ToHaveCountAsync(0);
            });
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// Presses the Notes tab and waits for the Attachments panel's entry
    /// form, which is the first thing the tab shows.
    /// </summary>
    private async Task OpenNotesTab()
    {
        await Page.Locator("#report-tab-notes").ClickAsync();
        await Expect(Page.Locator("#attachments-entry")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Opens the list whose box is labelled <paramref name="label"/>, chooses
    /// the item reading <paramref name="item"/>, and reads that box back.
    /// </summary>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
