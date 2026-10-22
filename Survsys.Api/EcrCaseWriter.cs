using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>One person the matcher found for an eICR's patient, with its verdict.</summary>
public sealed record EcrMatchCandidate(int PersonId, string Verdict);

/// <summary>
/// Whom an eICR's patient is: the person on file, a new person, or a
/// reviewer's to say, with why.
/// </summary>
public sealed record EcrMatchDecision(int? PersonId, bool NewPerson, string? Review);

/// <summary>
/// What linking an eICR came to: the cases it was linked to, or why it was
/// refused.
/// </summary>
public sealed record EcrLinkOutcome(IReadOnlyList<int> CaseIds, string? Refusal);

/// <summary>
/// What writing an eICR to a case reads from configuration: which county
/// value its address is stored as, and the senders (custodian ids) whose
/// results are written as labs.
/// </summary>
public sealed record EcrCaseSettings(CountyPrograms Counties, IReadOnlyCollection<string> LabSenders);

/// <summary>
/// Turns a reportable eICR into cases: finds whom its patient is, links the
/// person's open case on each reportable condition or opens one, and writes
/// the eICR onto each case, recording every value and row it writes.
/// </summary>
/// <remarks>
/// An eICR is reportable once an RR that responds to it holds a condition
/// determined reportable whose code maps to a catalogue condition, which is
/// <see cref="EcrReportability.ReportableRouting"/>. A later version of a set
/// already linked goes to the cases its earlier version went to; otherwise
/// the patient is matched by phase 9's matcher, and a match that is not
/// confident is left to a reviewer, who settles it through
/// <see cref="Link"/>. Each link is one transaction, joined where the caller
/// already holds one.
///
/// What a version writes is recorded as <see cref="EcrCaseValue"/> rows under
/// its <see cref="EcrCaseLink"/>, so each value carries the eICR id and
/// version it came from. A value on a row the eICR did not create — the
/// case's onset date, pregnant answer and occupation — is written where it is
/// blank or still reads as the previous version wrote it. A row the eICR
/// creates — the person, an address, an encounter, a problem's note, a
/// treatment, a travel exposure, a lab — is keyed by what it records, so a
/// later version updates its counterpart where no user has changed it, adds
/// what is new, and removes the encounters, notes and treatments of the
/// previous version it no longer carries that no user has changed. Addresses
/// and exposures are not removed, an address being the person's history and
/// an exposure the head of its contacts, and a lab is added once by its key
/// and never updated.
/// </remarks>
public static class EcrCaseWriter
{
    /// <summary>The note type a problem is written as: the notes vocabulary's clinical note.</summary>
    public const string ProblemNoteType = NoteEndpoints.ClinicalNoteType;

    private const string Persons = "persons";
    private const string Cases = "person_conditions";
    private const string Addresses = "person_addresses";
    private const string Encounters = "encounters";
    private const string Notes = "notes";
    private const string Treatments = "person_treatments";
    private const string Exposures = "exposures";
    private const string Labs = "labs";

    private const int KeyLength = 500;

    private const string PregnantCode = "77386006";
    private const string NotPregnantCode = "60001007";

    /// <summary>
    /// Whom the patient is from the matcher's candidates: the one exact
    /// candidate where there is no probable one, a new person where there is
    /// no candidate at all, and a reviewer's to say otherwise.
    /// </summary>
    public static EcrMatchDecision Decide(IReadOnlyList<EcrMatchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var found = candidates.Where(candidate => candidate.Verdict != PersonMatchCandidateResponse.None).ToList();
        var exact = found.Where(candidate => candidate.Verdict == PersonMatchCandidateResponse.Exact).ToList();

        if (found.Count == 0)
        {
            return new EcrMatchDecision(null, true, null);
        }

        if (exact.Count == 1 && found.Count == 1)
        {
            return new EcrMatchDecision(exact[0].PersonId, false, null);
        }

        return new EcrMatchDecision(null, false, exact.Count > 1
            ? $"{exact.Count} people on file are exact matches for the eICR's patient."
            : exact.Count == 1
                ? "One person on file is an exact match for the eICR's patient, and others may be."
                : $"{found.Count} people on file may be the eICR's patient, none of them an exact match.");
    }

