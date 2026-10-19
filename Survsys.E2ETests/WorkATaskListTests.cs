using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/work-a-task-list.md</c>, performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// The one test method below performs the document's numbered steps in their
/// written order and asserts at each one what the document tells its reader
/// they will see, selecting by the stable identifiers the screens carry for
/// the words the document names — never by a MudBlazor class or a cell's
/// position. A task's row carries its own identifier rather than its
/// position, so each row is found once by the words in it, its identifier
/// noted, and read by that identifier afterwards: that is what lets step 14
/// press the one row the document names out of four that look alike.
///
/// The document has its reader note the report's number at step 8 and read
/// it again at step 13, in the Report column of the task list screen, and
/// this test keeps it for the same purpose.
///
/// Two screens share this list, and they share its identifiers: the Notes
/// tab's Tasks section and the Tasks screen are the same panel asked for one
/// report and for every report. So each move between them waits on the
/// screen it is moving to before reading anything, or it would read the
/// screen it has just left.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class WorkATaskListTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/work-a-task-list.md";

    /// <summary>The one-off task the document raises first.</summary>
    private const string Interview = "Interview the patient";

    /// <summary>The repeating task the document raises second.</summary>
    private const string Check = "Weekly symptom check";

    /// <summary>The schedule each task of that series carries.</summary>
    private const string WeeklySchedule = "weekly until 2026-11-02";

    /// <summary>The report's address, as the document has the reader note it.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/(\d+)$");

    [Fact]
    public async Task WorkATaskList()
    {
        // 1. Open the application tier's address and press New CMR.
        await Step(Document, 1, "open the application tier's address and press New CMR", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-new-report").ClickAsync();
        });

        // 2. Doe in Last name, then Search.
        await Step(Document, 2, "type Doe in Last name and press Search", async () =>
        {
            await Page.Locator("#person-search-last-name").FillAsync("Doe");
            await Page.Locator("#person-search-search").ClickAsync();
        });

        // 3. One person found, on file under a last name alone.
        await Step(Document, 3, "read the count line and the Doe row", async () =>
        {
            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Doe"]);
        });

        // 4. Create CMR on that row.
        await Step(Document, 4, "press Create CMR on that row", async () =>
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync());

        // 5. Step 2 of 2. Reporting on Doe.
        await Step(Document, 5, "read the Step 2 of 2 line", async () =>
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Doe"));

        // 6. Malaria from the Disease list.
        await Step(Document, 6, "choose Malaria from Disease", async () =>
            await Choose("Disease", "Malaria"));

        // 7. Salt Lake County from the Investigating agency list.
        await Step(Document, 7, "choose Salt Lake County from Investigating agency", async () =>
            await Choose("Investigating agency", "Salt Lake County"));

        // 8. Save & Continue, the report's header, and the report's own
        //    number, which step 13 reads out of the task list.
        var reportId = string.Empty;

        await Step(Document, 8, "press Save & Continue, read the report form's header and note the report's number", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Doe");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Malaria");

            var address = ReportAddress.Match(Page.Url);

            Assert.True(
                address.Success,
                $"The address {Page.Url} does not end in /event/edit/ and a number. The "
                + "document has its reader note that number and read it again in the Report "
                + "column of the task list.");

            reportId = address.Groups[1].Value;
        });

        // 9. The Notes tab's Tasks section: no tasks, and the entry panel.
        await Step(Document, 9, "press the Notes tab and read the empty Tasks section and the Add a task panel", async () =>
        {
            await OpenNotesTab();

            await Expect(Page.Locator("#tasks-none")).ToHaveTextAsync("No tasks found.");
            await Expect(Page.Locator("#tasks-table")).ToHaveCountAsync(0);

            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Show", Exact = true }))
                .ToHaveTextAsync("To do");
            await Expect(Page.GetByRole(AriaRole.Combobox, new() { Name = "Repeats", Exact = true }))
                .ToHaveTextAsync("Not at all");
            await Expect(Page.Locator("#tasks-entry-record")).ToHaveTextAsync("Add task");
        });

        // 10. The one-off task, added and read as the table's one row.
        var interviewId = string.Empty;

        await Step(Document, 10, "add Interview the patient due 2026-10-17 at High and read its row", async () =>
        {
            await Page.Locator("#tasks-entry-name").FillAsync(Interview);
            await Page.Locator("#tasks-entry-due-date").FillAsync("2026-10-17");
            await Page.Locator("#tasks-entry-priority").FillAsync("High");
            await Page.Locator("#tasks-entry-record").ClickAsync();

            await Expect(Page.Locator("#tasks-saved")).ToHaveTextAsync("The task has been added.");
            await Expect(Page.Locator("#tasks-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#tasks-complaint")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#tasks-table thead th")).ToHaveTextAsync(
                ["Due", "Task", "Priority", "Status", "Schedule", "Action"]);
            await Expect(Page.Locator("#tasks-table tbody tr")).ToHaveCountAsync(1);

            interviewId = await IdentifierOf(Interview, "2026-10-17");

            await Expect(Page.Locator($"#tasks-row-{interviewId} td")).ToHaveTextAsync(
                ["2026-10-17", Interview, "High", "open", "—", "Complete"]);
        });

        // 11. The repeating task.
        await Step(Document, 11, "add Weekly symptom check due 2026-10-19, repeating weekly until 2026-11-02", async () =>
        {
            await Page.Locator("#tasks-entry-name").FillAsync(Check);
            await Page.Locator("#tasks-entry-due-date").FillAsync("2026-10-19");
            await Page.Locator("#tasks-entry-priority").FillAsync("Routine");
            await Choose("Repeats", "Weekly");
            await Page.Locator("#tasks-entry-until-date").FillAsync("2026-11-02");
            await Page.Locator("#tasks-entry-record").ClickAsync();
        });

        // 12. The series: the message counting it, and the four rows it
        //     leaves, soonest due first.
        var checkIds = new List<string>();

        await Step(Document, 12, "read the message counting the series and the four rows it leaves", async () =>
        {
            await Expect(Page.Locator("#tasks-saved"))
                .ToHaveTextAsync("The task has been added, as 3 dated tasks.");
            await Expect(Page.Locator("#tasks-error")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#tasks-complaint")).ToHaveCountAsync(0);

            await Expect(Page.Locator("#tasks-table tbody tr")).ToHaveCountAsync(4);

            // The order the document promises: soonest due first, the
            // interview ahead of the three Mondays.
            await Expect(Page.Locator("#tasks-table tbody [id$='-name']"))
                .ToHaveTextAsync([Interview, Check, Check, Check]);

            foreach (var due in new[] { "2026-10-19", "2026-10-26", "2026-11-02" })
            {
                var id = await IdentifierOf(Check, due);

                checkIds.Add(id);

                await Expect(Page.Locator($"#tasks-row-{id} td")).ToHaveTextAsync(
                    [due, Check, "Routine", "open", WeeklySchedule, "Complete"]);
            }
        });

        // 13. The task list screen, with the report beside each task.
        await Step(Document, 13, "press Tasks in the application bar and read the four rows with the report's number", async () =>
        {
            await OpenTheTaskListScreen();

            await Expect(Page.Locator("#tasks-table thead th")).ToHaveTextAsync(
                ["Due", "Task", "Priority", "Status", "Schedule", "Report", "Action"]);
            await Expect(Page.Locator("#tasks-table tbody [id$='-name']"))
                .ToHaveTextAsync([Interview, Check, Check, Check]);

            foreach (var id in checkIds.Prepend(interviewId))
            {
                await Expect(Page.Locator($"#tasks-row-{id}-report")).ToHaveTextAsync(reportId);
            }
        });

        // 14. Complete on the one row the document names, which stays where
        //     it is with nothing left to press.
        await Step(Document, 14, "press Complete on the Interview the patient row and read it done with the others untouched", async () =>
        {
            await Page.Locator($"#tasks-row-{interviewId}-complete").ClickAsync();

            await Expect(Page.Locator("#tasks-saved")).ToHaveTextAsync("The task has been completed.");
            await Expect(Page.Locator("#tasks-error")).ToHaveCountAsync(0);

            await Expect(Page.Locator($"#tasks-row-{interviewId}-status")).ToHaveTextAsync("complete");
            await Expect(Page.Locator($"#tasks-row-{interviewId}-complete")).ToHaveCountAsync(0);
            await Expect(Page.Locator("#tasks-table tbody tr")).ToHaveCountAsync(4);

            foreach (var id in checkIds)
            {
                await Expect(Page.Locator($"#tasks-row-{id}-status")).ToHaveTextAsync("open");
                await Expect(Page.Locator($"#tasks-row-{id}-complete")).ToHaveTextAsync("Complete");
            }
        });

        // 15. Done: the completed task alone.
        await Step(Document, 15, "choose Done from Show and read the one completed row", async () =>
        {
            await Choose("Show", "Done");

            await Expect(Page.Locator("#tasks-table tbody tr")).ToHaveCountAsync(1);
            await Expect(Page.Locator($"#tasks-row-{interviewId}-status")).ToHaveTextAsync("complete");

            foreach (var id in checkIds)
            {
                await Expect(Page.Locator($"#tasks-row-{id}")).ToHaveCountAsync(0);
            }
        });

        // 16. To do: the three that are not done, read afresh from the
        //     database, which is how the completion is proved.
        await Step(Document, 16, "choose To do from Show and read the three rows that are not done", async () =>
        {
            await Choose("Show", "To do");

            await Expect(Page.Locator("#tasks-table tbody tr")).ToHaveCountAsync(3);
            await Expect(Page.Locator($"#tasks-row-{interviewId}")).ToHaveCountAsync(0);

            foreach (var id in checkIds)
            {
                await Expect(Page.Locator($"#tasks-row-{id}-status")).ToHaveTextAsync("open");
            }
        });
    }

    /// <summary>
    /// The identifier a task's row carries, found by the two things the
    /// document names it by: the words in the <strong>Task</strong> column
    /// and the day in the <strong>Due</strong> column, which together name
    /// one of the three rows of a series that otherwise read alike.
    /// </summary>
    private async Task<string> IdentifierOf(string task, string due)
    {
        // The two cells are matched by the identifiers they carry and by
        // their whole text, not by the words appearing anywhere in the row:
        // every task of a series carries "weekly until 2026-11-02" in its
        // Schedule cell, so a row filtered on that date as text is all three
        // of them.
        var row = Page.Locator("#tasks-table tbody tr")
            .Filter(new() { Has = Page.Locator($"[id$='-name']:text-is(\"{task}\")") })
            .Filter(new() { Has = Page.Locator($"[id$='-due-date']:text-is(\"{due}\")") });

        await Expect(row).ToHaveCountAsync(1);

        var identifier = await row.GetAttributeAsync("id");

        Assert.StartsWith("tasks-row-", identifier, StringComparison.Ordinal);

        return identifier!["tasks-row-".Length..];
    }

    /// <summary>
    /// Presses the Notes tab and waits for the Tasks section's entry panel,
    /// which is at the foot of the tab — and what keeps the next step from
    /// typing into a form that is not yet there.
    /// </summary>
    private async Task OpenNotesTab()
    {
        await Page.Locator("#report-tab-notes").ClickAsync();
        await Expect(Page.Locator("#tasks-entry")).ToBeVisibleAsync();
    }

    /// <summary>
    /// Presses Tasks in the application bar and waits for that screen's own
    /// heading and lead-in.
    /// </summary>
    /// <remarks>
    /// The heading is waited on before anything on the list is read, because
    /// the list on this screen and the list in the Notes tab's Tasks section
    /// are the same panel and carry the same identifiers: a row read before
    /// this screen is there is a row of the screen just left. The heading is
    /// this screen's and the Notes tab has none like it.
    /// </remarks>
    private async Task OpenTheTaskListScreen()
    {
        await Page.Locator("#nav-tasks").ClickAsync();

        await Expect(Page.Locator("#tasks-heading")).ToHaveTextAsync("Tasks");
        await Expect(Page.Locator("#tasks-lead-in"))
            .ToContainTextAsync("The tasks raised against every case, soonest due first.");
        await Expect(Page.Locator("#tasks-entry")).ToHaveCountAsync(0);
    }

    /// <summary>
    /// Opens the list whose box is labelled <paramref name="label"/>, chooses
    /// the item reading <paramref name="item"/> by the words on it, and reads
    /// that box, which now shows what was chosen.
    /// </summary>
    /// <remarks>
    /// The box is reached by the label the document names it by rather than
    /// by the identifier the markup puts on the list, because a list that
    /// already holds a value — and <strong>Show</strong> opens on <strong>To
    /// do</strong>, <strong>Repeats</strong> on <strong>Not at all</strong> —
    /// renders the input carrying that identifier as <c>type="hidden"</c> and
    /// shows a sibling box beside it. The hidden input cannot be pressed, and
    /// it is not what the reader presses: the box carrying the label is.
    /// </remarks>
    private async Task Choose(string label, string item)
    {
        var box = Page.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
