using Microsoft.EntityFrameworkCore;
using Survsys.Contracts;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// Runs <see cref="ClassificationRules"/> over a saved case: reads what the
/// rules read, applies what they decide, and records what they found as an
/// administrative note on the case, which is how the user sees it.
/// </summary>
/// <remarks>
/// Called after the save that changed the case, by every route that saves one
/// except the status route — a status a person sets is theirs — so the rules
/// read the labs and address as stored. It saves what it changes itself. Each
/// finding is its own note, its text starting <see cref="NotePrefix"/>; a
/// finding is noted only where no unstruck note on the case already records
/// it, so saving a case twice does not repeat it, and a classification note
/// whose finding no longer holds is struck. A contact the rules close gets a
/// workflow history row where the save has a signed-in user to name. What it
/// decided is left on <see cref="SurvsysDbContext.Classified"/>, which
/// <see cref="CaseClassificationHeader"/> answers the request with.
/// </remarks>
public sealed class CaseClassifier(SurvsysDbContext database)
{
    /// <summary>How the text of a note the classifier writes starts, which is what marks it as one.</summary>
    public const string NotePrefix = "Classification: ";

    /// <summary>Classifies the case with that id; a case not on file is left alone.</summary>
    public async Task ClassifyAsync(int reportId, CancellationToken cancellationToken)
    {
        var report = await database.PersonConditions
            .SingleOrDefaultAsync(candidate => candidate.Id == reportId, cancellationToken);

        if (report is null)
        {
            return;
        }

        var condition = await database.Conditions
            .Where(candidate => candidate.Id == report.ConditionId)
            .Select(candidate => candidate.Name)
            .SingleAsync(cancellationToken);

        var address = await database.PersonAddresses
            .Where(candidate => candidate.PersonId == report.PersonId && candidate.Ending == null)
            .OrderByDescending(candidate => candidate.Beginning)
            .ThenByDescending(candidate => candidate.Id)
            .Select(candidate => new { candidate.State, candidate.PostalCode })
            .FirstOrDefaultAsync(cancellationToken);

        var zip = ZipAgencyRules.Normalize(address?.PostalCode);
        var mapping = zip is null
            ? []
            : await database.ZipCodeAgencies.Where(row => row.ZipCode == zip).ToListAsync(cancellationToken);

        var labs = await (
            from lab in database.Labs
            join test in database.LabTests on lab.Id equals test.LabId
            join result in database.LabTestResults on test.Id equals result.LabTestId
            where lab.PersonConditionId == reportId
            select new ClassificationLab(
                // A message's result names a vocabulary organism; one entered
                // on the Laboratory tab names the organism code list's entry.
                database.VocabMasterOrganisms
                    .Where(organism => organism.Id == result.OrganismId)
                    .Select(organism => organism.OrganismName)
                    .FirstOrDefault()
                ?? database.CodeValues
                    .Where(organism => organism.List == CodeLists.Organism && organism.Id == result.OrganismId)
                    .Select(organism => organism.Code)
                    .FirstOrDefault(),
                database.CodeValues
                    .Where(type => type.Id == test.TestTypeId)
                    .Select(type => type.Code)
                    .FirstOrDefault(),
                result.TestResult,
                database.CodeValueConditions.Any(approved =>
                    approved.CodeValueId == test.TestTypeId && approved.ConditionId == report.ConditionId)))
            .ToListAsync(cancellationToken);

        var outcome = ClassificationRules.Classify(new ClassificationInput(
            condition,
            report.EventType,
            report.WorkflowState,
            report.CaseStatus,
            report.AgencyId,
            address?.State,
            ZipAgencyRules.Resolve(zip, mapping).AgencyId,
            labs,
            FacilityEncounter: await database.Encounters.AnyAsync(
                encounter => encounter.PersonConditionId == reportId && encounter.FacilityId != null, cancellationToken),
            Symptomatic: report.DiseaseOnsetDate is not null,
            EpiLink: await database.ExposureContacts.AnyAsync(
                contact => contact.PersonConditionId == reportId, cancellationToken)));

        report.CaseStatus = outcome.CaseStatus;
        report.AgencyId = outcome.AgencyId;

        if (outcome.CloseContact)
        {
            report.WorkflowState = WorkflowRules.ClosedCode;

            if (database.AuditUserId is { } userId)
            {
                database.WorkflowHistory.Add(new WorkflowHistoryEntry
                {
                    PersonConditionId = reportId,
                    UserId = userId,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Transition = WorkflowRules.CloseCode,
                    FromState = WorkflowRules.AcceptedCode,
                    ToState = WorkflowRules.ClosedCode,
                    Note = "Closed by the classification rules.",
                });
            }
        }

        var findings = outcome.Findings.Select(finding => NotePrefix + finding).ToList();
        var noted = await database.Notes
            .Where(note => note.PersonConditionId == reportId
                && !note.Struckthrough
                && note.NoteType == NoteEndpoints.AdministrativeNoteType
                && note.Text!.StartsWith(NotePrefix))
            .ToListAsync(cancellationToken);

        foreach (var stale in noted.Where(note => !findings.Contains(note.Text!)))
        {
            stale.Struckthrough = true;
        }

        foreach (var text in findings.Except(noted.Select(note => note.Text!)))
        {
            database.Notes.Add(new Note
            {
                PersonConditionId = reportId,
                NoteType = NoteEndpoints.AdministrativeNoteType,
                Text = text,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
        database.Classified = (reportId, outcome);
    }
}
