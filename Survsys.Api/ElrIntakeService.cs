using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// What the inbound ELR intake reads from configuration: the directory files
/// are dropped into, how often to poll it, how long one message may take
/// before it is set aside, and the port the MLLP listener listens on.
/// </summary>
/// <remarks>
/// The drop directory is empty and the MLLP port unset by default, which turns
/// both transports off: a
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

    /// <summary>How long the handling of one message may take before it is set aside, in seconds.</summary>
    public int MessageTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The TCP port <see cref="MllpListener"/> listens on, or null to leave the
    /// MLLP listener off.
    /// </summary>
    public int? MllpPort { get; set; }
}

/// <summary>
/// The inbound ELR intake: a background worker inside the API tier that polls a
/// file drop, parses each message and queues it for review. The cut session 2
/// decided — Mirth's file receipt absorbed into an <see cref="IHostedService"/>
/// rather than kept — with no integration engine, no SOAP hop and no master
/// XML between the lab's file and the queue.
/// </summary>
/// <remarks>
/// A parseable message from a sending facility on file is queued in the
/// <see cref="MessageReviewStatus.Entry"/> state for a reviewer, one staged
/// message per order; a message that cannot be parsed, or comes from a sender
/// not on file, is held in the <see cref="MessageReviewStatus.Exception"/>
/// state with the reason, rather than dropped. This stages a message and no more: it writes no
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
    /// Classifies one raw message into the staged messages it becomes: one per
    /// order (OBR), each carrying the whole raw text, the parsed patient, its
    /// order, specimen and results, queued for review in the
    /// <see cref="MessageReviewStatus.Entry"/> state. A message that fails to
    /// parse becomes one row in the <see cref="MessageReviewStatus.Exception"/>
    /// state carrying the parser's detail, and a message whose sending facility
    /// (MSH-4.1) is not among <paramref name="knownSendingFacilities"/> has every
    /// row held in that state with that reason. The one decision the intake
    /// makes, kept pure and static so it is proven without a host, a database
    /// or a file.
    /// </summary>
    public static IReadOnlyList<StagedMessage> Stage(
        string raw, string? source, IReadOnlySet<string> knownSendingFacilities)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(knownSendingFacilities);

        IMessageParser parser = new OruR01Parser();
        var receivedAt = DateTime.UtcNow;

        StagedMessage NewMessage() => new()
        {
            RawMessage = raw,
            Source = source,
            ReceivedAt = receivedAt,
            MessageFormat = parser.Format,
        };

        ParsedMessage parsed;
        try
        {
            parsed = parser.Parse(raw);
        }
        catch (Hl7ParseException unparseable)
        {
            var excepted = NewMessage();
            excepted.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
            excepted.ExceptionDetail = unparseable.Message;
            return [excepted];
        }

        string? unknownSender = parsed.SendingFacility is { } sender && knownSendingFacilities.Contains(sender)
            ? null
            : $"The sending facility '{parsed.SendingFacility}' (MSH-4.1) is not on file.";

        // A message that reports no order is still staged, once, so it is not lost.
        IReadOnlyList<ParsedOrder?> orders = parsed.Orders.Count == 0 ? [null] : [.. parsed.Orders];

        return orders
            .Select((order, index) =>
            {
                var message = NewMessage();
                message.MessageType = parsed.MessageType;
                message.SendingFacility = parsed.SendingFacility;
                message.PatientLastName = parsed.Person.LastName;
                message.PatientFirstName = parsed.Person.FirstName;
                message.PatientMiddleName = parsed.Person.MiddleName;
                message.PatientBirthDate = parsed.Person.BirthDate;
                message.PatientSex = parsed.Person.BirthGender;
                message.PatientStreet = parsed.Street;
                message.PatientCity = parsed.City;
                message.PatientState = parsed.State;
                message.PatientZip = parsed.Zip;
                message.ReviewStatus = unknownSender is null
                    ? MessageReviewStatusRules.EntryCode
                    : MessageReviewStatusRules.ExceptionCode;
                message.ExceptionDetail = unknownSender;

                if (order is not null)
                {
                    message.OrderIndex = index;
                    message.AccessionNo = order.AccessionNo;
                    message.TestCode = order.TestCode;
                    message.TestName = order.TestName;
                    message.ObservedOn = order.ObservedOn;
                    message.SpecimenSourceCode = order.SpecimenCode;
                    message.SpecimenSourceText = order.SpecimenText;
                    message.CollectedOn = order.CollectedOn;
                    message.Results = order.Results
                        .Select((result, sequence) => new StagedMessageResult
                        {
                            Sequence = sequence,
                            ObservationCode = result.ObservationCode,
                            ObservationName = result.ObservationName,
                            ResultValue = result.ResultValue,
                            CodedResult = result.CodedResult,
                            Units = result.Units,
                            ReferenceRange = result.ReferenceRange,
                            AbnormalFlag = result.AbnormalFlag,
                            ResultStatus = result.ResultStatus,
                        })
                        .ToList();
                }

                return message;
            })
            .ToList();
    }

    /// <summary>
    /// Receives one raw message from any transport — the file drop or MLLP:
    /// stages it against the sending facilities on file, a facility being on
    /// file when its short name is the message's MSH-4.1, and saves what it
    /// staged.
    /// </summary>
    public static async Task<IReadOnlyList<StagedMessage>> Receive(
        SurvsysDbContext database, string raw, string? source, CancellationToken cancellationToken)
    {
        var shortNames = await database.Facilities
            .Where(facility => facility.ShortName != null)
            .Select(facility => facility.ShortName!)
            .ToListAsync(cancellationToken);

        var staged = Stage(raw, source, shortNames.ToHashSet(StringComparer.OrdinalIgnoreCase));
        database.StagedMessages.AddRange(staged);
        await database.SaveChangesAsync(cancellationToken);

        return staged;
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

            using var scope = scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();
            var staged = await Receive(database, raw, fileName, perMessage.Token);

            Move(file, Path.Combine(dropDirectory, ProcessedFolder));
            logger.LogInformation(
                "Staged ELR file {File} as {Count} message(s) in {Status}.",
                fileName,
                staged.Count,
                staged[0].ReviewStatus);
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
