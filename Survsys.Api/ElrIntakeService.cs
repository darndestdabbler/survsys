using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// What the inbound ELR intake reads from configuration: the directory files
/// are dropped into, how often to poll it, and how long one message may take
/// before it is set aside.
/// </summary>
/// <remarks>
/// The drop directory is empty by default, which turns the intake off: a
/// deployment that wants it points this at a directory, and one that does not
/// is left alone. The two intervals are in seconds because that is how a
/// configuration file reads them, and both fall back to their default rather
/// than to zero when misconfigured.
/// </remarks>
public sealed class ElrIntakeOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "ElrIntake";

    /// <summary>
    /// The directory inbound ELR files are dropped into, or null/empty to leave
    /// the intake off.
    /// </summary>
    public string? DropDirectory { get; set; }

    /// <summary>How often the drop directory is polled, in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 120;

    /// <summary>How long the handling of one file may take before it is set aside, in seconds.</summary>
    public int MessageTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// The inbound ELR intake: a background worker inside the API tier that polls a
/// file drop, parses each message and queues it for review. The cut session 2
/// decided — Mirth's file receipt absorbed into an <see cref="IHostedService"/>
/// rather than kept — with no integration engine, no SOAP hop and no master
/// XML between the lab's file and the queue.
/// </summary>
/// <remarks>
/// A parseable message is queued in the <see cref="MessageReviewStatus.Entry"/>
/// state for a reviewer; a message that cannot be parsed is held in the
/// <see cref="MessageReviewStatus.Exception"/> state with the parser's account
/// of why, rather than dropped. This stages a message and no more: it writes no
/// person, no lab and no case, because promoting a queued message to a case is
/// a later session's work and needs a reviewer.
///
/// The worker is a singleton, as a hosted service is, so it resolves a scoped
/// <see cref="SurvsysDbContext"/> through <see cref="IServiceScopeFactory"/> per
/// message rather than holding one. Each file is handled under its own timeout,
/// a token linked to the stopping token, so one pathological message is set
/// aside rather than left to stall the poller; a handled file is moved out of
/// the drop so it is not read twice. With no drop directory configured the
/// worker logs once and returns, so it is inert for a deployment that has not
/// turned it on.
/// </remarks>
public sealed class ElrIntakeService(
    IServiceScopeFactory scopeFactory,
    IOptions<ElrIntakeOptions> options,
    ILogger<ElrIntakeService> logger) : BackgroundService
{
    /// <summary>Where a handled file is moved once it has been staged.</summary>
    private const string ProcessedFolder = ".processed";

    /// <summary>Where a file is moved when it could not be handled at all.</summary>
    private const string FailedFolder = ".failed";

    /// <summary>
    /// Classifies one raw message into the staged message it becomes: a
    /// parseable message into the <see cref="MessageReviewStatus.Entry"/> state
    /// queued for review, and a message that fails to parse into the
    /// <see cref="MessageReviewStatus.Exception"/> state carrying the parser's
    /// detail. The one decision the intake makes, kept pure and static so it is
    /// proven without a host, a database or a file.
    /// </summary>
    public static StagedMessage Stage(string raw, string? source)
    {
        ArgumentNullException.ThrowIfNull(raw);

        var message = new StagedMessage
        {
            RawMessage = raw,
            Source = source,
            ReceivedAt = DateTime.UtcNow,
        };

        try
        {
            _ = new OruR01Parser().Parse(raw);
            message.ReviewStatus = MessageReviewStatusRules.EntryCode;
        }
        catch (Hl7ParseException unparseable)
        {
            message.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
            message.ExceptionDetail = unparseable.Message;
        }

        return message;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var dropDirectory = options.Value.DropDirectory;

        if (string.IsNullOrWhiteSpace(dropDirectory))
        {
            logger.LogInformation(
                "ELR intake is off: no {Section}:{Key} is configured, so no file drop is polled.",
                ElrIntakeOptions.SectionName,
                nameof(ElrIntakeOptions.DropDirectory));
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        var messageTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.MessageTimeoutSeconds));

        logger.LogInformation(
            "ELR intake is polling {Drop} every {Interval}.", dropDirectory, pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Poll(dropDirectory, messageTimeout, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception failure)
            {
                logger.LogError(
                    failure,
                    "An ELR intake poll of {Drop} failed; the next poll will retry.",
                    dropDirectory);
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One sweep of the drop directory: each file at its top level handled in
    /// turn. The <c>.processed</c> and <c>.failed</c> folders are subdirectories
    /// and so are not swept.
    /// </summary>
    private async Task Poll(string dropDirectory, TimeSpan messageTimeout, CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(dropDirectory);

        foreach (var file in Directory.EnumerateFiles(dropDirectory))
        {
            stoppingToken.ThrowIfCancellationRequested();
            await Handle(file, dropDirectory, messageTimeout, stoppingToken);
        }
    }

    /// <summary>
    /// Reads one dropped file, stages it, and moves it out of the drop — to
    /// <c>.processed</c> once it is queued, or to <c>.failed</c> where the file
    /// could not be read, staged or saved, so the poller is not stalled by it.
    /// </summary>
    private async Task Handle(
        string file,
        string dropDirectory,
        TimeSpan messageTimeout,
        CancellationToken stoppingToken)
    {
        var fileName = Path.GetFileName(file);

        using var perMessage = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        perMessage.CancelAfter(messageTimeout);

        try
        {
            var raw = await File.ReadAllTextAsync(file, perMessage.Token);
            var staged = Stage(raw, fileName);

            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();
            database.StagedMessages.Add(staged);
            await database.SaveChangesAsync(perMessage.Token);

            Move(file, Path.Combine(dropDirectory, ProcessedFolder));
            logger.LogInformation("Staged ELR file {File} as {Status}.", fileName, staged.ReviewStatus);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is shutting down rather than the message timing out:
            // leave the file in the drop for the next run and let the loop exit.
            throw;
        }
        catch (Exception failure)
        {
            logger.LogError(
                failure,
                "ELR file {File} could not be staged; moving it aside so the poller is not "
                + "stalled by it.",
                fileName);
            TryMove(file, Path.Combine(dropDirectory, FailedFolder));
        }
    }

    private static void Move(string file, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        File.Move(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
    }

    private void TryMove(string file, string targetDirectory)
    {
        try
        {
            Move(file, targetDirectory);
        }
        catch (IOException couldNotMove)
        {
            logger.LogError(
                couldNotMove,
                "ELR file {File} could not be moved aside; it will be retried on the next poll.",
                Path.GetFileName(file));
        }
    }
}
