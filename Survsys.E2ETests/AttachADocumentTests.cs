using System.Globalization;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/attach-a-document.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position.
///
/// Step 1 is a step like any other: the document has its reader write a file
/// before opening the browser, because a file picker can only offer a file
/// that is there, so this test writes that same file, with that same text and
/// no trailing newline, into a folder of its own under the run's temporary
/// directory — not into the repository, which holds no walkthrough's
/// test data — and removes the folder when the walkthrough is over, however
/// it ends.
///
/// The file is chosen through the <c>input type="file"</c> that Blazor's own
/// InputFile renders, which the Attachments panel names rather than hides,
/// so the input the document tells its reader to press is the input this test
/// sets.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class AttachADocumentTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/attach-a-document.md";

    /// <summary>The file the document has its reader write, by name.</summary>
    private const string Filename = "exposure-letter.txt";

    /// <summary>
    /// The one line the document has its reader put in that file, written
    /// with no trailing newline — which is what makes the file the size the
    /// document names and the screen shows.
    /// </summary>
    private const string Line =
        "Letter sent 2026-09-14: advise household contacts to watch for symptoms.";

    /// <summary>The size the screen shows for that file, as the document says.</summary>
    private const string Size = "72 B";

    /// <summary>
    /// The one attachment row as the document promises it at steps 13 and 14:
    /// the filename, the category under its legacy label, the size, the
    /// content type the browser gave the file, and the day it was attached.
    /// </summary>
    private static readonly string[] AttachmentRow =
    [
        Filename,
        "Letter",
        Size,
        "text/plain",
        DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    ];

    [Fact]
    public async Task AttachADocument()
    {
        // 1. The file, written where the picker can offer it.
        var folder = Path.Combine(Path.GetTempPath(), $"survsys-attach-a-document-{Guid.NewGuid():N}");
        var file = Path.Combine(folder, Filename);

        try
        {
            await Step(Document, 1, "write exposure-letter.txt with the document's one line in it", async () =>
            {
                Directory.CreateDirectory(folder);

                // The line and nothing else: no trailing newline, and no
                // byte-order mark, so the file is the 72 bytes the document
                // names and the screen shows.
                await File.WriteAllTextAsync(file, Line, new UTF8Encoding(false));

                Assert.Equal(Line.Length, new FileInfo(file).Length);
            });

            // 2. Open the application tier's address and press New CMR.
            await Step(Document, 2, "open the application tier's address and press New CMR", async () =>
            {
                await Open(harness.WebBaseAddress);
                await Page.Locator("#nav-new-report").ClickAsync();
            });

            // 3. Abbott in Last name, then Search.
            await Step(Document, 3, "type Abbott in Last name and press Search", async () =>
            {
                await Page.Locator("#person-search-last-name").FillAsync("Abbott");
                await Page.Locator("#person-search-search").ClickAsync();
            });

            // 4. One person found: Abbott, Jane.
            await Step(Document, 4, "read the count line and the Abbott, Jane row", async () =>
            {
                await Expect(Page.Locator("#person-search-total"))
                    .ToHaveTextAsync("1 person(s) found, showing 1.");
                await Expect(Page.Locator("#person-search-results [data-person-name]"))
                    .ToHaveTextAsync(["Abbott, Jane"]);
            });

            // 5. Create CMR on that row.
            await Step(Document, 5, "press Create CMR on that row", async () =>
                await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

            // 6. Step 2 of 2. Reporting on Abbott, Jane.
            await Step(Document, 6, "read the Step 2 of 2 line", async () =>
                await Expect(Page.Locator("#new-report-details-person"))
                    .ToContainTextAsync("Step 2 of 2. Reporting on Abbott, Jane"));

            // 7. Listeriosis from the Disease list.
            await Step(Document, 7, "choose Listeriosis from Disease", async () =>
                await Choose("Disease", "Listeriosis"));

            // 8. Salt Lake County from the Investigating agency list.
            await Step(Document, 8, "choose Salt Lake County from Investigating agency", async () =>
                await Choose("Investigating agency", "Salt Lake County"));

            // 9. Save & Continue, the report's header, and its address kept
            //    for step 14.
            var reportAddress = string.Empty;

            await Step(Document, 9, "press Save & Continue, read the report form's header and note its address", async () =>
            {
                await Page.Locator("#report-save").ClickAsync();

                await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Abbott, Jane");
                await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Listeriosis");

                reportAddress = Page.Url;
                Assert.Contains("/event/edit/", reportAddress);
            });

            // 10. The Notes tab: no attachments found, and the entry panel.
            await Step(Document, 10, "press the Notes tab and read the empty Attachments section and the entry panel", async () =>
            {
                await OpenNotesTab();

                await Expect(Page.Locator("#attachments-none"))
                    .ToHaveTextAsync("No attachments found.");
                await Expect(Page.Locator("#attachments-files")).ToHaveCountAsync(0);

                await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Category", Exact = true }))
                    .ToHaveTextAsync("Correspondence");
                await Expect(Page.Locator("#attachments-entry-chosen"))
                    .ToHaveTextAsync("No file chosen.");
                await Expect(Page.Locator("#attachments-entry-upload")).ToHaveTextAsync("Attach file");
            });

            // 11. The category, then the file, then the line that reads it
            //     back.
            await Step(Document, 11, "choose Letter from Category, choose the file, and read its name and size back", async () =>
            {
                await Choose("Category", "Letter");

                await Page.Locator("#attachments-entry-file").SetInputFilesAsync(file);

                await Expect(Page.Locator("#attachments-entry-chosen"))
                    .ToHaveTextAsync($"{Filename} ({Size})");
            });

            // 12. Attach file: the message naming the file.
            await Step(Document, 12, "press Attach file and read the attached message", async () =>
            {
                await Page.Locator("#attachments-entry-upload").ClickAsync();

                await Expect(Page.Locator("#attachments-entry-saved"))
                    .ToContainTextAsync($"{Filename} has been attached.");
                await Expect(Page.Locator("#attachments-entry-error")).ToHaveCountAsync(0);
                await Expect(Page.Locator("#attachments-entry-complaint")).ToHaveCountAsync(0);
            });

            // 13. The table, with the one row.
            await Step(Document, 13, "read the Attachments table's columns and its one row", async () =>
            {
                await Expect(Page.Locator("#attachments-files thead th")).ToHaveTextAsync(
                    ["File", "Category", "Size", "Type", "Attached"]);
                await Expect(Page.Locator("#attachments-none")).ToHaveCountAsync(0);

                await ReadTheOneAttachment();
            });

            // 14. The report's address again, read afresh from the database.
            await Step(Document, 14, "open the report's address again, press Notes and read the row back", async () =>
            {
                await Open(reportAddress);
                await OpenNotesTab();

                await ReadTheOneAttachment();
            });
        }
        finally
        {
            // The file is the reader's own, written outside the repository
            // and taken away again however this walkthrough ended: a failed
            // run leaves no litter in the temporary directory for the next
            // one to trip over.
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// The one attachment row, as the document promises it at steps 13 and
    /// 14. Shared by the two steps that read it because the document says of
    /// the second that it reads what the first did, "exactly as in step 13" —
    /// and a copy of the assertions could drift from that promise.
    /// </summary>
    private async Task ReadTheOneAttachment()
    {
        await Expect(Page.Locator("#attachments-files tbody tr")).ToHaveCountAsync(1);
        await Expect(Page.Locator("#attachments-file-0 td")).ToHaveTextAsync(AttachmentRow);
    }

    /// <summary>
    /// Presses the Notes tab and waits for the Attachments panel's entry
    /// form, which is the first thing the tab shows — and what keeps the next
    /// step from pressing a picker that is not yet there.
    /// </summary>
    private async Task OpenNotesTab()
    {
        await Page.Locator("#report-tab-notes").ClickAsync();
        await Expect(Page.Locator("#attachments-entry")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Opens the list whose box is labelled <paramref name="label"/>, chooses
    /// the item reading <paramref name="item"/> by the words on it, and reads
    /// that box, which now shows what was chosen.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — and <strong>Category</strong> opens on
    /// <strong>Correspondence</strong> — renders the input carrying that
    /// identifier as <c>type="hidden"</c> and shows a sibling box beside it.
    /// The hidden input cannot be pressed, and it is not what the reader
    /// presses: the box labelled Category is. The other walkthroughs' tests
    /// press the identifier because the lists they open — Disease,
    /// Investigating agency, Agency — start empty, so there the identifier is
    /// on the visible box.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
