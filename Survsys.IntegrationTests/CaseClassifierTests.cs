using Microsoft.EntityFrameworkCore;
using Survsys.Api;
using Survsys.Data;
using Xunit;

namespace Survsys.IntegrationTests;

/// <summary>
/// The classification rules run over a case as stored: that the classifier
/// reads the case's labs, address and workflow from the database, applies what
/// the rules decide, and leaves its findings as an administrative note.
/// </summary>
/// <remarks>
/// Each test makes its own person, so the cases and labs it writes are its own
/// to clear; the organism is made here too, under a SNOMED code no seed uses,
/// and so is the chlamydia condition with Culture approved for it, because the
/// fixture's conditions do not include it.
/// </remarks>
[Collection(SqlServerCollection.Name)]
public class CaseClassifierTests(SqlServerFixture server)
{
    private const string Chlamydia = "Chlamydia trachomatis infection (session 96 test)";
    private const string TestSnomed = "TEST-S96-CT";

    [Fact]
    public async Task ClassifyAsyncConfirmsAChlamydiaCaseWithAPositiveLab()
    {
        var (personId, reportId) = await Open(Chlamydia, "CT");

        try
        {
            await AddPositiveCulture(personId, reportId);

            await Classify(reportId);

            await using var context = server.NewContext();
            var report = await context.PersonConditions.SingleAsync(candidate => candidate.Id == reportId);
            Assert.Equal(CaseStatusRules.ConfirmedCode, report.CaseStatus);

            var note = Assert.Single(await context.Notes.Where(candidate => candidate.PersonConditionId == reportId).ToListAsync());
            Assert.Equal(NoteEndpoints.AdministrativeNoteType, note.NoteType);
            Assert.StartsWith(CaseClassifier.NotePrefix, note.Text, StringComparison.Ordinal);
            Assert.Contains("Chlamydia", note.Text, StringComparison.Ordinal);

            await Classify(reportId);
            Assert.Single(await context.Notes.Where(candidate => candidate.PersonConditionId == reportId).ToListAsync());
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task ClassifyAsyncReadsAnOrganismEnteredFromTheOrganismCodeList()
    {
        var (personId, reportId) = await Open(Chlamydia, "CT");

        try
        {
            await using (var context = server.NewContext())
            {
                var organism = await context.CodeValues
                    .Where(row => row.List == "organism" && row.Code == "Chlamydia trachomatis")
                    .Select(row => row.Id)
                    .SingleAsync();
                var lab = context.Labs.Add(new Lab { PersonId = personId, PersonConditionId = reportId }).Entity;
                await context.SaveChangesAsync();

                var test = context.LabTests.Add(new LabTest { LabId = lab.Id, TestTypeId = await CultureId(context) }).Entity;
                await context.SaveChangesAsync();

                context.LabTestResults.Add(new LabTestResult { LabTestId = test.Id, OrganismId = organism, TestResult = "POSITIVE" });
                await context.SaveChangesAsync();
            }

            await Classify(reportId);

            await using var read = server.NewContext();
            var report = await read.PersonConditions.SingleAsync(candidate => candidate.Id == reportId);
            Assert.Equal(CaseStatusRules.ConfirmedCode, report.CaseStatus);
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task ClassifyAsyncStrikesTheNoteOfAFindingThatNoLongerHolds()
    {
        var (personId, reportId) = await Open(Chlamydia, "CT");

        try
        {
            await Classify(reportId);
            await Classify(reportId);

            await using var context = server.NewContext();
            var noLab = Assert.Single(await context.Notes.Where(candidate => candidate.PersonConditionId == reportId).ToListAsync());
            Assert.Equal(CaseClassifier.NotePrefix + ClassificationRules.NoValidLab, noLab.Text);
            Assert.False(noLab.Struckthrough);

            await AddPositiveCulture(personId, reportId);
            await Classify(reportId);

            var notes = await context.Notes.AsNoTracking()
                .Where(candidate => candidate.PersonConditionId == reportId)
                .ToListAsync();
            Assert.Equal(2, notes.Count);
            Assert.True(notes.Single(candidate => candidate.Id == noLab.Id).Struckthrough);
            var confirmed = notes.Single(candidate => candidate.Id != noLab.Id);
            Assert.False(confirmed.Struckthrough);
            Assert.Contains("Chlamydia", confirmed.Text, StringComparison.Ordinal);
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task ClassifyAsyncMakesAnOutOfStatePatientsCaseOutOfState()
    {
        var (personId, reportId) = await Open(Chlamydia, "NY");

        try
        {
            await Classify(reportId);

            await using var context = server.NewContext();
            var report = await context.PersonConditions.SingleAsync(candidate => candidate.Id == reportId);
            Assert.Equal(CaseStatusRules.OutOfStateCode, report.CaseStatus);
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task ClassifyAsyncClosesAnAcceptedChlamydiaContact()
    {
        var (personId, reportId) = await Open(Chlamydia, "CT", report =>
        {
            report.EventType = EventTypes.Contact;
            report.WorkflowState = WorkflowRules.AcceptedCode;
        });

        try
        {
            await Classify(reportId);

            await using var context = server.NewContext();
            var report = await context.PersonConditions.SingleAsync(candidate => candidate.Id == reportId);
            Assert.Equal(WorkflowRules.ClosedCode, report.WorkflowState);
        }
        finally
        {
            await Clear(personId);
        }
    }

    [Fact]
    public async Task ClassifyAsyncLeavesAStatusAPersonSet()
    {
        var (personId, reportId) = await Open(Chlamydia, "CT",
            report => report.CaseStatus = CaseStatusRules.NotACaseCode);

        try
        {
            await AddPositiveCulture(personId, reportId);

            await Classify(reportId);

            await using var context = server.NewContext();
            var report = await context.PersonConditions.SingleAsync(candidate => candidate.Id == reportId);
            Assert.Equal(CaseStatusRules.NotACaseCode, report.CaseStatus);
        }
        finally
        {
            await Clear(personId);
        }
    }

    private async Task Classify(int reportId)
    {
        await using var context = server.NewContext();
        await new CaseClassifier(context).ClassifyAsync(reportId, CancellationToken.None);
    }

    /// <summary>Opens a case for a new person whose current address is in that state.</summary>
    private async Task<(int PersonId, int ReportId)> Open(
        string condition, string state, Action<PersonCondition>? shape = null)
    {
        await using var context = server.NewContext();

        var person = new Person { LastName = "Classifier", FirstName = "Session96" };
        context.Persons.Add(person);
        await context.SaveChangesAsync();

        context.PersonAddresses.Add(new PersonAddress { PersonId = person.Id, State = state });

        var conditionRow = await context.Conditions.SingleOrDefaultAsync(row => row.Name == condition)
            ?? context.Conditions.Add(new Condition { Name = condition, Active = true }).Entity;
        await context.SaveChangesAsync();

        var culture = await CultureId(context);
        if (!await context.CodeValueConditions.AnyAsync(row => row.CodeValueId == culture && row.ConditionId == conditionRow.Id))
        {
            context.CodeValueConditions.Add(new CodeValueCondition { CodeValueId = culture, ConditionId = conditionRow.Id });
        }

        var report = new PersonCondition
        {
            PersonId = person.Id,
            ConditionId = conditionRow.Id,
            RecordNumber = $"9696{person.Id:D6}",
            AgencyId = await context.Agencies.Select(agency => agency.Id).FirstAsync(),
            CreatedAt = DateTimeOffset.UtcNow,
            WorkflowState = WorkflowRules.InitialCode,
        };
        shape?.Invoke(report);
        context.PersonConditions.Add(report);
        await context.SaveChangesAsync();

        return (person.Id, report.Id);
    }

    /// <summary>Files a positive <i>Chlamydia trachomatis</i> culture against the case.</summary>
    private async Task AddPositiveCulture(int personId, int reportId)
    {
        await using var context = server.NewContext();

        var organism = await context.VocabMasterOrganisms.SingleOrDefaultAsync(row => row.Snomed == TestSnomed)
            ?? context.VocabMasterOrganisms.Add(
                new VocabMasterOrganism { Snomed = TestSnomed, OrganismName = "Chlamydia trachomatis" }).Entity;
        var lab = context.Labs.Add(new Lab { PersonId = personId, PersonConditionId = reportId }).Entity;
        await context.SaveChangesAsync();

        var test = context.LabTests.Add(new LabTest
        {
            LabId = lab.Id,
            TestTypeId = await CultureId(context),
        }).Entity;
        await context.SaveChangesAsync();

        context.LabTestResults.Add(new LabTestResult { LabTestId = test.Id, OrganismId = organism.Id, TestResult = "POSITIVE" });
        await context.SaveChangesAsync();
    }

    private static Task<int> CultureId(SurvsysDbContext context) =>
        context.CodeValues
            .Where(row => row.List == "test_type" && row.Code == "Culture")
            .Select(row => row.Id)
            .SingleAsync();

    private async Task Clear(int personId)
    {
        await using var context = server.NewContext();

        var labs = context.Labs.Where(lab => lab.PersonId == personId).Select(lab => lab.Id);
        var tests = context.LabTests.Where(test => labs.Contains(test.LabId)).Select(test => test.Id);
        await context.LabTestResults.Where(result => tests.Contains(result.LabTestId)).ExecuteDeleteAsync();
        await context.LabTests.Where(test => labs.Contains(test.LabId)).ExecuteDeleteAsync();
        await context.Labs.Where(lab => lab.PersonId == personId).ExecuteDeleteAsync();
        await context.VocabMasterOrganisms.Where(row => row.Snomed == TestSnomed).ExecuteDeleteAsync();

        var reports = context.PersonConditions.Where(report => report.PersonId == personId).Select(report => report.Id);
        await context.Notes.Where(note => note.PersonConditionId != null && reports.Contains(note.PersonConditionId.Value))
            .ExecuteDeleteAsync();
        await context.PersonConditions.Where(report => report.PersonId == personId).ExecuteDeleteAsync();
        await context.PersonAddresses.Where(address => address.PersonId == personId).ExecuteDeleteAsync();
        await context.Persons.Where(person => person.Id == personId).ExecuteDeleteAsync();

        var conditions = context.Conditions.Where(row => row.Name == Chlamydia).Select(row => row.Id);
        await context.CodeValueConditions.Where(row => conditions.Contains(row.ConditionId)).ExecuteDeleteAsync();
        await context.Conditions.Where(row => row.Name == Chlamydia).ExecuteDeleteAsync();
    }
}
