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
/// The <c>Conformance</c> configuration section: the conformance profile each
/// sending facility's messages are checked against at intake, and the
/// environment's values the profiles expect.
/// </summary>
/// <remarks>
/// A sender with no profile is not checked, so an empty
/// <see cref="SenderProfiles"/> leaves conformance off. The Connecticut OIDs
/// differ between CT's testing and production receivers, so each environment's
/// configuration names its own.
/// </remarks>
public sealed class ConformanceOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Conformance";

    /// <summary>The OID the Connecticut profile expects in MSH-5.2.</summary>
    public string? ConnecticutReceivingApplicationOid { get; set; }

    /// <summary>The OID the Connecticut profile expects in MSH-6.2.</summary>
    public string? ConnecticutReceivingFacilityOid { get; set; }

    /// <summary>
    /// Each sending facility's profile, by its MSH-4.1 code, named as
    /// <see cref="ConnecticutElrProfile.Name"/> names the one profile there is.
    /// </summary>
    public Dictionary<string, string> SenderProfiles { get; set; } = [];

    /// <summary>
    /// The profile each sender in <see cref="SenderProfiles"/> is checked
    /// against, its code matched without regard to case. Throws
    /// <see cref="InvalidOperationException"/> for a profile this tier does not
    /// know, or the Connecticut profile without both its OIDs, so a
    /// misconfiguration is refused rather than left unchecked.
    /// </summary>
    public IReadOnlyDictionary<string, ConformanceProfile> ProfilesBySender()
    {
        ConformanceProfile? connecticut = null;
        var profiles = new Dictionary<string, ConformanceProfile>(StringComparer.OrdinalIgnoreCase);

        foreach (var (sender, name) in SenderProfiles)
        {
            if (!string.Equals(name, ConnecticutElrProfile.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{SectionName}:{nameof(SenderProfiles)} names '{name}' for '{sender}', which is not a "
                    + $"profile this tier knows; the one there is is {ConnecticutElrProfile.Name}.");
            }

            if (string.IsNullOrWhiteSpace(ConnecticutReceivingApplicationOid)
                || string.IsNullOrWhiteSpace(ConnecticutReceivingFacilityOid))
            {
                throw new InvalidOperationException(
                    $"The {ConnecticutElrProfile.Name} profile needs {SectionName}:"
                    + $"{nameof(ConnecticutReceivingApplicationOid)} and {nameof(ConnecticutReceivingFacilityOid)}.");
            }

            connecticut ??= ConnecticutElrProfile.Create(
                ConnecticutReceivingApplicationOid, ConnecticutReceivingFacilityOid);
            profiles[sender] = connecticut;
        }

        return profiles;
    }
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
/// message per order, once <see cref="IntakeTranslator"/> has translated it
/// through the vocabulary and its condition's rules; a message that cannot be
/// parsed, comes from a sender not on file, or that the vocabulary or rules
/// decide nothing for, is held in the <see cref="MessageReviewStatus.Exception"/>
/// state with the reason, rather than dropped, and one its condition's filter
/// refuses is <see cref="MessageReviewStatus.Filtered"/>. This stages a message and no more: it writes no
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
    IMessageParser parser,
    IServiceScopeFactory scopeFactory,
    IOptions<ElrIntakeOptions> options,
    IOptions<ConformanceOptions> conformance,
    ILogger<ElrIntakeService> logger) : BackgroundService
{
    /// <summary>The longest <see cref="StagedMessage.ConformanceWarnings"/> its column holds.</summary>
    private const int ConformanceWarningsLength = 2000;

    /// <summary>Where a handled file is moved once it has been staged.</summary>
    private const string ProcessedFolder = ".processed";

    /// <summary>Where a file is moved when it could not be handled at all.</summary>
    private const string FailedFolder = ".failed";

    /// <summary>
    /// Classifies one raw message, read by <paramref name="parser"/>, into the
    /// staged messages it becomes: one per row of
    /// <see cref="ParsedMessage.Rows"/> — an order (OBR) with the patient it was
    /// reported against — each carrying the whole raw text, the parsed patient, its
    /// order, specimen and results, queued for review in the
    /// <see cref="MessageReviewStatus.Entry"/> state. A message that fails to
    /// parse becomes one row in the <see cref="MessageReviewStatus.Exception"/>
    /// state carrying the parser's detail, and a message whose sending facility
    /// (MSH-4.1) is not among <paramref name="knownSendingFacilities"/> has every
    /// row held in that state with that reason. A message from a known sender
    /// with a profile in <paramref name="profilesBySender"/> is checked against
    /// it: where it fails an exception-level check every row is held under
    /// <see cref="MessageExceptionRules.ConformanceCode"/> with the failures as
    /// its detail, and the warning-level failures are recorded on every row.
    /// The one decision the intake makes, kept pure and static so it is proven
    /// without a host, a database or a file.
    /// </summary>
    public static IReadOnlyList<StagedMessage> Stage(
        IMessageParser parser,
        string raw,
        string? source,
        IReadOnlySet<string> knownSendingFacilities,
        IReadOnlyDictionary<string, ConformanceProfile>? profilesBySender = null)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(knownSendingFacilities);

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
            excepted.ExceptionCategory = MessageExceptionRules.UnparseableCode;
            return [excepted];
        }

        var known = parsed.SendingFacility is { } sender && knownSendingFacilities.Contains(sender);
        string? heldDetail = known ? null : UnknownSender(parsed.SendingFacility);
        var heldCategory = known ? null : MessageExceptionRules.UnknownSenderCode;
        string? warnings = null;

        if (known && profilesBySender is not null && profilesBySender.TryGetValue(parsed.SendingFacility!, out var profile))
        {
            var failures = profile.Check(raw);
            var exceptions = failures.Where(failure => failure.Severity == ConformanceSeverity.Exception).ToList();
            if (exceptions.Count > 0)
            {
                heldDetail = $"The message does not conform to the {profile.Name} profile: "
                    + string.Join(" ", exceptions.Select(failure => failure.Detail));
                heldCategory = MessageExceptionRules.ConformanceCode;
            }

            warnings = string.Join(
                "\n",
                failures.Where(failure => failure.Severity == ConformanceSeverity.Warning).Select(failure => failure.Detail));
            warnings = warnings.Length == 0 ? null : warnings[..Math.Min(warnings.Length, ConformanceWarningsLength)];
        }

        // A message that reports no patient at all is still staged, once, so it
        // is not lost.
        var rows = parsed.Rows;
        if (rows.Count == 0)
        {
            var empty = NewMessage();
            empty.MessageType = parsed.MessageType;
            empty.SendingFacility = parsed.SendingFacility;
            empty.ReviewStatus = heldCategory is null
                ? MessageReviewStatusRules.EntryCode
                : MessageReviewStatusRules.ExceptionCode;
            empty.ExceptionDetail = heldDetail;
            empty.ExceptionCategory = heldCategory;
            empty.ConformanceWarnings = warnings;
            return [empty];
        }

        return rows
            .Select((row, index) =>
            {
                var (patient, order) = row;
                var message = NewMessage();
                message.MessageType = parsed.MessageType;
                message.SendingFacility = parsed.SendingFacility;
                message.OrderIndex = index;
                message.PatientLastName = patient.Person.LastName;
                message.PatientFirstName = patient.Person.FirstName;
                message.PatientMiddleName = patient.Person.MiddleName;
                message.PatientBirthDate = patient.Person.BirthDate;
                message.PatientSex = patient.Person.BirthGender;
                message.PatientStreet = patient.Street;
                message.PatientCity = patient.City;
                message.PatientState = patient.State;
                message.PatientZip = patient.Zip;
                message.ReviewStatus = heldCategory is null
                    ? MessageReviewStatusRules.EntryCode
                    : MessageReviewStatusRules.ExceptionCode;
                message.ExceptionDetail = heldDetail;
                message.ExceptionCategory = heldCategory;
                message.ConformanceWarnings = warnings;

                if (order is not null)
                {
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
    /// file when its short name is the message's MSH-4.1, translates each
    /// message it queued through <see cref="Translate"/>, and saves what it
    /// staged. A sender with a profile in <paramref name="profilesBySender"/>
    /// is checked against it, as <see cref="Stage"/> says.
    /// </summary>
    public static async Task<IReadOnlyList<StagedMessage>> Receive(
        IMessageParser parser,
        SurvsysDbContext database,
        string raw,
        string? source,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, ConformanceProfile>? profilesBySender = null)
    {
        var facilities = await LoadFacilities(database, cancellationToken);
        var staged = Stage(
            parser, raw, source, facilities.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), profilesBySender);

        await Translate(database, facilities, staged, cancellationToken);

        database.StagedMessages.AddRange(staged);
        await database.SaveChangesAsync(cancellationToken);

        return staged;
    }

    /// <summary>
    /// Translates each of <paramref name="messages"/> that is queued for review
    /// through <see cref="IntakeTranslator"/> against its sending facility's
    /// vocabulary and its condition's rules, holding as an exception one whose
    /// sending facility is not on file. Saves nothing: the caller does.
    /// </summary>
    /// <remarks>
    /// What intake does to a message it has just staged, and what a retry does
    /// again to one already in the queue, against the vocabulary and rules on
    /// file now.
    /// </remarks>
    public static async Task Translate(
        SurvsysDbContext database, IReadOnlyList<StagedMessage> messages, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(messages);

        await Translate(database, await LoadFacilities(database, cancellationToken), messages, cancellationToken);
    }

    private static async Task Translate(
        SurvsysDbContext database,
        IReadOnlyDictionary<string, int> facilities,
        IReadOnlyList<StagedMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!messages.Any(message => message.ReviewStatus == MessageReviewStatusRules.EntryCode))
        {
            return;
        }

        var zips = messages
            .Select(message => ZipAgencyRules.Normalize(message.PatientZip))
            .OfType<string>()
            .Distinct()
            .ToList();
        var reference = await LoadReference(database, zips, cancellationToken);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var message in messages.Where(message => message.ReviewStatus == MessageReviewStatusRules.EntryCode))
        {
            if (message.SendingFacility is { } sender && facilities.TryGetValue(sender, out var labId))
            {
                IntakeTranslator.Translate(message, labId, reference, today);
            }
            else
            {
                message.ReviewStatus = MessageReviewStatusRules.ExceptionCode;
                message.ExceptionDetail = UnknownSender(message.SendingFacility);
                message.ExceptionCategory = MessageExceptionRules.UnknownSenderCode;
            }
        }
    }

    /// <summary>
    /// The sending facilities on file, by short name, each to its id. Two
    /// facilities sharing a short name are one sender to a message, so the
    /// first on file is the one its vocabulary is read for.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, int>> LoadFacilities(
        SurvsysDbContext database, CancellationToken cancellationToken) =>
        (await database.Facilities
            .Where(facility => facility.ShortName != null)
            .OrderBy(facility => facility.Id)
            .Select(facility => new { facility.Id, ShortName = facility.ShortName! })
            .ToListAsync(cancellationToken))
        .DistinctBy(facility => facility.ShortName, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(facility => facility.ShortName, facility => facility.Id, StringComparer.OrdinalIgnoreCase);

    private static string UnknownSender(string? sendingFacility) =>
        $"The sending facility '{sendingFacility}' (MSH-4.1) is not on file.";

    /// <summary>
    /// Reads what <see cref="IntakeTranslator"/> translates against: the
    /// vocabulary, with archived child LOINCs left out because they no longer
    /// resolve; each condition's rules in execution order, their conditions in
    /// the order they were written; and the zip-to-agency rows for the
    /// <paramref name="zips"/> the messages carry.
    /// </summary>
    private static async Task<IntakeReference> LoadReference(
        SurvsysDbContext database, IReadOnlyList<string> zips, CancellationToken cancellationToken)
    {
        var masterLoincs = await database.VocabMasterLoincs.AsNoTracking()
            .Select(master => new MasterLoincEntry(
                master.Loinc, master.ConditionId, master.OrganismId, master.OrganismFromResult))
            .ToListAsync(cancellationToken);
        var childLoincs = await database.VocabChildLoincs.AsNoTracking()
            .Where(child => !child.Archived)
            .Join(
                database.VocabMasterLoincs,
                child => child.MasterLoincId,
                master => master.Id,
                (child, master) => new ChildLoincEntry(child.ChildLoinc, child.LabId, master.Loinc))
            .ToListAsync(cancellationToken);
        var organisms = await database.VocabMasterOrganisms.AsNoTracking()
            .Select(organism => new MasterOrganismEntry(
                organism.Snomed, organism.Id, organism.ConditionId, organism.SnomedAlt, organism.OrganismName))
            .ToListAsync(cancellationToken);
        var masterValues = await database.VocabMasterValues.AsNoTracking()
            .Select(master => new MasterValueEntry(master.Id, master.Category, master.Concept, master.AppValue))
            .ToListAsync(cancellationToken);
        var childValues = await database.VocabChildValues.AsNoTracking()
            .Select(child => new ChildValueEntry(child.LabId, child.Category, child.ChildCode, child.MasterId))
            .ToListAsync(cancellationToken);

        var rules = await database.VocabRules.AsNoTracking()
            .OrderBy(rule => rule.ExecutionOrder)
            .ThenBy(rule => rule.Id)
            .ToListAsync(cancellationToken);
        var conditions = (await database.VocabRuleConditions.AsNoTracking()
                .OrderBy(condition => condition.Id)
                .ToListAsync(cancellationToken))
            .ToLookup(condition => condition.VocabRuleId);

        ConditionExpression When(VocabRule rule) => new(
            Value: null,
            OtherSpecified: null,
            rule.ExecutionOrder,
            conditions[rule.Id]
                .Select(condition => new ExpressionCondition(
                    condition.Field, condition.Operator, condition.Value, condition.AndOr, ValueIsField: false))
                .ToList());

        var rulesByCondition = rules
            .GroupBy(rule => rule.ConditionId)
            .ToDictionary(
                group => group.Key,
                group => new IntakeConditionRules(
                    group.Where(rule => rule.Kind == VocabRule.MessageFilterKind).Select(When).ToList(),
                    group.Where(rule => rule.Kind == VocabRule.CaseManagementKind)
                        .Select(rule => new CaseManagementRule(
                            When(rule), rule.AllowNewCase, rule.AllowUpdateCase, rule.IsSurveillance))
                        .ToList()));

        var zipMapping = await database.ZipCodeAgencies.AsNoTracking()
            .Where(row => zips.Contains(row.ZipCode))
            .ToListAsync(cancellationToken);

        return new IntakeReference(
            new VocabularyMatcher(new Vocabulary(masterLoincs, childLoincs, organisms, masterValues, childValues)),
            rulesByCondition,
            zipMapping);
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
            var staged = await Receive(
                parser, database, raw, fileName, perMessage.Token, conformance.Value.ProfilesBySender());

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
