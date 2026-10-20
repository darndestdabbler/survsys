using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>One coded value a message's lab is written with: its list, its code and its id.</summary>
public sealed record LabCode(string List, string Code, int Id);

/// <summary>
/// What a message's lab is written with that the message does not hold
/// itself: the lab facility its sender is on file as, and the coded values of
/// the specimen source, test status and susceptibility lists.
/// </summary>
public sealed record LabCodes(int? LabFacilityId, IReadOnlyList<LabCode> Codes)
{
    /// <summary>The id of the value on <paramref name="list"/> with this code, ignoring case, or null.</summary>
    public int? Find(string list, string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Codes.FirstOrDefault(value => value.List == list
                && string.Equals(value.Code, code.Trim(), StringComparison.OrdinalIgnoreCase))?.Id;
}

/// <summary>
/// Where updating a case from a message left the message: assigned to a case,
/// or pending with why, and the case it was assigned to or held against.
/// </summary>
public sealed record CaseUpdate(string Status, int? PersonConditionId, string? Reason);

/// <summary>
/// Writes a staged message onto a case: the case it opens or updates, the
/// specimen, tests, results and susceptibilities it carries, the address it
/// gives its patient, and the message's own assignment to that case.
/// </summary>
/// <remarks>
/// Promotion by a reviewer and automatic assignment both write cases here, so
/// a message becomes a case one way. Everything is one transaction, joined
/// where the caller already holds one: a failure part-way leaves no case, no
/// lab and the message where it was.
///
/// The message is claimed first, by an update that only moves it from a
/// promotable state. The row it locks is held to the commit, so of two
/// promotions of one message the second finds it assigned and moves nothing,
/// and so writes nothing.
/// </remarks>
public static class MessageCaseWriter
{
    /// <summary>
    /// Opens the case <paramref name="creation"/> describes for the message
    /// with this id, writes the message's lab and address onto it, and assigns
    /// the message to it; answers with the case, or null where the message is
    /// not (or no longer) in a state it can be promoted from, having written
    /// nothing.
    /// </summary>
    public static async Task<PersonConditionResponse?> Open(
        SurvsysDbContext database,
        int messageId,
        CreatePersonConditionRequest creation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(creation);

        await using var owned = await Join(database, cancellationToken);

        if (await Claim(database, messageId, cancellationToken) is not { } staged)
        {
            return null;
        }

        var created = await OpenClaimed(database, staged, creation, now, cancellationToken);
        await Commit(owned, cancellationToken);

        return created;
    }

    /// <summary>
    /// Updates one of a person's cases from the message with this id — the
    /// legacy <c>updateCmrProcess</c> — and answers where that left the
    /// message, or null where it is not (or no longer) promotable.
    /// </summary>
    /// <remarks>
    /// The candidates are the person's open, undeleted cases on the message's
    /// condition, or the one case <paramref name="personConditionId"/> names,
    /// which must be such a case. A case-management decision that allows no update is a reviewer's. The
    /// condition's whitelist rule then chooses among them: an exception, or
    /// several matched, is a reviewer's; none matched, or the one matched Not a
    /// Case where the rule heeds case status, opens a new case where the
    /// decision allows one and is a reviewer's where it does not. On the one
    /// case matched, a message whose patient is not an exact match for the
    /// case's person is a reviewer's; otherwise its lab is compared with the
    /// case's by <see cref="LabComparison"/> — added, knitted and upgraded, or
    /// left as a duplicate, or an exception that is a reviewer's — its address
    /// added, and the message assigned to the case. A message left to a
    /// reviewer moves to pending with why, and with the case where there is
    /// one.
    /// </remarks>
    public static async Task<CaseUpdate?> Update(
        SurvsysDbContext database,
        IMessageParser parser,
        PersonMatchThresholds thresholds,
        MessageProcessingOptions options,
        int messageId,
        int personId,
        int? personConditionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        await using var owned = await Join(database, cancellationToken);

        if (await Claim(database, messageId, cancellationToken) is not { } staged)
        {
            return null;
        }

        var outcome = await UpdateClaimed(
            database, parser, thresholds, options, staged, personId, personConditionId, now, cancellationToken);
        await Commit(owned, cancellationToken);

        return outcome;
    }

