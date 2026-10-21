using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>The processing a notification is generated for, as MSH-11 carries it.</summary>
public enum NotificationProcessing
{
    /// <summary>Production: <c>P</c>.</summary>
    Production,

    /// <summary>Training: <c>T</c>.</summary>
    Training,

    /// <summary>Debugging: <c>D</c>.</summary>
    Debug,
}

/// <summary>The queue the notifications are exported from.</summary>
public enum NotificationQueueType
{
    /// <summary>The production queue.</summary>
    Production,

    /// <summary>The onboarding queue, while a guide is being validated with CDC.</summary>
    Onboarding,

    /// <summary>The batch queue.</summary>
    Batch,
}

/// <summary>
/// The <c>NotificationQueue</c> configuration section: how often the export
/// queue is processed, the processing id its messages are generated under, and
/// the queue it is.
/// </summary>
public sealed class NotificationQueueOptions
{
    public const string SectionName = "NotificationQueue";

    /// <summary>
    /// Seconds between passes over the queue. Null leaves the worker off, so a
    /// deployment that has not turned it on exports only by "process queue now".
    /// </summary>
    public int? PollIntervalSeconds { get; set; }

    /// <summary>The processing id the messages are generated under; production by default.</summary>
    public NotificationProcessing ProcessingId { get; set; } = NotificationProcessing.Production;

    /// <summary>The queue this deployment exports from, recorded on each log row; production by default.</summary>
    public NotificationQueueType Queue { get; set; } = NotificationQueueType.Production;
}

/// <summary>
/// The legacy NMI export queue worker: each pass takes every released entry off
/// <c>nmi_export_queue</c>, builds its notification, sends it to CDC and writes
/// the outcome to <c>nmi_log</c>.
/// </summary>
/// <remarks>
/// The pass is a public static over the database, which the worker runs on
/// its interval, the "process queue now" endpoint runs on demand, and the
/// integration suite runs directly. An entry leaves the queue whatever its
/// outcome: an accepted send is logged <c>accepted</c>, and a build stopped by
/// an error or a send that was rejected, timed out or faulted is logged
/// <c>failed</c> with its detail. Off unless
/// <see cref="NotificationQueueOptions.PollIntervalSeconds"/> is configured.
/// </remarks>
public sealed class NotificationQueueService(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationQueueOptions> options,
    ILogger<NotificationQueueService> logger) : BackgroundService
{
    public const string AcceptedOutcome = "accepted";
    public const string FailedOutcome = "failed";

    /// <summary>The MSH-11 code of <paramref name="processing"/>: <c>P</c>, <c>T</c> or <c>D</c>.</summary>
    public static string ProcessingCode(NotificationProcessing processing) => processing switch
    {
        NotificationProcessing.Training => "T",
        NotificationProcessing.Debug => "D",
        _ => "P",
    };

    /// <summary>
    /// Takes each unheld entry off the queue, oldest first, builds and sends
    /// its notification, and logs the outcome; answers with how many it took.
    /// </summary>
    /// <remarks>
    /// Safe beside another pass: each entry is claimed by deleting it before
    /// it is built, and only the pass whose delete took it sends and logs it.
    /// The claim commits with the log row, so a cancelled pass leaves the
    /// entry queued.
    /// </remarks>
    public static async Task<int> Process(
        SurvsysDbContext database,
        NotificationMessageBuilder builder,
        CdcTransmitter transmitter,
        NotificationQueueOptions options,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var entries = await database.NotificationQueue.AsNoTracking()
            .Where(entry => !entry.Hold)
            .OrderBy(entry => entry.QueuedAt)
            .ThenBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
        var processingId = ProcessingCode(options.ProcessingId);
        var queue = options.Queue.ToString().ToLowerInvariant();
        var processed = 0;

        foreach (var entry in entries)
        {
            // Claim the entry by taking it off the queue first: a pass running
            // beside this one (the worker and "process queue now", or another
            // instance) waits on the deleted row and then finds it gone, so no
            // entry is sent or logged twice. The claim, the send and the log
            // are one transaction, so a pass cancelled or failing before its
            // log is written puts the entry back rather than losing it.
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            var claimed = await database.NotificationQueue
                .Where(queued => queued.Id == entry.Id && !queued.Hold)
                .ExecuteDeleteAsync(cancellationToken);
            if (claimed == 0)
            {
                continue;
            }

            processed++;
            NotificationBuild build;
            try
            {
                build = await builder.Build(entry.PersonConditionId, processingId, time.GetUtcNow(), cancellationToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // A build that throws is logged as a failure and the entry left
                // off the queue, rather than rolled back to fail every pass.
                var recordNumber = await database.PersonConditions
                    .Where(report => report.Id == entry.PersonConditionId)
                    .Select(report => report.RecordNumber)
                    .SingleAsync(cancellationToken);
                database.CdcExportLog.Add(new CdcExportLogEntry
                {
                    PersonConditionId = entry.PersonConditionId,
                    RecordNumber = recordNumber,
                    QueuedAt = entry.QueuedAt,
                    ProcessingId = processingId,
                    Queue = queue,
                    Outcome = FailedOutcome,
                    Detail = $"The message could not be built: {failure.Message}",
                });
                await database.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                continue;
            }

            var log = new CdcExportLogEntry
            {
                PersonConditionId = entry.PersonConditionId,
                RecordNumber = build.RecordNumber,
                GuideId = build.GuideId,
                GeneratedMessage = build.Message,
                QueuedAt = entry.QueuedAt,
                ProcessingId = processingId,
                Queue = queue,
                Details = build.Messages
                    .Select(message => new CdcExportLogDetail
                    {
                        PhinVar = message.PhinVar,
                        Message = message.Message,
                        Severity = message.Severity.ToString(),
                    })
                    .ToList(),
            };

            if (build.Message is null)
            {
                log.Outcome = FailedOutcome;
                log.Detail = "The message was not built: an error stopped it.";
            }
            else
            {
                var result = await transmitter.Transmit(build.Message, cancellationToken);
                var accepted = result.Status == CdcTransmissionStatus.Accepted;
                log.Outcome = accepted ? AcceptedOutcome : FailedOutcome;
                log.SentAt = accepted ? result.AttemptedAt.UtcDateTime : null;
                log.Detail = accepted ? "CDC accepted the message." : $"{result.Status}: {result.Detail}";
            }

            database.CdcExportLog.Add(log);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return processed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.PollIntervalSeconds is not { } seconds)
        {
            logger.LogInformation(
                "The notification queue worker is off: {Section}:{Poll} is not configured.",
                NotificationQueueOptions.SectionName,
                nameof(NotificationQueueOptions.PollIntervalSeconds));
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, seconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processed = await Process(
                    scope.ServiceProvider.GetRequiredService<SurvsysDbContext>(),
                    scope.ServiceProvider.GetRequiredService<NotificationMessageBuilder>(),
                    scope.ServiceProvider.GetRequiredService<CdcTransmitter>(),
                    options.Value,
                    TimeProvider.System,
                    stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("The notification queue worker exported {Processed} case(s).", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception failure)
            {
                logger.LogError(failure, "A notification queue pass failed; the next pass will retry.");
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
}
