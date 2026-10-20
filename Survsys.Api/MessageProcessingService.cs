using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Contracts;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// The <c>MessageProcessing</c> configuration section: how often the queue is
/// processed, the jurisdiction's home state, and how long a graylisted message
/// waits for a case.
/// </summary>
public sealed class MessageProcessingOptions
{
    public const string SectionName = "MessageProcessing";

    /// <summary>
    /// Seconds between passes over the queue. Null or empty leaves processing
    /// off, so a deployment that has not turned it on keeps every message for
    /// a reviewer, as it did before.
    /// </summary>
    public int? PollIntervalSeconds { get; set; }

    /// <summary>
    /// The jurisdiction's state, as a patient address's state carries it
    /// (<c>CT</c>). A patient whose state is another is out of state; empty
    /// leaves no message out of state.
    /// </summary>
    public string? HomeState { get; set; }

    /// <summary>
    /// Days a graylisted message waits for a case before it is pruned. The
    /// legacy pruner's 18 months.
    /// </summary>
    public int GraylistRetentionDays { get; set; } = 548;

    /// <summary>
    /// The whitelist rule choosing the case a message updates, for a condition
    /// with none of its own: 90 days from the case's event date by default.
    /// </summary>
    public WhitelistRule Whitelist { get; set; } = new();

    /// <summary>
    /// Whitelist rules of their own, by condition code (the condition's CDC
    /// code), or by name for a condition with no code.
    /// </summary>
    public Dictionary<string, WhitelistRule> WhitelistByCondition { get; set; } = [];
}

/// <summary>
/// A person on file a message's patient was scored against: who, how well
/// they match, and whether they already have an open case on the message's
/// condition.
/// </summary>
public sealed record AssignmentCandidate(int PersonId, PersonMatchKind Kind, bool HasOpenCase);

/// <summary>
/// Where automatic assignment sends a message: the review-state code it moves
/// to, and why. For <see cref="MessageReviewStatusRules.AssignedCode"/>, the
/// person the case is opened for, or null where a new person is created from
/// the message's patient. <paramref name="UpdatesCase"/> where the person
/// already has an open case on the condition, which the message updates
/// rather than opening another.
/// </summary>
public sealed record AssignmentDecision(string Status, int? PersonId, string? Reason, bool UpdatesCase = false);