    /// <summary>
    /// The specimen a message writes onto a case, or null where it carries no
    /// specimen, result or accession to write.
    /// </summary>
    /// <remarks>
    /// A result whose observation names a susceptibility agent on file is a
    /// susceptibility, read as susceptible, resistant or indeterminate from its
    /// interpretation, coded result or abnormal flag, and tested on the
    /// organism the message's other results found. Every other result is a test
    /// of its own, under its master LOINC where the vocabulary gave one, with
    /// its OBX-11 status and the one result it reported; the first is the
    /// primary result. Public because the unit suite proves it.
    /// </remarks>
    public static CreateLabRequest? Lab(StagedMessage message, LabCodes codes)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(codes);

        if (message.Results.Count == 0 && message.AccessionNo is null && message.CollectedOn is null)
        {
            return null;
        }

        var results = message.Results.OrderBy(result => result.Sequence).ToList();
        var agents = codes.Codes.Where(code => code.List == CodeLists.ResistTestAgent).ToList();

        LabCode? AgentOf(StagedMessageResult result) =>
            result.ObservationName is { } name
                ? agents.FirstOrDefault(agent => name.Contains(agent.Code, StringComparison.OrdinalIgnoreCase))
                : null;

        var tests = results.Where(result => AgentOf(result) is null).ToList();
        var organismId = tests.Select(result => result.OrganismId).FirstOrDefault(id => id is not null);

        return new CreateLabRequest(
            LabFacilityId: codes.LabFacilityId,
            SpecimenSourceId: codes.Find(CodeLists.SpecimenSource, message.SpecimenSourceCode),
            CollectionDate: message.CollectedOn,
            AccessionNo: message.AccessionNo,
            Tests: tests
                .Select((result, index) => new CreateLabTestRequest(
                    TestStatusId: codes.Find(CodeLists.TestStatus, result.ResultStatus),
                    LoincCode: result.MasterLoinc ?? result.ObservationCode,
                    ReferenceRange: result.ReferenceRange,
                    LabTestDate: message.ObservedOn,
                    Results:
                    [
                        new CreateLabTestResultRequest(
                            OrganismId: result.OrganismId,
                            ResultValue: result.ResultValue,
                            TestResult: result.Interpretation ?? result.CodedResult,
                            Units: result.Units,
                            PrimaryLabResult: index == 0),
                    ]))
                .ToList(),
            ResistTests: results
                .Where(result => AgentOf(result) is not null)
                .Select(result => new CreateLabResistTestRequest(
                    OrganismId: organismId,
                    ResistTestAgentId: AgentOf(result)!.Id,
                    ResistTestResultId: codes.Find(CodeLists.ResistTestResult, Susceptibility(result)),
                    TestDate: message.ObservedOn))
                .ToList());
    }

