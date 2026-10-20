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
    /// Seconds between assignment passes over the queue. Null or empty leaves
    /// assignment, release and pruning off, so a deployment that has not turned
    /// it on keeps every message for a reviewer, as it did before.
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
    /// Minutes between reprocessing passes, which retry the messages held for
    /// a retryable reason. Null leaves reprocessing off, so a held message
    /// waits for a reviewer's retry. Runs whether or not assignment is on, so a
    /// deployment may retry held messages and still assign each one by hand.
    /// </summary>
    public int? ReprocessIntervalMinutes { get; set; }

    /// <summary>
    /// How many retries reprocessing gives a held message before it is left to
    /// a reviewer, counting a reviewer's own.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

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
/// rather than opening another. <paramref name="Category"/> is the
/// <see cref="MessageExceptionRules"/> code a message sent to pending is held
/// under, and null for any other state.
/// </summary>
public sealed record AssignmentDecision(
    string Status, int? PersonId, string? Reason, bool UpdatesCase = false, string? Category = null);

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
            return Pending(
                $"The patient's zip '{message.PatientZip}' names no single investigating agency.",
                MessageExceptionRules.ZipCodeNotMappedCode);
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
    /// promoted in the meantime is left as they left it. A message whose
    /// assignment throws is held as a failed-to-add-or-update exception with
    /// the failure's account, which may be retried, and the pass goes on to
    /// the next.
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

        var waiting = await database.StagedMessages
            .Where(message => message.ReviewStatus == MessageReviewStatusRules.EntryCode)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in waiting)
        {
            try
            {
                await AssignOne(database, parser, thresholds, options, id, now, cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                await HoldFailed(database, id, failure, cancellationToken);
            }
        }

        return waiting.Count;
    }

    /// <summary>
    /// Retries every message held for a retryable category that has been
    /// retried fewer than <see cref="MessageProcessingOptions.MaxRetries"/>
    /// times, as a reviewer's retry does. Answers how many it retried.
    /// </summary>
    /// <remarks>
    /// The legacy Bulk Retry Processor and Reprocess Locked ELR channels, as a
    /// pass the worker runs every
    /// <see cref="MessageProcessingOptions.ReprocessIntervalMinutes"/>. A
    /// message held before categories were recorded has none, and is left to a
    /// reviewer's retry.
    /// </remarks>
    public static async Task<int> Reprocess(
        SurvsysDbContext database, MessageProcessingOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var retryable = MessageQueueEndpoints.RetryableCategories;
        var held = await database.StagedMessages
            .Where(message => MessageQueueEndpoints.HeldCodes.Contains(message.ReviewStatus)
                && message.MessageType != null
                && message.ExceptionCategory != null
                && retryable.Contains(message.ExceptionCategory)
                && message.RetryCount < options.MaxRetries)
            .OrderBy(message => message.Id)
            .Select(message => message.Id)
            .ToListAsync(cancellationToken);

        return held.Count == 0 ? 0 : await MessageQueueQueries.RetryHeld(database, held, cancellationToken);
    }

    private static async Task AssignOne(
        SurvsysDbContext database,
        IMessageParser parser,
        PersonMatchThresholds thresholds,
        MessageProcessingOptions options,
        int id,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var message = await database.StagedMessages.SingleOrDefaultAsync(
            message => message.Id == id && message.ReviewStatus == MessageReviewStatusRules.EntryCode,
            cancellationToken);
        if (message is null)
        {
            return;
        }

        var candidates = new List<AssignmentCandidate>();
        var request = MatchRequest(message);

        if (PersonMatchEndpoints.WhyNotAMatchRequest(request) is null)
        {
            foreach (var found in await new PersonMatchQueries(database, thresholds, parser)
                .Match(request, cancellationToken))
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
            return;
        }

        if (decision.Status == MessageReviewStatusRules.AssignedCode)
        {
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

            var personId = decision.PersonId
                ?? (await new PersonQueries(database).Add(Patient(message), cancellationToken)).Id;
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

            return;
        }

        message.ReviewStatus = decision.Status;
        message.ExceptionDetail = decision.Reason;
        message.ExceptionCategory = decision.Category;
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Holds a message whose assignment threw as a failed-to-add-or-update
    /// exception with the failure's account. What the failed attempt left
    /// tracked is discarded first — its transaction rolled back what it wrote
    /// — and a message no longer queued is left as it is.
    /// </summary>
    private static async Task HoldFailed(
        SurvsysDbContext database, int id, Exception failure, CancellationToken cancellationToken)
    {
        database.ChangeTracker.Clear();

        var message = await database.StagedMessages.SingleOrDefaultAsync(
            message => message.Id == id && message.ReviewStatus == MessageReviewStatusRules.EntryCode,
            cancellationToken);
        if (message is null)
        {
            return;
        }

        message.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
        message.ExceptionCategory = MessageExceptionRules.FailedToAddOrUpdateCode;
        message.ExceptionDetail = $"Assigning this message failed: {failure.GetBaseException().Message}";
        await database.SaveChangesAsync(cancellationToken);
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
            message.ExceptionCategory = MessageExceptionRules.EntryQueueExceptionCode;
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
    /// <remarks>
    /// The messages are loaded and saved rather than moved in one statement,
    /// so the save records each one's move on its audit trail.
    /// </remarks>
    public static async Task<int> Prune(
        SurvsysDbContext database,
        MessageProcessingOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(options);

        var cutoff = now.UtcDateTime.AddDays(-options.GraylistRetentionDays);

        var expired = await database.StagedMessages
            .Where(message => message.ReviewStatus == MessageReviewStatusRules.GrayCode && message.ReceivedAt < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var message in expired)
        {
            message.ReviewStatus = MessageReviewStatusRules.GrayUnprocessableCode;
            message.ExceptionDetail =
                $"No case appeared for this graylisted message within {options.GraylistRetentionDays} days.";
        }

        await database.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }

    /// <summary>
    /// How often the worker wakes: the assignment poll or the reprocessing
    /// interval, whichever is shorter, each at least a second; null where
    /// neither is configured and the worker does nothing. Reprocessing runs on
    /// its own interval whether or not assignment is on. Public because the
    /// unit suite proves it.
    /// </summary>
    public static TimeSpan? PassInterval(MessageProcessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        TimeSpan?[] intervals =
        [
            options.PollIntervalSeconds is { } seconds ? TimeSpan.FromSeconds(Math.Max(1, seconds)) : null,
            options.ReprocessIntervalMinutes is { } minutes ? TimeSpan.FromMinutes(Math.Max(1, minutes)) : null,
        ];

        return intervals.OfType<TimeSpan>().Cast<TimeSpan?>().Min();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (PassInterval(options.Value) is not { } interval)
        {
            logger.LogInformation(
                "Message processing is off: neither {Section}:{Poll} nor {Section}:{Reprocess} is configured, "
                + "so every message waits for a reviewer.",
                MessageProcessingOptions.SectionName,
                nameof(MessageProcessingOptions.PollIntervalSeconds),
                MessageProcessingOptions.SectionName,
                nameof(MessageProcessingOptions.ReprocessIntervalMinutes));
            return;
        }

        DateTimeOffset? reprocessedAt = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();

                if (options.Value.ReprocessIntervalMinutes is { } minutes
                    && (reprocessedAt is null
                        || DateTimeOffset.UtcNow - reprocessedAt >= TimeSpan.FromMinutes(Math.Max(1, minutes))))
                {
                    reprocessedAt = DateTimeOffset.UtcNow;
                    var retried = await Reprocess(database, options.Value, stoppingToken);
                    if (retried > 0)
                    {
                        logger.LogInformation("Message processing retried {Retried} held message(s).", retried);
                    }
                }

                if (options.Value.PollIntervalSeconds is not null)
                {
                    await AssignReleaseAndPrune(database, stoppingToken);
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

    private async Task AssignReleaseAndPrune(SurvsysDbContext database, CancellationToken stoppingToken)
    {
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

    private static AssignmentDecision Pending(
        string reason, string category = MessageExceptionRules.EntryQueueExceptionCode) =>
        new(MessageReviewStatusRules.PendingCode, null, reason, Category: category);

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
