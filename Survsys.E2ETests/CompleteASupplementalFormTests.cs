using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/complete-a-supplemental-form.md</c>,
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
/// position. Every list is opened and its item chosen by the text the
/// document names, as the creation walkthrough's test does.
///
/// The builder's two questions each carry an **Answer type** list, so the
/// second one's is opened inside that question's own panel rather than by its
/// label across the screen. The Forms tab's questions are reached by their
/// labels, which are the question's words, because the identifiers it gives
/// them carry the question's id and identity assigns the ids.
/// </remarks>
[Collection(SurvsysCollection.Name)]
public class CompleteASupplementalFormTests(SurvsysHarness harness) : SurvsysPageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/complete-a-supplemental-form.md";

    /// <summary>The form's name, as the document has the reader type it.</summary>
    private const string FormName = "Lyme disease tick exposure";

    /// <summary>The text question, as the document has the reader type it.</summary>
    private const string TextQuestion = "Where was the patient when the tick bite happened?";

    /// <summary>The coded question, as the document has the reader type it.</summary>
    private const string CodedQuestion = "Was a tick found attached to the patient?";

    /// <summary>The report's address, as the document has the reader note it.</summary>
    private static readonly Regex ReportAddress = new(@"/event/edit/\d+$");

    [Fact]
    public async Task CompleteASupplementalForm()
    {
        // 1. Open the application tier's address and press Forms.
        await Step(Document, 1, "open the application tier's address, press Forms and read the screen", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-forms").ClickAsync();

            await Expect(Page.Locator("#forms-heading")).ToHaveTextAsync("Forms");
            await Expect(Page.Locator("#forms-new")).ToHaveTextAsync("New form");
            await Expect(Page.Locator("#forms-table thead th"))
                .ToHaveTextAsync(["Name", "Condition", "Auto-assign"]);
        });

        // 2. New form, with no sections yet.
        await Step(Document, 2, "press New form and read the empty Sections panel", async () =>
        {
            await Page.Locator("#forms-new").ClickAsync();

            await Expect(Page.Locator("#form-heading")).ToHaveTextAsync("New form");
            await Expect(Page.Locator("#form-sections-empty"))
                .ToHaveTextAsync("No sections yet. Add one to begin.");
        });

        // 3. Name, Lyme disease from Condition, Auto-assign ticked.
        await Step(Document, 3, "type the name, choose Lyme disease from Condition and tick Auto-assign", async () =>
        {
            await Page.Locator("#form-name").FillAsync(FormName);
            await Choose(Page.Locator("#form-core"), "Condition", "Lyme disease");

            var autoAssign = Page.GetByRole(AriaRole.Checkbox, new() { Name = "Auto-assign", Exact = true });
            await autoAssign.CheckAsync();
            await Expect(autoAssign).ToBeCheckedAsync();
        });

        // 4. Add section, titled Tick exposure.
        await Step(Document, 4, "press Add section and type the section title", async () =>
        {
            await Page.Locator("#form-add-section").ClickAsync();
            await Page.Locator("#form-section-0-title").FillAsync("Tick exposure");
        });

        // 5. Add question: the text question, its answer type left at Text.
        await Step(Document, 5, "press Add question, type the text question and leave its Answer type at Text", async () =>
        {
            await Page.Locator("#form-section-0-add-question").ClickAsync();
            await Page.Locator("#form-section-0-question-0-text").FillAsync(TextQuestion);

            await Expect(AnswerType(0)).ToHaveTextAsync("Text");
        });

        // 6. Add question again: the coded question, its answer type Coded.
        await Step(Document, 6, "press Add question again, type the coded question and choose Coded from its Answer type", async () =>
        {
            await Page.Locator("#form-section-0-add-question").ClickAsync();
            await Page.Locator("#form-section-0-question-1-text").FillAsync(CodedQuestion);

            await AnswerType(1).ClickAsync();
            await Page.GetByRole(AriaRole.Option, new() { Name = "Coded", Exact = true }).ClickAsync();
            await Expect(AnswerType(1)).ToHaveTextAsync("Coded");

            await Expect(Page.Locator("#form-section-0-question-1-add-option")).ToHaveTextAsync("Add choice");
        });

        // 7. Two choices: Y Yes and N No.
        await Step(Document, 7, "press Add choice twice and type the codes and labels Y Yes and N No", async () =>
        {
            const string Choices = "#form-section-0-question-1";

            await Page.Locator($"{Choices}-add-option").ClickAsync();
            await Page.Locator($"{Choices}-option-0-code").FillAsync("Y");
            await Page.Locator($"{Choices}-option-0-label").FillAsync("Yes");

            await Page.Locator($"{Choices}-add-option").ClickAsync();
            await Page.Locator($"{Choices}-option-1-code").FillAsync("N");
            await Page.Locator($"{Choices}-option-1-label").FillAsync("No");
        });

        // 8. Save: back on the list, the form's row.
        await Step(Document, 8, "press Save and read the form's row on the Forms screen", async () =>
        {
            await Page.Locator("#form-save").ClickAsync();

            await Expect(Page.Locator("#forms-heading")).ToHaveTextAsync("Forms");
            await Expect(Page.Locator("#forms-table [data-form]").Filter(new() { HasText = FormName }).Locator("td"))
                .ToHaveTextAsync([FormName, "Lyme disease", "Yes"]);
        });

        // 9. New CMR, Zimmerman, one person found, Create CMR.
        await Step(Document, 9, "press New CMR, search for Zimmerman, read the one row and press Create CMR", async () =>
        {
            await Page.Locator("#nav-new-report").ClickAsync();
            await Page.Locator("#person-search-last-name").FillAsync("Zimmerman");
            await Page.Locator("#person-search-search").ClickAsync();

            await Expect(Page.Locator("#person-search-total"))
                .ToHaveTextAsync("1 person(s) found, showing 1.");
            await Expect(Page.Locator("#person-search-results [data-person-name]"))
                .ToHaveTextAsync(["Zimmerman, Paul"]);
            await Page.Locator("#person-search-results [data-new-report-for]").ClickAsync();
        });

        // 10. Step 2 of 2; Lyme disease and Summit County.
        await Step(Document, 10, "read the Step 2 of 2 line and choose Lyme disease and Summit County", async () =>
        {
            await Expect(Page.Locator("#new-report-details-person"))
                .ToContainTextAsync("Step 2 of 2. Reporting on Zimmerman, Paul");

            await Choose(Page.Locator("body"), "Disease", "Lyme disease");
            await Choose(Page.Locator("body"), "Investigating agency", "Summit County");
        });

        // 11. Save & Continue; note the address.
        var reportAddress = string.Empty;

        await Step(Document, 11, "press Save & Continue, read the header and note the address", async () =>
        {
            await Page.Locator("#report-save").ClickAsync();

            await Expect(Page.Locator("#report-header-patient")).ToHaveTextAsync("Zimmerman, Paul");
            await Expect(Page.Locator("#report-header-disease")).ToHaveTextAsync("Lyme disease");
            await Expect(Page).ToHaveURLAsync(ReportAddress);

            reportAddress = Page.Url;
        });

        // 12. The Forms tab: the one assigned form.
        await Step(Document, 12, "press Edit, then the Forms tab, and read the one assigned form", async () =>
        {
            await Page.Locator("#report-edit").ClickAsync();
            await Expect(Page.Locator("#report-done")).ToBeVisibleAsync();
            await OpenFormsTab();
        });

        // 13. Open: the form, its section and its two empty questions.
        await Step(Document, 13, "press Open and read the form's section and its two empty questions", async () =>
        {
            await Page.Locator("#forms-tab-table [data-assignment] button").ClickAsync();

            var form = Page.Locator("#forms-tab-form");
            await Expect(form).ToContainTextAsync(FormName);
            await Expect(form).ToContainTextAsync("Tick exposure");
            await Expect(TextAnswer()).ToHaveValueAsync("");
            await Expect(CodedAnswer()).ToHaveTextAsync("");
            await Expect(Page.Locator("#forms-tab-save")).ToHaveTextAsync("Save answers");
        });

        // 14. The two answers.
        await Step(Document, 14, "type the text answer and choose Yes for the coded question", async () =>
        {
            await TextAnswer().FillAsync("Hiking near Park City");
            await Choose(Page.Locator("#forms-tab-form"), CodedQuestion, "Yes");
        });

        // 15. Save answers: the saved message.
        await Step(Document, 15, "press Save answers and read the saved message", async () =>
        {
            await Page.Locator("#forms-tab-save").ClickAsync();

            await Expect(Page.Locator("#forms-tab-saved")).ToHaveTextAsync("The answers have been saved.");
            await Expect(Page.Locator("#forms-tab-error")).ToHaveCountAsync(0);
        });

        // 16. The report's address again, read afresh from the database.
        await Step(Document, 16, "open the report's address again, open the form on the Forms tab and read both answers", async () =>
        {
            await Open(reportAddress);
            await OpenFormsTab();
            await Page.Locator("#forms-tab-table [data-assignment] button").ClickAsync();

            await Expect(TextAnswer()).ToHaveValueAsync("Hiking near Park City");
            await Expect(CodedAnswer()).ToHaveTextAsync("Yes");
        });
    }

    /// <summary>
    /// Presses the Forms tab and reads its one assigned form and the Open
    /// button on it: steps 12 and 16 both begin this way.
    /// </summary>
    private async Task OpenFormsTab()
    {
        await Page.Locator("#report-tab-forms").ClickAsync();

        await Expect(Page.Locator("#forms-tab-list")).ToContainTextAsync("Assigned forms");
        await Expect(Page.Locator("#forms-tab-table [data-assignment-name]")).ToHaveTextAsync([FormName]);
        await Expect(Page.Locator("#forms-tab-table [data-assignment] button")).ToHaveTextAsync("Open");
    }

    /// <summary>The builder's Answer type list for the question at <paramref name="index"/>.</summary>
    private ILocator AnswerType(int index) =>
        Page.Locator($"[data-question=\"0-{index}\"]")
            .GetByRole(AriaRole.Combobox, new() { Name = "Answer type", Exact = true });

    /// <summary>The open form's box for the text question, by its label.</summary>
    private ILocator TextAnswer() =>
        Page.Locator("#forms-tab-form").GetByRole(AriaRole.Textbox, new() { Name = TextQuestion, Exact = true });

    /// <summary>The open form's list for the coded question, by its label.</summary>
    private ILocator CodedAnswer() =>
        Page.Locator("#forms-tab-form").GetByRole(AriaRole.Combobox, new() { Name = CodedQuestion, Exact = true });

    /// <summary>
    /// Opens the list labelled <paramref name="label"/> inside
    /// <paramref name="scope"/>, chooses the item reading
    /// <paramref name="item"/> by the words on it, and reads that box, which
    /// now shows what was chosen — the same way the creation walkthrough's
    /// test does it. The scope is what tells the form builder's
    /// <strong>Condition</strong> from nothing else, and keeps the Forms tab's
    /// question to the open form.
    /// </summary>
    private async Task Choose(ILocator scope, string label, string item)
    {
        var box = scope.GetByRole(AriaRole.Combobox, new() { Name = label, Exact = true });

        await box.ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = item, Exact = true }).ClickAsync();
        await Expect(box).ToHaveTextAsync(item);
    }
}