    private static async Task<CaseUpdate> UpdateClaimed(
        SurvsysDbContext database,
        IMessageParser parser,
        PersonMatchThresholds thresholds,
        MessageProcessingOptions options,
        StagedMessage staged,
        int personId,
        int? personConditionId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (staged.AllowUpdateCase != true)
        {
            return await Pend(database, staged, null,
                "The condition's case-management rule does not allow this message to update a case.",
                cancellationToken);
        }

        var cases = await database.PersonConditions
            .Where(report => report.PersonId == personId
                && report.DeletedAt == null
                && report.WorkflowState != WorkflowRules.ClosedCode)
            .Where(report => personConditionId == null
                ? report.ConditionId == staged.ConditionId
                : report.Id == personConditionId
                    && (staged.ConditionId == null || report.ConditionId == staged.ConditionId))
            .Select(report => new { report.Id, report.ConditionId, report.EventOnsetDate, report.CaseStatus })
            .ToListAsync(cancellationToken);

        if (personConditionId is { } named && cases.Count == 0)
        {
            return await Pend(database, staged, null,
                $"Case {named} is not an open case of person {personId} on this message's condition.",
                cancellationToken);
        }

        var conditionId = cases.FirstOrDefault()?.ConditionId ?? staged.ConditionId;
        var condition = await database.Conditions
            .Where(condition => condition.Id == conditionId)
            .Select(condition => new { condition.CdcCode, condition.Name })
            .SingleOrDefaultAsync(cancellationToken);
        var rule = WhitelistRules.For(options, condition?.CdcCode, condition?.Name);

        var match = WhitelistRules.Match(
            rule,
            staged.CollectedOn ?? staged.ObservedOn,
            cases.Select(report => new WhitelistCase(
                    report.Id, report.EventOnsetDate, report.CaseStatus == CaseStatusRules.NotACaseCode))
                .ToList());

        if (match.Exception is { } exception)
        {
            return await Pend(database, staged, personConditionId, exception, cancellationToken, MessageExceptionRules.WhitelistRuleExceptionCode);
        }

        if (match.Matched.Count > 1)
        {
            return await Pend(database, staged, null,
                $"{match.Matched.Count} of person {personId}'s cases match this message by the whitelist rule.",
                cancellationToken,
                MessageExceptionRules.WhitelistRuleExceptionCode);
        }

        if (match.Matched.Count == 0 || (match.Matched[0].NotACase && !rule.IgnoreCaseStatus))
        {
            if (staged.AllowNewCase != true || conditionId is null)
            {
                return await Pend(database, staged, null,
                    "No case of this person's matches this message, and its case-management rule opens no new one.",
                    cancellationToken);
            }

            var created = await OpenClaimed(
                database,
                staged,
                new CreatePersonConditionRequest(
                    personId, conditionId.Value, staged.AgencyId, DateOnly.FromDateTime(staged.ReceivedAt)),
                now,
                cancellationToken);

            return new CaseUpdate(MessageReviewStatusRules.AssignedCode, created.Id, null);
        }

        var target = match.Matched[0].Id;

        var exact = (await new PersonMatchQueries(database, thresholds, parser)
                .Match(MessageProcessingService.MatchRequest(staged) with { PersonIds = [personId] }, cancellationToken))
            .Any(found => found.Verdict == PersonMatchCandidateResponse.Exact);

        if (!exact)
        {
            return await Pend(database, staged, target,
                $"This message's patient is not an exact match for person {personId}, whose case it would update.",
                cancellationToken);
        }

        if (Lab(staged, await Codes(database, staged.SendingFacility, cancellationToken)) is { } lab)
        {
            var comparison = LabComparison.Compare(
                lab,
                await StoredLabs(database, target, cancellationToken),
                await StatusRanks(database, cancellationToken));

            switch (comparison.Outcome)
            {
                case LabComparisonOutcome.Exception:
                    return await Pend(database, staged, target, comparison.Reason!, cancellationToken);
                case LabComparisonOutcome.Add:
                    await new LabQueries(database).Add(target, lab, cancellationToken);
                    break;
                case LabComparisonOutcome.Update:
                    await Knit(database, comparison, cancellationToken);
                    break;
            }
        }

        await AddAddress(database, personId, staged, cancellationToken);
        await Assign(database, staged, target, cancellationToken);

        return new CaseUpdate(MessageReviewStatusRules.AssignedCode, target, null);
    }