/// <summary>
/// The automated half of the legacy EMSA assignment, and the graylist: a
/// background worker that, each pass, assigns the messages queued for review
/// that it can assign with confidence, releases graylisted messages a case has
/// appeared for, and prunes graylisted messages that waited too long.
/// </summary>
/// <remarks>
/// The legacy ran these as Mirth channels and a spooler fed by EpiTrax
/// triggers; here each is a public static pass over the database, which the
/// worker runs in turn and the integration suite runs directly. Off unless
/// <see cref="MessageProcessingOptions.PollIntervalSeconds"/> is configured.
///
/// Assignment opens a new case, or updates the open case a confident person
/// already has on the condition, through <see cref="MessageCaseWriter"/>.
/// </remarks>
public sealed class MessageProcessingService(
    IMessageParser parser,
    PersonMatchThresholds thresholds,
    IServiceScopeFactory scopeFactory,
    IOptions<MessageProcessingOptions> options,
    ILogger<MessageProcessingService> logger) : BackgroundService
{
    /// <summary>
    /// Where automatic assignment sends one message queued for review, given
    /// the people on file its patient was scored against.
    /// </summary>
    /// <remarks>
    /// In the legacy order: a message with no condition or no case-management
    /// decision is a reviewer's; one whose rule allows no new case is
    /// graylisted to wait for a case; a patient outside the home state is out
    /// of state; a message with no investigating agency is a reviewer's. Then
    /// the person: exactly one exact match is that person, whose open case on
    /// the condition, where they have one, the message updates; no exact or
    /// probable match at all is
    /// a new person, if the message names one; anything else is a reviewer's.
    /// </remarks>
    public static AssignmentDecision Decide(
        StagedMessage message, IReadOnlyList<AssignmentCandidate> candidates, string? homeState)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(candidates);

        if (message.ConditionId is null || message.AllowNewCase is null)
        {
            return Pending("This message has no condition and case-management decision to assign it by.");
        }

        if (message.AllowNewCase == false)
        {
            return new AssignmentDecision(
                MessageReviewStatusRules.GrayCode,
                null,
                "Graylisted: the condition's case-management rule opens no case for this result, so it "
                + "waits for a case to attach to.");
        }

        if (!string.IsNullOrWhiteSpace(homeState)
            && !string.IsNullOrWhiteSpace(message.PatientState)
            && !string.Equals(message.PatientState.Trim(), homeState.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return new AssignmentDecision(
                MessageReviewStatusRules.OutOfStateCode,
                null,
                $"The patient's state '{message.PatientState}' is not {homeState}.");
        }

        if (message.AgencyId is null)
        {
            return Pending($"The patient's zip '{message.PatientZip}' names no single investigating agency.");
        }

        var exact = candidates.Where(candidate => candidate.Kind == PersonMatchKind.Exact).ToList();

        if (exact is [var person])
        {
            return new AssignmentDecision(
                MessageReviewStatusRules.AssignedCode, person.PersonId, null, UpdatesCase: person.HasOpenCase);
        }

        if (exact.Count > 1)
        {
            return Pending($"{exact.Count} people on file match this message's patient exactly.");
        }

        if (candidates.Any(candidate => candidate.Kind == PersonMatchKind.Fuzzy))
        {
            return Pending("Only probable matches for this message's patient are on file.");
        }

        return PersonEndpoints.WhyNotAPerson(Patient(message)) is { } whyNot
            ? Pending(whyNot)
            : new AssignmentDecision(MessageReviewStatusRules.AssignedCode, null, null);
    }

    /// <summary>
    /// Takes every message queued for review through <see cref="Decide"/>:
    /// opening a case — and a person, where the decision names none — for one
    /// it assigns, and moving each to the state decided, with why. Answers
    /// how many it moved.
    /// </summary>
    /// <remarks>
    /// Each message is saved on its own, so one that fails leaves the ones
    /// before it assigned. A message assigned is written by
    /// <see cref="MessageCaseWriter.Open"/>, in one transaction with the new
    /// person it is assigned to, so a failure leaves neither; one a reviewer
    /// promoted in the meantime is left as they left it.
    /// </remarks>
    public static async Task<int> Assign(
        SurvsysDbContext database,
        IMessageParser parser,
        PersonMatchThresholds thresholds,
        MessageProcessingOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var matches = new PersonMatchQueries(database, thresholds, parser);
        var persons = new PersonQueries(database);

        var waiting = await database.StagedMessages
            .Where(message => message.ReviewStatus == MessageReviewStatusRules.EntryCode)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken);

        foreach (var message in waiting)
        {
            var candidates = new List<AssignmentCandidate>();
            var request = MatchRequest(message);

            if (PersonMatchEndpoints.WhyNotAMatchRequest(request) is null)
            {
                foreach (var found in await matches.Match(request, cancellationToken))
                {
                    var kind = Kind(found.Verdict);
                    if (kind == PersonMatchKind.None)
                    {
                        continue;
                    }

                    var hasOpenCase = await database.PersonConditions.AnyAsync(
                        report => report.PersonId == found.Person.Id
                            && report.ConditionId == message.ConditionId
                            && report.DeletedAt == null
                            && report.WorkflowState != WorkflowRules.ClosedCode,
                        cancellationToken);
                    candidates.Add(new AssignmentCandidate(found.Person.Id, kind, hasOpenCase));
                }
            }

            var decision = Decide(message, candidates, options.HomeState);

            if (decision.UpdatesCase)
            {
                await MessageCaseWriter.Update(
                    database, parser, thresholds, options, message.Id, decision.PersonId!.Value, null, now, cancellationToken);
                continue;
            }

            if (decision.Status == MessageReviewStatusRules.AssignedCode)
            {
                await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

                var personId = decision.PersonId
                    ?? (await persons.Add(Patient(message), cancellationToken)).Id;
                var created = await MessageCaseWriter.Open(
                    database,
                    message.Id,
                    new CreatePersonConditionRequest(
                        personId,
                        message.ConditionId!.Value,
                        message.AgencyId,
                        DateOnly.FromDateTime(message.ReceivedAt)),
                    now,
                    cancellationToken);

                if (created is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                continue;
            }

            message.ReviewStatus = decision.Status;
            message.ExceptionDetail = decision.Reason;
            await database.SaveChangesAsync(cancellationToken);
        }

        return waiting.Count;
    }

    /// <summary>
    /// Releases to a reviewer each graylisted message a case has appeared for:
    /// a case on its condition, not deleted, whose event date is within
    /// <see cref="GraylistWindow.Default"/> of the message's collection date
    /// (its received date where it has none), for a person who scores an exact
    /// match against the message's patient. The message moves to pending with
    /// that case. Answers how many it released.
    /// </summary>
    public static async Task<int> Release(
        SurvsysDbContext database,
        IMessageParser parser,
        PersonMatchThresholds thresholds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var matches = new PersonMatchQueries(database, thresholds, parser);
        var window = GraylistWindow.Default;

        var graylisted = await database.StagedMessages
            .Where(message => message.ReviewStatus == MessageReviewStatusRules.GrayCode && message.ConditionId != null)
            .OrderBy(message => message.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var message in graylisted)
        {
            var conditionId = message.ConditionId!.Value;
            var referenceDate = message.CollectedOn ?? DateOnly.FromDateTime(message.ReceivedAt);
            var candidate = new GraylistCandidate(conditionId, referenceDate);

            // A case matches when the message's date is within the window drawn
            // around the case's date, so the case's date is within the window
            // turned the other way around the message's.
            var earliest = referenceDate.AddDays(-window.DaysAfter);
            var latest = referenceDate.AddDays(window.DaysBefore);
            var cases = (await database.PersonConditions
                    .Where(report => report.ConditionId == conditionId
                        && report.DeletedAt == null
                        && report.EventOnsetDate >= earliest
                        && report.EventOnsetDate <= latest)
                    .OrderBy(report => report.Id)
                    .Select(report => new { report.Id, report.PersonId, report.RecordNumber, report.EventOnsetDate })
                    .ToListAsync(cancellationToken))
                .Where(report => GraylistRules.Matches(
                    new GraylistRequest(conditionId, report.EventOnsetDate!.Value), candidate, window))
                .ToList();

            if (cases.Count == 0)
            {
                continue;
            }

            var request = MatchRequest(message) with
            {
                PersonIds = cases.Select(report => report.PersonId).Distinct().ToList(),
            };
            var exact = (await matches.Match(request, cancellationToken))
                .Where(found => Kind(found.Verdict) == PersonMatchKind.Exact)
                .Select(found => found.Person.Id)
                .ToHashSet();

            if (cases.FirstOrDefault(report => exact.Contains(report.PersonId)) is not { } found)
            {
                continue;
            }

            message.ReviewStatus = MessageReviewStatusRules.PendingCode;
            message.PersonConditionId = found.Id;
            message.ExceptionDetail =
                $"Released from the graylist: case {found.RecordNumber} has appeared for this patient.";
            await database.SaveChangesAsync(cancellationToken);
            released++;
        }

        return released;
    }

    /// <summary>
    /// Moves each graylisted message received more than
    /// <see cref="MessageProcessingOptions.GraylistRetentionDays"/> before
    /// <paramref name="now"/> to gray-unprocessable: no case appeared for it in
    /// its window. The legacy pruner deleted it; this keeps what was received.
    /// Answers how many it pruned.
    /// </summary>
    public static Task<int> Prune(
        SurvsysDbContext database,
        MessageProcessingOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var cutoff = now.UtcDateTime.AddDays(-options.GraylistRetentionDays);

        return database.StagedMessages
            .Where(message => message.ReviewStatus == MessageReviewStatusRules.GrayCode && message.ReceivedAt < cutoff)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(message => message.ReviewStatus, MessageReviewStatusRules.GrayUnprocessableCode)
                    .SetProperty(
                        message => message.ExceptionDetail,
                        $"No case appeared for this graylisted message within {options.GraylistRetentionDays} days."),
                cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollIntervalSeconds is not { } seconds)
        {
            logger.LogInformation(
                "Message processing is off: no {Section}:{Key} is configured, so every message waits "
                + "for a reviewer.",
                MessageProcessingOptions.SectionName,
                nameof(MessageProcessingOptions.PollIntervalSeconds));
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, seconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();

                var assigned = await Assign(database, parser, thresholds, options.Value, DateTimeOffset.Now, stoppingToken);
                var released = await Release(database, parser, thresholds, stoppingToken);
                var pruned = await Prune(database, options.Value, DateTimeOffset.UtcNow, stoppingToken);

                if (assigned + released + pruned > 0)
                {
                    logger.LogInformation(
                        "Message processing moved {Assigned} queued, released {Released} and pruned {Pruned} "
                        + "graylisted message(s).",
                        assigned,
                        released,
                        pruned);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception failure)
            {
                logger.LogError(failure, "A message processing pass failed; the next pass will retry.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static AssignmentDecision Pending(string reason) =>
        new(MessageReviewStatusRules.PendingCode, null, reason);

    /// <summary>The person a message's patient would be created as.</summary>
    private static CreatePersonRequest Patient(StagedMessage message) => new(
        message.PatientLastName,
        message.PatientFirstName,
        message.PatientMiddleName,
        message.PatientBirthDate,
        message.PatientSex);

    /// <summary>A message's patient, as person matching scores it.</summary>
    internal static PersonMatchRequest MatchRequest(StagedMessage message) => new(
        message.PatientLastName,
        message.PatientFirstName,
        message.PatientBirthDate,
        message.PatientSex,
        Street: message.PatientStreet,
        PostalCode: message.PatientZip);

    private static PersonMatchKind Kind(string verdict) => verdict switch
    {
        PersonMatchCandidateResponse.Exact => PersonMatchKind.Exact,
        PersonMatchCandidateResponse.Probable => PersonMatchKind.Fuzzy,
        _ => PersonMatchKind.None,
    };
}
