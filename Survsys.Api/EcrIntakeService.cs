using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Data;
using Survsys.Messaging;

namespace Survsys.Api;

/// <summary>
/// What the eCR intake reads from configuration: the folder each feed's
/// documents are delivered to, and how often they are polled.
/// </summary>
/// <remarks>
/// AIMS delivers a test feed, which carries no PII, apart from the production
/// feed, so each has a folder of its own and a document records which it came
/// from. Both folders are empty by default, which leaves that feed off.
/// </remarks>
public sealed class EcrIntakeOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "EcrIntake";

    /// <summary>The folder test-feed documents are delivered to, or null/empty to leave that feed off.</summary>
    public string? TestDirectory { get; set; }

    /// <summary>The folder production-feed documents are delivered to, or null/empty to leave that feed off.</summary>
    public string? ProductionDirectory { get; set; }

    /// <summary>How often the folders are polled, in seconds.</summary>
    public int PollIntervalSeconds { get; set; } = 120;
}

/// <summary>
/// The eCR intake: a background worker inside the API tier that polls the test
/// and production feeds' folders and stores each eICR and RR it finds, as the
/// XML it arrived as, once.
/// </summary>
/// <remarks>
/// A document is read by <see cref="CdaParser"/> for its kind, id, set and
/// version, and for the eICR an RR responds to; the rest of what the parser
/// reads is the later eCR sessions'. Each is validated by
/// <see cref="CdaValidator"/> against the CDA schema and the HL7 schematron,
/// and its findings are stored with it: a finding of any severity never stops
/// a document from being stored. A document whose id is already on file
/// is not stored again and its file is moved to <c>.duplicate</c>; one that
/// cannot be read is moved to <c>.failed</c>, so neither stalls the poller; a
/// stored one is moved to <c>.processed</c>. This stores documents and no
/// more: it stages nothing into the ELR message queue and writes no person or
/// case.
/// </remarks>
public sealed class EcrIntakeService(
    CdaParser parser,
    CdaValidator validator,
    IServiceScopeFactory scopeFactory,
    IOptions<EcrIntakeOptions> options,
    ILogger<EcrIntakeService> logger) : BackgroundService
{
    private const string ProcessedFolder = ".processed";
    private const string DuplicateFolder = ".duplicate";
    private const string FailedFolder = ".failed";

    /// <summary>
    /// The document to store for one raw eICR or RR taken from
    /// <paramref name="feed"/>: its raw text unchanged, with the kind, ids,
    /// set and version <paramref name="parser"/> reads from it, and what
    /// <paramref name="validator"/> finds wrong with it as its findings. Throws
    /// <see cref="Hl7ParseException"/> for a document the parser cannot read,
    /// and <see cref="ArgumentException"/> for a feed that is neither test nor
    /// production.
    /// </summary>
    public static EcrDocument Stage(CdaParser parser, CdaValidator validator, string raw, string feed, string? source)
    {
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(raw);
        if (feed is not (EcrDocument.TestFeed or EcrDocument.ProductionFeed))
        {
            throw new ArgumentException($"The feed '{feed}' is neither test nor production.", nameof(feed));
        }

        var document = parser.Read(raw);
        return new EcrDocument
        {
            DocumentId = document.DocumentId,
            Kind = document.Kind,
            Feed = feed,
            SetId = document.SetId,
            VersionNumber = document.VersionNumber,
            EicrDocumentId = document.EicrDocumentId,
            RawXml = raw,
            Source = source,
            ReceivedAt = DateTime.UtcNow,
            Findings = validator.Validate(raw, document.Kind, document.IgVersion)
                .Select(finding => new EcrValidationFinding
                {
                    Source = finding.Source,
                    Severity = finding.Severity,
                    Message = finding.Message,
                    Location = finding.Location,
                })
                .ToList(),
        };
    }

    /// <summary>
    /// Stages one raw document and stores it, or answers null without storing
    /// it where a document of the same id is already on file.
    /// </summary>
    public static async Task<EcrDocument?> Receive(
        CdaParser parser,
        CdaValidator validator,
        SurvsysDbContext database,
        string raw,
        string feed,
        string? source,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);

        var document = Stage(parser, validator, raw, feed, source);
        if (await database.EcrDocuments.AnyAsync(taken => taken.DocumentId == document.DocumentId, cancellationToken))
        {
            return null;
        }

        database.EcrDocuments.Add(document);
        await database.SaveChangesAsync(cancellationToken);
        return document;
    }

    /// <summary>
    /// The versions of an eICR on <paramref name="feed"/>: every eICR sharing
    /// <paramref name="setId"/>, first version first.
    /// </summary>
    public static IQueryable<EcrDocument> Versions(IQueryable<EcrDocument> documents, string feed, string setId) =>
        documents
            .Where(document => document.Kind == EcrDocument.EicrKind
                && document.Feed == feed
                && document.SetId == setId)
            .OrderBy(document => document.VersionNumber)
            .ThenBy(document => document.ReceivedAt);

    /// <summary>The eICR on <paramref name="rr"/>'s feed that it responds to, if it is on file.</summary>
    public static IQueryable<EcrDocument> EicrOf(IQueryable<EcrDocument> documents, EcrDocument rr)
    {
        ArgumentNullException.ThrowIfNull(rr);

        return documents.Where(document => document.Kind == EcrDocument.EicrKind
            && document.Feed == rr.Feed
            && document.DocumentId == rr.EicrDocumentId);
    }

    /// <summary>The RRs on <paramref name="eicr"/>'s feed that respond to it, first received first.</summary>
    public static IQueryable<EcrDocument> ResponsesTo(IQueryable<EcrDocument> documents, EcrDocument eicr)
    {
        ArgumentNullException.ThrowIfNull(eicr);

        return documents
            .Where(document => document.Kind == EcrDocument.RrKind
                && document.Feed == eicr.Feed
                && document.EicrDocumentId == eicr.DocumentId)
            .OrderBy(document => document.ReceivedAt);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var feeds = new[]
            {
                (Feed: EcrDocument.TestFeed, Directory: options.Value.TestDirectory),
                (Feed: EcrDocument.ProductionFeed, Directory: options.Value.ProductionDirectory),
            }
            .Where(feed => !string.IsNullOrWhiteSpace(feed.Directory))
            .Select(feed => (feed.Feed, Directory: feed.Directory!))
            .ToList();

        if (feeds.Count == 0)
        {
            logger.LogInformation(
                "eCR intake is off: no {Section} folder is configured.", EcrIntakeOptions.SectionName);
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var (feed, directory) in feeds)
            {
                try
                {
                    await Poll(feed, directory, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception failure)
                {
                    logger.LogError(
                        failure, "An eCR intake poll of {Directory} failed; the next poll will retry.", directory);
                }
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    /// <summary>One sweep of a feed's folder: each file at its top level stored and moved aside.</summary>
    private async Task Poll(string feed, string directory, CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(directory);

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            stoppingToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);

            try
            {
                var raw = await File.ReadAllTextAsync(file, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();
                var stored = await Receive(parser, validator, database, raw, feed, fileName, stoppingToken);

                if (stored is null)
                {
                    logger.LogInformation("eCR file {File} is a document already on file; not stored again.", fileName);
                    Move(file, Path.Combine(directory, DuplicateFolder));
                }
                else
                {
                    logger.LogInformation(
                        "Stored eCR file {File} as {Kind} {DocumentId} from the {Feed} feed.",
                        fileName, stored.Kind, stored.DocumentId, feed);
                    Move(file, Path.Combine(directory, ProcessedFolder));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception failure)
            {
                logger.LogError(failure, "eCR file {File} could not be stored; moving it aside.", fileName);
                try
                {
                    Move(file, Path.Combine(directory, FailedFolder));
                }
                catch (IOException couldNotMove)
                {
                    logger.LogError(couldNotMove, "eCR file {File} could not be moved aside.", fileName);
                }
            }
        }
    }

    private static void Move(string file, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        File.Move(file, Path.Combine(targetDirectory, Path.GetFileName(file)), overwrite: true);
    }
}