    private static async Task<PersonConditionResponse> OpenClaimed(
        SurvsysDbContext database,
        StagedMessage staged,
        CreatePersonConditionRequest creation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var created = await new PersonConditionQueries(database, new CaseNotificationQueue(database))
            .Add(creation, now, cancellationToken);

        if (Lab(staged, await Codes(database, staged.SendingFacility, cancellationToken)) is { } lab)
        {
            await new LabQueries(database).Add(created.Id, lab, cancellationToken);
        }

        await AddAddress(database, created.PersonId, staged, cancellationToken);
        await Assign(database, staged, created.Id, cancellationToken);

        return created;
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
    /// Moves the message to assigned if it is in a promotable state, and
    /// answers with it and its results, or null where it was not.
    /// </summary>
    private static async Task<StagedMessage?> Claim(
        SurvsysDbContext database, int messageId, CancellationToken cancellationToken)
    {
        var claimed = await database.StagedMessages
            .Where(message => message.Id == messageId
                && MessageQueueEndpoints.PromotableCodes.Contains(message.ReviewStatus))
            .ExecuteUpdateAsync(
                set => set.SetProperty(message => message.ReviewStatus, MessageReviewStatusRules.AssignedCode),
                cancellationToken);

        return claimed == 0
            ? null
            : await database.StagedMessages
                .Include(message => message.Results)
                .SingleAsync(message => message.Id == messageId, cancellationToken);
    }

    private static async Task Assign(
        SurvsysDbContext database, StagedMessage staged, int personConditionId, CancellationToken cancellationToken)
    {
        staged.ReviewStatus = MessageReviewStatusRules.AssignedCode;
        staged.PersonConditionId = personConditionId;
        staged.ExceptionDetail = null;
        staged.ExceptionCategory = null;

        // Claim moved the message to assigned past the change tracker. Where the
        // message was loaded after that, the save sees no change of state to
        // record, so the trail is written here; where it was already tracked in
        // its earlier state, the save records the move itself.
        if (database.Entry(staged).Property(message => message.ReviewStatus).OriginalValue
            == MessageReviewStatusRules.AssignedCode)
        {
            staged.Audits.Add(StagedMessageAudit.Of(staged, DateTime.UtcNow));
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Sends the message to pending with why, held as an entry-queue exception
    /// unless <paramref name="category"/> names another.
    /// </summary>
    private static async Task<CaseUpdate> Pend(
        SurvsysDbContext database,
        StagedMessage staged,
        int? personConditionId,
        string reason,
        CancellationToken cancellationToken,
        string category = MessageExceptionRules.EntryQueueExceptionCode)
    {
        staged.ReviewStatus = MessageReviewStatusRules.PendingCode;
        staged.PersonConditionId = personConditionId;
        staged.ExceptionDetail = reason;
        staged.ExceptionCategory = category;
        await database.SaveChangesAsync(cancellationToken);

        return new CaseUpdate(MessageReviewStatusRules.PendingCode, personConditionId, reason);
    }

    /// <summary>The labs on a case, with their tests' first results and their susceptibilities.</summary>
    private static async Task<IReadOnlyList<StoredLab>> StoredLabs(
        SurvsysDbContext database, int personConditionId, CancellationToken cancellationToken)
    {
        var labIds = await database.PersonConditionLabs
            .Where(link => link.PersonConditionId == personConditionId)
            .Select(link => link.LabId)
            .ToListAsync(cancellationToken);
        var labs = await database.Labs
            .Where(lab => labIds.Contains(lab.Id))
            .OrderBy(lab => lab.Id)
            .ToListAsync(cancellationToken);
        var tests = await database.LabTests
            .Where(test => labIds.Contains(test.LabId))
            .OrderBy(test => test.Id)
            .ToListAsync(cancellationToken);
        var testIds = tests.Select(test => test.Id).ToList();
        var results = (await database.LabTestResults
                .Where(result => testIds.Contains(result.LabTestId))
                .OrderBy(result => result.Id)
                .ToListAsync(cancellationToken))
            .GroupBy(result => result.LabTestId)
            .ToDictionary(group => group.Key, group => group.First());
        var resistTests = await database.LabResistTests
            .Where(resist => labIds.Contains(resist.LabId))
            .ToListAsync(cancellationToken);

        return labs
            .Select(lab => new StoredLab(
                lab.Id,
                lab.CollectionDate,
                lab.AccessionNo,
                tests.Where(test => test.LabId == lab.Id)
                    .Select(test => new StoredTest(
                        test.Id,
                        test.LoincCode,
                        test.TestStatusId,
                        results.GetValueOrDefault(test.Id)?.ResultValue,
                        results.GetValueOrDefault(test.Id)?.TestResult,
                        results.GetValueOrDefault(test.Id)?.OrganismId))
                    .ToList(),
                resistTests.Where(resist => resist.LabId == lab.Id)
                    .Select(resist => new StoredResistTest(resist.ResistTestAgentId, resist.ResistTestResultId))
                    .ToList()))
            .ToList();
    }

    /// <summary>Each test status's id to its rank, the test_status list's sort order.</summary>
    private static async Task<IReadOnlyDictionary<int, int>> StatusRanks(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        await database.CodeValues
            .Where(value => value.List == CodeLists.TestStatus)
            .ToDictionaryAsync(value => value.Id, value => value.SortOrder, cancellationToken);

    /// <summary>Knits the comparison's new tests and susceptibilities onto the lab, and upgrades its statuses.</summary>
    private static async Task Knit(
        SurvsysDbContext database, LabComparisonResult comparison, CancellationToken cancellationToken)
    {
        var labId = comparison.LabId!.Value;

        foreach (var testRequest in comparison.NewTests ?? [])
        {
            var test = new LabTest
            {
                LabId = labId,
                TestTypeId = testRequest.TestTypeId,
                TestStatusId = testRequest.TestStatusId,
                LoincCode = testRequest.LoincCode,
                ReferenceRange = testRequest.ReferenceRange,
                LabTestDate = testRequest.LabTestDate,
            };

            database.LabTests.Add(test);
            await database.SaveChangesAsync(cancellationToken);

            database.LabTestResults.AddRange((testRequest.Results ?? []).Select(result => new LabTestResult
            {
                LabTestId = test.Id,
                OrganismId = result.OrganismId,
                ResultValue = result.ResultValue,
                TestResult = result.TestResult,
                Comment = result.Comment,
                Units = result.Units,
            }));
        }

        database.LabResistTests.AddRange((comparison.NewResistTests ?? []).Select(resist => new LabResistTest
        {
            LabId = labId,
            OrganismId = resist.OrganismId,
            ResistTestAgentId = resist.ResistTestAgentId,
            ResistTestResultId = resist.ResistTestResultId,
            TestDate = resist.TestDate,
        }));

        foreach (var upgrade in comparison.Upgrades ?? [])
        {
            var test = await database.LabTests.SingleAsync(test => test.Id == upgrade.TestId, cancellationToken);
            test.TestStatusId = upgrade.TestStatusId;
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>The susceptibility a result reports, as the resist_test_result list codes it.</summary>
    private static string? Susceptibility(StagedMessageResult result) =>
        (result.AbnormalFlag?.Trim().ToUpperInvariant()) switch
        {
            "S" => "Susceptible",
            "R" => "Resistant",
            "I" => "Indeterminate",
            _ => result.Interpretation ?? result.CodedResult,
        };

    /// <summary>The codes a message's lab is written with, and its sender's lab facility.</summary>
    private static async Task<LabCodes> Codes(
        SurvsysDbContext database, string? sendingFacility, CancellationToken cancellationToken)
    {
        string[] lists = [CodeLists.SpecimenSource, CodeLists.TestStatus, CodeLists.ResistTestAgent, CodeLists.ResistTestResult];

        var codes = await database.CodeValues
            .Where(value => lists.Contains(value.List) && value.Active)
            .OrderBy(value => value.Id)
            .Select(value => new LabCode(value.List, value.Code, value.Id))
            .ToListAsync(cancellationToken);

        var facilityId = sendingFacility is null
            ? null
            : await database.Facilities
                .Where(facility => facility.ShortName == sendingFacility)
                .OrderBy(facility => facility.Id)
                .Select(facility => (int?)facility.Id)
                .FirstOrDefaultAsync(cancellationToken);

        return new LabCodes(facilityId, codes);
    }

    /// <summary>
    /// Adds the message's patient address to the person, where it names a
    /// street or zip and the person has no address with that street and zip.
    /// </summary>
    private static async Task AddAddress(
        SurvsysDbContext database, int personId, StagedMessage message, CancellationToken cancellationToken)
    {
        var street = message.PatientStreet?.Trim();
        var zip = message.PatientZip?.Trim();

        if (string.IsNullOrEmpty(street) && string.IsNullOrEmpty(zip))
        {
            return;
        }

        var addresses = await database.PersonAddresses
            .Where(address => address.PersonId == personId)
            .Select(address => new { address.Street, address.PostalCode })
            .ToListAsync(cancellationToken);

        if (addresses.Any(address =>
            string.Equals(address.Street?.Trim() ?? "", street ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(address.PostalCode?.Trim() ?? "", zip ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        database.PersonAddresses.Add(new PersonAddress
        {
            PersonId = personId,
            Street = street,
            City = message.PatientCity?.Trim(),
            State = message.PatientState?.Trim(),
            PostalCode = zip,
        });
        await database.SaveChangesAsync(cancellationToken);
    }
}