    /// <summary>
    /// A value or row as it is recorded and compared: its fields in order,
    /// joined by a bar, a date as <c>yyyy-MM-dd</c>, a number invariantly and
    /// a null as nothing.
    /// </summary>
    public static string Text(params object?[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return string.Join('|', fields.Select(field => field switch
        {
            null => "",
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => field.ToString(),
        }));
    }

    /// <summary>
    /// Whether an eICR may write over a value that reads
    /// <paramref name="current"/>: where it is blank, or where it still reads
    /// as <paramref name="written"/>, what the previous version wrote. Anything
    /// else a user has edited.
    /// </summary>
    public static bool MayWrite(string? current, string? written) =>
        current is null || (written is not null && current == written);

    /// <summary>
    /// Writes the eICR with this id to its cases if it is reportable and not
    /// yet decided, or holds it for a reviewer; does nothing for an RR, an eICR
    /// already linked or held, or one not yet reportable.
    /// </summary>
    public static async Task Process(
        SurvsysDbContext database,
        CdaParser parser,
        PersonMatchThresholds thresholds,
        EcrCaseSettings settings,
        int eicrId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(settings);

        var eicr = await database.EcrDocuments.SingleOrDefaultAsync(
            document => document.Id == eicrId && document.Kind == EcrDocument.EicrKind, cancellationToken);
        if (eicr is null || eicr.CaseStatus is not null)
        {
            return;
        }

        var conditionIds = await ReportableConditions(database, eicr, cancellationToken);
        if (conditionIds.Count == 0)
        {
            return;
        }

        var document = parser.Read(eicr.RawXml);

        var earlier = await EarlierCases(database, eicr, cancellationToken);
        if (earlier.Count > 0)
        {
            await using var owned = await Join(database, cancellationToken);
            await WriteCases(database, eicr, document, earlier, false, settings, now, cancellationToken);
            await Commit(owned, cancellationToken);
            return;
        }

        var request = MatchRequest(document.Patient);
        var decision = PersonMatchEndpoints.WhyNotAMatchRequest(request) is { } why
            ? new EcrMatchDecision(null, false, $"The eICR's patient cannot be matched: {why}")
            : Decide((await new PersonMatchQueries(database, thresholds, parser).Match(request, cancellationToken))
                .Select(found => new EcrMatchCandidate(found.Person.Id, found.Verdict))
                .ToList());

        if (decision.Review is { } review)
        {
            eicr.CaseStatus = EcrDocument.ReviewCaseStatus;
            eicr.CaseReason = review;
            await database.SaveChangesAsync(cancellationToken);
            return;
        }

        await LinkCases(
            database, eicr, document, decision.PersonId, null, conditionIds, settings, now, cancellationToken);
    }

    /// <summary>
    /// Settles the eICR with this id as a reviewer decided: written to
    /// <paramref name="personId"/>'s cases, or to a new person's where it is
    /// null, linking <paramref name="personConditionId"/> where one is named.
    /// Answers null where no eICR has that id; refuses an eICR already linked
    /// or not reportable, a person not on file, and a case that is not an open
    /// case of that person on one of the eICR's reportable conditions.
    /// </summary>
    public static async Task<EcrLinkOutcome?> Link(
        SurvsysDbContext database,
        CdaParser parser,
        EcrCaseSettings settings,
        int eicrId,
        int? personId,
        int? personConditionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(settings);

        var eicr = await database.EcrDocuments.SingleOrDefaultAsync(
            document => document.Id == eicrId && document.Kind == EcrDocument.EicrKind, cancellationToken);
        if (eicr is null)
        {
            return null;
        }

        if (eicr.CaseStatus == EcrDocument.LinkedCaseStatus)
        {
            return Refuse("The eICR is already written to its cases.");
        }

        var conditionIds = await ReportableConditions(database, eicr, cancellationToken);
        if (conditionIds.Count == 0)
        {
            return Refuse("No RR on file holds a reportable condition for this eICR.");
        }

        if (personId is { } person
            && !await database.Persons.AnyAsync(
                found => found.Id == person && found.MergedIntoPersonId == null, cancellationToken))
        {
            return Refuse($"No person on file has id {person}.");
        }

        if (personConditionId is { } named
            && (personId is null
                || !await OpenCases(database, personId.Value)
                    .AnyAsync(report => report.Id == named && conditionIds.Contains(report.ConditionId),
                        cancellationToken)))
        {
            return Refuse($"Case {named} is not an open case of person {personId} on a reportable condition of this eICR.");
        }

        var caseIds = await LinkCases(
            database,
            eicr,
            parser.Read(eicr.RawXml),
            personId,
            personConditionId,
            conditionIds,
            settings,
            now,
            cancellationToken);

        return new EcrLinkOutcome(caseIds, null);
    }

    /// <summary>
    /// The catalogue conditions of the RRs that respond to the eICR: each
    /// condition kept as reportable whose code is mapped.
    /// </summary>
    public static async Task<IReadOnlyList<int>> ReportableConditions(
        SurvsysDbContext database, EcrDocument eicr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var rrIds = EcrIntakeService.ResponsesTo(database.EcrDocuments, eicr).Select(rr => rr.Id);

        return await (
                from condition in database.EcrRrConditions
                where rrIds.Contains(condition.EcrDocumentId)
                    && condition.DeterminationCode == EcrReportability.Reportable
                join mapping in database.EcrConditionMappings
                    on new { condition.CodeSystem, condition.Code }
                    equals new { CodeSystem = (string?)mapping.CodeSystem, Code = (string?)mapping.Code }
                where mapping.ConditionId != null
                select mapping.ConditionId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The person-match request for an eICR's patient, their address included.</summary>
    public static PersonMatchRequest MatchRequest(ParsedPatient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);

        return PersonMatchEndpoints.ToRequest(patient.Person) with
        {
            Street = patient.Street,
            PostalCode = patient.Zip,
        };
    }

    /// <summary>
    /// The cases the latest earlier version of the eICR's set was written to,
    /// or none where it is the first or no earlier version is linked.
    /// </summary>
    private static async Task<IReadOnlyList<int>> EarlierCases(
        SurvsysDbContext database, EcrDocument eicr, CancellationToken cancellationToken)
    {
        if (eicr.SetId is null)
        {
            return [];
        }

        var earlier = await EcrIntakeService.Versions(database.EcrDocuments, eicr.Feed, eicr.SetId)
            .Where(version => version.Id != eicr.Id
                && version.VersionNumber < eicr.VersionNumber
                && version.CaseLinks.Any())
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return earlier == 0
            ? []
            : await database.EcrCaseLinks
                .Where(link => link.EcrDocumentId == earlier)
                .Select(link => link.PersonConditionId)
                .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Links the eICR to the person's case on each condition — the named case
    /// on its condition, else their open case, else a new one — making the
    /// person from the eICR's demographics where none is given, and writes it
    /// to each.
    /// </summary>
    private static async Task<IReadOnlyList<int>> LinkCases(
        SurvsysDbContext database,
        EcrDocument eicr,
        CdaDocument document,
        int? personId,
        int? personConditionId,
        IReadOnlyList<int> conditionIds,
        EcrCaseSettings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var owned = await Join(database, cancellationToken);

        var patient = document.Patient;
        var person = personId ?? (await new PersonQueries(database).Add(patient.Person, cancellationToken)).Id;
        var cases = new PersonConditionQueries(database, new CaseNotificationQueue(database));
        var agencyId = await AgencyOf(database, patient.Zip, cancellationToken);
        var caseIds = new List<int>();

        foreach (var conditionId in conditionIds)
        {
            var open = await OpenCases(database, person)
                .Where(report => report.ConditionId == conditionId)
                .OrderByDescending(report => report.Id == personConditionId)
                .ThenByDescending(report => report.Id)
                .Select(report => (int?)report.Id)
                .FirstOrDefaultAsync(cancellationToken);

            caseIds.Add(open
                ?? (await cases.Add(
                    new CreatePersonConditionRequest(
                        person, conditionId, agencyId, DateOnly.FromDateTime(eicr.ReceivedAt)),
                    now,
                    cancellationToken)).Id);
        }

        await WriteCases(database, eicr, document, caseIds, personId is null, settings, now, cancellationToken);
        await Commit(owned, cancellationToken);

        return caseIds;
    }

    /// <summary>
    /// Links the eICR to each case, writes it onto each, and marks it linked.
    /// </summary>
    private static async Task WriteCases(
        SurvsysDbContext database,
        EcrDocument eicr,
        CdaDocument document,
        IReadOnlyList<int> caseIds,
        bool newPerson,
        EcrCaseSettings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var caseId in caseIds)
        {
            var link = new EcrCaseLink { EcrDocumentId = eicr.Id, PersonConditionId = caseId, CreatedAt = now };
            database.EcrCaseLinks.Add(link);

            var write = new CaseWrite(
                database, link, await PreviousValues(database, eicr, caseId, cancellationToken), cancellationToken);
            await write.All(document, caseId, newPerson, settings, now);
        }

        eicr.CaseStatus = EcrDocument.LinkedCaseStatus;
        eicr.CaseReason = null;
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// What the latest earlier version of the eICR's set wrote on the case, or
    /// nothing where no earlier version was written there.
    /// </summary>
    private static async Task<IReadOnlyList<EcrCaseValue>> PreviousValues(
        SurvsysDbContext database, EcrDocument eicr, int caseId, CancellationToken cancellationToken)
    {
        if (eicr.SetId is null)
        {
            return [];
        }

        var previous = await database.EcrCaseLinks
            .Where(link => link.PersonConditionId == caseId
                && link.EcrDocumentId != eicr.Id
                && link.EcrDocument!.Feed == eicr.Feed
                && link.EcrDocument.SetId == eicr.SetId
                && link.EcrDocument.VersionNumber < eicr.VersionNumber)
            .OrderByDescending(link => link.EcrDocument!.VersionNumber)
            .Select(link => link.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return previous == 0
            ? []
            : await database.EcrCaseValues
                .Where(value => value.EcrCaseLinkId == previous)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
    }

    /// <summary>A person's open, undeleted cases: those an eICR may be linked to.</summary>
    private static IQueryable<PersonCondition> OpenCases(SurvsysDbContext database, int personId) =>
        database.PersonConditions.Where(report => report.PersonId == personId
            && report.DeletedAt == null
            && report.WorkflowState != WorkflowRules.ClosedCode);

    /// <summary>The agency the patient's zip belongs to, where exactly one claims it.</summary>
    private static async Task<int?> AgencyOf(SurvsysDbContext database, string? zip, CancellationToken cancellationToken)
    {
        if (ZipAgencyRules.Normalize(zip) is not { } code)
        {
            return null;
        }

        var mapping = await database.ZipCodeAgencies.Where(row => row.ZipCode == code).ToListAsync(cancellationToken);
        var resolution = ZipAgencyRules.Resolve(code, mapping);

        return resolution.Outcome == ZipResolutionOutcome.Resolved ? resolution.AgencyId : null;
    }

    private static EcrLinkOutcome Refuse(string why) => new([], why);

    /// <summary>The text trimmed and cut to the column's length, or null where it is blank.</summary>
    private static string? Clip(string? text, int length)
    {
        var trimmed = text?.Trim();

        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > length ? trimmed[..length] : trimmed;
    }

    /// <summary>A transaction of this writer's own, or null where the caller already holds one.</summary>
    private static async Task<IDbContextTransaction?> Join(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        database.Database.CurrentTransaction is null
            ? await database.Database.BeginTransactionAsync(cancellationToken)
            : null;

    private static async Task Commit(IDbContextTransaction? owned, CancellationToken cancellationToken)
    {
        if (owned is not null)
        {
            await owned.CommitAsync(cancellationToken);
        }
    }

    /// <summary>
    /// One eICR version's writing onto one case: what the previous version
    /// wrote there, and the link this version records what it writes under.
    /// </summary>
    private sealed class CaseWrite(
        SurvsysDbContext database,
        EcrCaseLink link,
        IReadOnlyList<EcrCaseValue> previous,
        CancellationToken cancellationToken)
    {
        public async Task All(
            CdaDocument document, int caseId, bool newPerson, EcrCaseSettings settings, DateTimeOffset now)
        {
            var report = await database.PersonConditions.SingleAsync(found => found.Id == caseId, cancellationToken);
            var patient = document.Patient;

            await Person(report.PersonId, patient.Person, newPerson);
            await Address(report.PersonId, patient, settings.Counties, now);
            CaseValues(report, document);
            await Encounter(caseId, document.Encounter, now);
            await Problems(caseId, document.Problems, now);
            await Medications(caseId, document.Medications, now);
            await Travel(caseId, document.Travel);

            if (document.Custodian is { } custodian
                && settings.LabSenders.Contains(custodian, StringComparer.OrdinalIgnoreCase))
            {
                await LabsOf(caseId, patient.Orders);
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// The person's demographics: recorded where this eICR made the person,
        /// and updated where the previous version made them and no user has
        /// changed them since.
        /// </summary>
        private async Task Person(int personId, CreatePersonRequest demographics, bool newPerson)
        {
            const string key = "demographics";
            var person = await database.Persons.SingleAsync(found => found.Id == personId, cancellationToken);
            var prior = Prior(Persons, key);

            if (!newPerson)
            {
                if (prior is null || !MayWrite(TextOf(person), prior.Written))
                {
                    return;
                }

                var made = PersonEndpoints.ToPerson(demographics);
                person.LastName = made.LastName;
                person.FirstName = made.FirstName;
                person.MiddleName = made.MiddleName;
                person.BirthDate = made.BirthDate;
                person.BirthGender = made.BirthGender;
            }

            Record(Persons, person.Id, key, TextOf(person));

            static string TextOf(Data.Person person) => Text(
                person.LastName, person.FirstName, person.MiddleName, person.BirthDate, person.BirthGender);
        }

        /// <summary>
        /// The patient's address, added where the person has none with its
        /// street and zip, its county stored in the field the <c>ecr</c> setting
        /// names and the other filled from the town, as ELR stores one.
        /// </summary>
        private async Task Address(int personId, ParsedPatient patient, CountyPrograms counties, DateTimeOffset now)
        {
            var street = patient.Street?.Trim();
            var zip = patient.Zip?.Trim();
            if (string.IsNullOrEmpty(street) && string.IsNullOrEmpty(zip))
            {
                return;
            }

            var key = Text(street?.ToUpperInvariant(), zip);
            var prior = Prior(Addresses, key);
            var addresses = await database.PersonAddresses
                .Where(address => address.PersonId == personId)
                .ToListAsync(cancellationToken);

            PersonAddress address;
            if (prior is null)
            {
                if (addresses.Any(address =>
                    string.Equals(address.Street?.Trim() ?? "", street ?? "", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(address.PostalCode?.Trim() ?? "", zip ?? "", StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                var today = DateOnly.FromDateTime(now.Date);
                address = new PersonAddress { PersonId = personId, Street = street, PostalCode = zip, Beginning = today };
                await Fill(address, patient, counties);

                // The closing is saved before the insert, so the one-current-address
                // index never sees two addresses with no ending.
                AddressRules.CloseHistoric([.. addresses, address], today);
                await database.SaveChangesAsync(cancellationToken);
                database.PersonAddresses.Add(address);
                await database.SaveChangesAsync(cancellationToken);
            }
            else
            {
                address = addresses.FirstOrDefault(found => found.Id == prior.TargetId)!;
                if (address is null || !MayWrite(TextOf(address), prior.Written))
                {
                    return;
                }

                address.County = null;
                address.CountyEquivalent = null;
                await Fill(address, patient, counties);
            }

            Record(Addresses, address.Id, key, TextOf(address));

            static string TextOf(PersonAddress address) => Text(
                address.Street, address.City, address.State, address.PostalCode, address.County, address.CountyEquivalent);
        }

        private async Task Fill(PersonAddress address, ParsedPatient patient, CountyPrograms counties)
        {
            address.City = Clip(patient.City, 100);
            address.State = Clip(patient.State, 20);

            if (AddressRules.Text(patient.County) is { } carried)
            {
                var county = await database.CodeValues
                    .Where(value => value.List == CodeLists.County && (value.Code == carried || value.Fips == carried))
                    .OrderBy(value => value.Id)
                    .Select(value => value.Code)
                    .FirstOrDefaultAsync(cancellationToken) ?? carried;

                if (counties.UsesCountyEquivalent(CountyPrograms.EcrProgram))
                {
                    address.CountyEquivalent = county;
                }
                else
                {
                    address.County = county;
                }
            }

            var countiesByZip = address.County is null && ZipAgencyRules.Normalize(address.PostalCode) is { } zipCode
                ? await database.ZipCodes
                    .Where(row => row.Code == zipCode && row.County != null)
                    .ToDictionaryAsync(row => row.Code, row => row.County!, cancellationToken)
                : null;
            var towns = address.City is { } city
                ? await database.CtTowns.Where(town => town.Town == city).ToListAsync(cancellationToken)
                : null;
            AddressRules.FillConnecticutCounties(address, towns, countiesByZip);
        }

        /// <summary>The case's onset date, pregnant answer and occupation.</summary>
        private void CaseValues(PersonCondition report, CdaDocument document)
        {
            var onset = document.Problems
                .Where(problem => problem.IsTrigger && problem.Onset is not null)
                .Min(problem => problem.Onset);
            Value(
                report.Id,
                "disease_onset_date",
                report.DiseaseOnsetDate is null ? null : Text(report.DiseaseOnsetDate),
                onset is null ? null : Text(onset),
                () => report.DiseaseOnsetDate = onset);

            var pregnant = document.Pregnancy switch
            {
                { Status.Code: PregnantCode, Negated: false } => "Yes",
                { Status.Code: PregnantCode, Negated: true } => "No",
                { Status.Code: NotPregnantCode, Negated: false } => "No",
                _ => null,
            };
            Value(report.Id, "pregnant", report.Pregnant, pregnant, () => report.Pregnant = pregnant);

            var occupation = document.Occupations
                .Select(held => Clip(held.Occupation?.DisplayName ?? held.Text, 255))
                .FirstOrDefault(text => text is not null);
            Value(report.Id, "occupation", report.Occupation, occupation, () => report.Occupation = occupation);
        }

        private Task Encounter(int caseId, CdaEncounter? encounter, DateTimeOffset now)
        {
            List<(string, Action<Data.Encounter>)> wanted = encounter?.Start is { } start
                ?
                [
                    (Text("encounter", encounter.Id), row =>
                    {
                        row.EncounterDate = start;
                        row.EncounterType = Clip(encounter.Type?.DisplayName ?? encounter.Type?.Code, 50);
                        row.Description = Clip(encounter.FacilityName, 1_000);
                    }),
                ]
                : [];

            return Rows(
                Encounters,
                wanted,
                row => row.Id,
                row => Text(row.EncounterDate, row.EncounterType, row.FacilityId, row.Description),
                () => new Data.Encounter { PersonConditionId = caseId, CreatedAt = now },
                removes: true);
        }

        private Task Problems(int caseId, IReadOnlyList<CdaProblem> problems, DateTimeOffset now) =>
            Rows(
                Notes,
                problems
                    .Where(problem => problem.Value is not null)
                    .Select(problem => (Text("problem", problem.Value!.CodeSystem, problem.Value.Code, problem.Onset),
                        (Action<Note>)(row =>
                        {
                            row.Text = $"Problem reported by eICR: {problem.Value.DisplayName ?? problem.Value.Code}"
                                + (problem.Value.Code is null ? "" : $" ({problem.Value.Code})")
                                + (problem.Onset is null ? "" : $", onset {Text(problem.Onset)}")
                                + (problem.Resolved is null ? "" : $", resolved {Text(problem.Resolved)}")
                                + (problem.IsTrigger ? ", the trigger code" : "");
                            row.NoteType = ProblemNoteType;
                        })))
                    .ToList(),
                row => row.Id,
                row => Text(row.Text, row.NoteType, row.Struckthrough, row.Private),
                () => new Note { PersonConditionId = caseId, CreatedAt = now },
                removes: true);

        private async Task Medications(int caseId, IReadOnlyList<CdaMedication> medications, DateTimeOffset now)
        {
            var wanted = new List<(string, Action<PersonTreatment>)>();

            foreach (var medication in medications)
            {
                if (Clip(medication.Drug?.DisplayName ?? medication.Drug?.Code, 200) is not { } name)
                {
                    continue;
                }

                var treatmentId = await TreatmentNamed(name, now);
                wanted.Add((Text("medication", name, medication.Start), row =>
                {
                    row.TreatmentId = treatmentId;
                    row.TreatmentDate = medication.Start;
                    row.Dose = Clip(medication.Dose, 100);
                    row.Comment = medication.Route is null ? null : Clip($"Route: {medication.Route}", 1_000);
                }));
            }

            await Rows(
                Treatments,
                wanted,
                row => row.Id,
                row => Text(row.TreatmentId, row.TreatmentDate, row.StopTreatmentDate, row.Dose, row.Comment),
                () => new PersonTreatment { PersonConditionId = caseId, CreatedAt = now },
                removes: true);
        }

        /// <summary>The treatment of this name, added to the catalogue where there is none.</summary>
        private async Task<int> TreatmentNamed(string name, DateTimeOffset now)
        {
            var found = await database.Treatments
                .Where(treatment => treatment.Name == name)
                .OrderBy(treatment => treatment.Id)
                .Select(treatment => (int?)treatment.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (found is { } id)
            {
                return id;
            }

            var added = new Treatment { Name = name, CreatedAt = now };
            database.Treatments.Add(added);
            await database.SaveChangesAsync(cancellationToken);

            return added.Id;
        }

        private Task Travel(int caseId, IReadOnlyList<CdaTravel> travel) =>
            Rows(
                Exposures,
                travel
                    .Where(entry => (entry.Location ?? entry.Text) is not null)
                    .Select(entry => (Text("travel", entry.Location ?? entry.Text, entry.From),
                        (Action<Exposure>)(row =>
                        {
                            row.Name = entry.Location ?? entry.Text;
                            row.Description = entry.Text;
                            row.ExposureStart = entry.From;
                            row.ExposureEnd = entry.To;
                        })))
                    .ToList(),
                row => row.Id,
                row => Text(row.Name, row.Description, row.ExposureStart, row.ExposureEnd, row.ExposurePlaceId),
                () => new Exposure { PersonConditionId = caseId },
                removes: false);

        /// <summary>
        /// Each order's results as a lab, added once by its key and never
        /// updated: a key any version of any eICR wrote on the case before is
        /// not added again, whether or not the previous version carried it.
        /// </summary>
        private async Task LabsOf(int caseId, IReadOnlyList<ParsedOrder> orders)
        {
            var written = (await database.EcrCaseValues
                    .Where(value => value.Target == Labs && value.EcrCaseLink!.PersonConditionId == caseId)
                    .Select(value => new { value.Key, value.TargetId })
                    .ToListAsync(cancellationToken))
                .DistinctBy(value => value.Key)
                .ToDictionary(value => value.Key, value => value.TargetId);

            foreach (var order in orders.Where(order => order.Results.Count > 0 || order.AccessionNo is not null))
            {
                var key = KeyOf(Text("lab", order.AccessionNo ?? order.TestCode, order.CollectedOn ?? order.ObservedOn));

                if (written.TryGetValue(key, out var labId))
                {
                    if (link.Values.All(value => value.Target != Labs || value.Key != key))
                    {
                        Record(Labs, labId, key, key);
                    }

                    continue;
                }

                labId = await new LabQueries(database).Add(
                    caseId,
                    new CreateLabRequest(
                        CollectionDate: order.CollectedOn,
                        AccessionNo: order.AccessionNo,
                        Tests: order.Results
                            .Select((result, index) => new CreateLabTestRequest(
                                LoincCode: result.ObservationCode,
                                ReferenceRange: result.ReferenceRange,
                                LabTestDate: order.ObservedOn,
                                Results:
                                [
                                    new CreateLabTestResultRequest(
                                        ResultValue: result.ResultValue,
                                        TestResult: result.CodedResult,
                                        Units: result.Units,
                                        PrimaryLabResult: index == 0),
                                ]))
                            .ToList()),
                    cancellationToken);

                Record(Labs, labId, key, key);
                written[key] = labId;
            }
        }

        /// <summary>
        /// A value on a row the eICR did not make: written where it is blank or
        /// still as the previous version wrote it, and only where the eICR
        /// carries one.
        /// </summary>
        private void Value(int caseId, string key, string? current, string? value, Action write)
        {
            if (value is null || !MayWrite(current, Prior(Cases, key)?.Written))
            {
                return;
            }

            write();
            Record(Cases, caseId, key, value);
        }

        /// <summary>
        /// The rows of one table the eICR makes, by key: each added where the
        /// previous version had none, and updated where it had one no user has
        /// changed; and, where <paramref name="removes"/>, the previous
        /// version's unchanged rows this one does not carry removed.
        /// </summary>
        private async Task Rows<T>(
            string target,
            IReadOnlyList<(string Key, Action<T> Fill)> wanted,
            Func<T, int> idOf,
            Func<T, string> textOf,
            Func<T> create,
            bool removes)
            where T : class
        {
            foreach (var (key, fill) in wanted.DistinctBy(row => row.Key))
            {
                if (Prior(target, key) is not { } prior)
                {
                    var row = create();
                    fill(row);
                    database.Add(row);
                    await database.SaveChangesAsync(cancellationToken);
                    Record(target, idOf(row), key, textOf(row));
                    continue;
                }

                var existing = await database.Set<T>().FindAsync([prior.TargetId], cancellationToken);
                if (existing is null || !MayWrite(textOf(existing), prior.Written))
                {
                    continue;
                }

                fill(existing);
                Record(target, prior.TargetId, key, textOf(existing));
            }

            if (removes)
            {
                foreach (var prior in previous.Where(value =>
                    value.Target == target && wanted.All(row => KeyOf(row.Key) != value.Key)))
                {
                    if (await database.Set<T>().FindAsync([prior.TargetId], cancellationToken) is { } existing
                        && MayWrite(textOf(existing), prior.Written))
                    {
                        database.Remove(existing);
                    }
                }
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        private EcrCaseValue? Prior(string target, string key) =>
            previous.FirstOrDefault(value => value.Target == target && value.Key == KeyOf(key));

        private void Record(string target, int targetId, string key, string? written) =>
            link.Values.Add(new EcrCaseValue { Target = target, TargetId = targetId, Key = KeyOf(key), Written = written });

        /// <summary>The key as its column holds it.</summary>
        private static string KeyOf(string key) => key.Length > KeyLength ? key[..KeyLength] : key;
    }
}
