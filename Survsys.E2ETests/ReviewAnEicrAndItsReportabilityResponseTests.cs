using System.Diagnostics;
using Microsoft.Playwright;
using Xunit;

namespace Survsys.E2ETests;

/// <summary>
/// The walkthrough <c>docs/walkthroughs/review-an-eicr-and-its-reportability-response.md</c>,
/// performed.
/// </summary>
/// <remarks>
/// One document, one test, a name that matches: the walkthrough contract's
/// second clause, and the reason this file is named after the document.
///
/// It runs in <see cref="IntakeCollection"/>, whose API tier polls the eCR
/// test feed's drop directory and keeps the RR conditions of the agency the
/// fixture RRs name, and whose seed maps Malaria's code. It drops the eICR
/// and its RR as the document's reader does, by copying the files into the
/// drop directory, and waits within a bound for the intake to take them.
/// </remarks>
[Collection(IntakeCollection.Name)]
public class ReviewAnEicrAndItsReportabilityResponseTests(IntakeHarness harness) : IntakePageTest
{
    /// <summary>
    /// The document this test performs, put into every failure by
    /// <see cref="SurvsysPageTest.Step"/>.
    /// </summary>
    private const string Document = "docs/walkthroughs/review-an-eicr-and-its-reportability-response.md";

    private const string EicrFile = "ecr-review-eicr.xml";

    private const string RrFile = "ecr-review-rr.xml";

    /// <summary>The eICR's id as the queue shows it: its root, a caret and its extension.</summary>
    private const string EicrId = "2.16.840.1.113883.9.9.9.9.9^e2e-review-eicr";

    private const string RrId = "2.16.840.1.113883.9.9.9.9.8^e2e-review-rr";

    /// <summary>
    /// How long the intake is given: many times its interval, and bounded,
    /// because a test never waits without a timeout.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task ReviewAnEicrAndItsReportabilityResponse()
    {
        // 1. The two documents, copied into the drop directory and taken.
        await Step(Document, 1, "copy the eICR and its RR into the eCR drop directory and see them taken", async () =>
        {
            foreach (var file in new[] { EicrFile, RrFile })
            {
                await Drop(file);
            }
        });

        // 2. The eCR queue.
        await Step(Document, 2, "press eCR and read the queue's heading", async () =>
        {
            await Open(harness.WebBaseAddress);
            await Page.Locator("#nav-ecr").ClickAsync();
            await Expect(Page.Locator("#ecr-queue-heading")).ToHaveTextAsync("eCR documents");
        });

        // 3. The two rows.
        await Step(Document, 3, "read the eICR's row and the RR's row", async () =>
        {
            var eicr = Row(EicrId);
            await Expect(eicr.Locator("td").Nth(0)).ToHaveTextAsync("eICR");
            await Expect(eicr.Locator("td").Nth(2)).ToHaveTextAsync("1");
            await Expect(eicr.Locator("td").Nth(3)).ToHaveTextAsync("test");

            var rr = Row(RrId);
            await Expect(rr.Locator("td").Nth(0)).ToHaveTextAsync("RR");
            await Expect(rr.Locator("td").Nth(3)).ToHaveTextAsync("test");
            await Expect(rr.Locator("[data-ecr-review]")).ToHaveTextAsync("0");
        });

        // 4. The eICR's own screen.
        await Step(Document, 4, "press the eICR's id and read the heading", async () =>
        {
            await Row(EicrId).GetByRole(AriaRole.Link).ClickAsync();
            await Expect(Page).ToHaveTitleAsync("eCR document — Survsys");
            await Expect(Page.Locator("#ecr-document-heading")).ToHaveTextAsync($"eICR {EicrId}");
        });

        // 5. The eICR, rendered.
        await Step(Document, 5, "read the rendered eICR's title and patient", async () =>
        {
            var rendered = Page.FrameLocator("#ecr-document-eicr");
            await Expect(rendered.Locator("h1")).ToContainTextAsync("Initial Public Health Case Report");

            var patient = rendered.GetByRole(AriaRole.Row).Filter(new() { HasText = "Gender:" });
            await Expect(patient).ToContainTextAsync("Ingrid SOLBERG");
            await Expect(patient).ToContainTextAsync("June 7, 1985");
        });

        // 6. The RR's one condition.
        await Step(Document, 6, "read the reportability response's condition", async () =>
        {
            var conditions = Page.Locator("#ecr-document-conditions tbody tr");
            await Expect(conditions).ToHaveCountAsync(1);

            var cells = conditions.Locator("td");
            await Expect(cells.Nth(0)).ToHaveTextAsync("Malaria (61462000)");
            await Expect(cells.Nth(1)).ToContainTextAsync("Reportable (RRVS1)");
            await Expect(cells.Nth(2)).ToHaveTextAsync(
                "Utah Department of Health (2.16.840.1.113883.4.6^8000000001)");
            await Expect(cells.Nth(3)).ToHaveTextAsync("24 h");
            await Expect(cells.Nth(4)).ToHaveTextAsync("Malaria");
            await Expect(cells.Nth(5)).ToHaveTextAsync("Reportable");
        });

        // 7. The validation findings.
        await Step(Document, 7, "read the validation findings", async () =>
        {
            var findings = Page.Locator("#ecr-document-findings tbody tr");
            await Expect(findings.Locator($"xpath=self::*[@data-ecr-finding-document='{EicrId}']").First)
                .ToBeVisibleAsync();

            var agencyAddress = findings
                .Locator($"xpath=self::*[@data-ecr-finding-document='{RrId}']")
                .Filter(new() { HasText = "(CONF:3315-413)" });
            await Expect(agencyAddress).ToHaveCountAsync(1);

            var cells = agencyAddress.Locator("td");
            await Expect(cells.Nth(0)).ToHaveTextAsync("error");
            await Expect(cells.Nth(1)).ToHaveTextAsync("schematron");
            await Expect(cells.Nth(2)).ToHaveTextAsync($"RR {RrId}");
            await Expect(cells.Nth(3)).ToHaveTextAsync(
                "This participantRole SHALL contain at least one [1..*] addr (CONF:3315-413).");
        });
    }

    /// <summary>The queue's row for the document with this id.</summary>
    private ILocator Row(string documentId) =>
        Page.Locator("#ecr-queue-table tbody tr").Filter(new() { HasText = documentId });

    /// <summary>
    /// Copies one fixture into the eCR drop directory and waits for the intake
    /// to move it into the directory's <c>.processed</c> folder.
    /// </summary>
    private async Task Drop(string file)
    {
        var dropped = Path.Combine(harness.EcrDirectory, file);
        File.Copy(IntakeHarness.Document(file), dropped, overwrite: true);

        var waited = Stopwatch.StartNew();
        while (File.Exists(dropped))
        {
            Assert.True(
                waited.Elapsed < Patience,
                $"{file} was still in the eCR drop directory after {Patience.TotalSeconds:0} seconds: "
                + "the intake never read it.");
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        Assert.True(
            File.Exists(Path.Combine(harness.EcrDirectory, ".processed", file)),
            $"{file} left the eCR drop directory but is not in its .processed folder, where the "
            + "document says the intake moves a document it has stored.");
    }
}
