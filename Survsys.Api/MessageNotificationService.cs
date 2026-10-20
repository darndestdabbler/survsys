using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Survsys.Data;

namespace Survsys.Api;

/// <summary>
/// The <c>MessageNotification</c> configuration section: how often assigned
/// messages are batched to their agencies, and the SMTP server they go through.
/// </summary>
/// <remarks>
/// The credentials are read from configuration — an environment variable or a
/// secret store in a deployment — and never stored in the database.
/// </remarks>
public sealed class MessageNotificationOptions
{
    public const string SectionName = "MessageNotification";

    /// <summary>
    /// Minutes between batches. Null leaves notification off, as does an empty
    /// <see cref="Host"/> or <see cref="From"/>.
    /// </summary>
    public int? IntervalMinutes { get; set; }

    /// <summary>The SMTP server's host name.</summary>
    public string? Host { get; set; }

    /// <summary>The SMTP server's port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>Whether the connection to the SMTP server uses TLS.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>The address the notifications are sent from.</summary>
    public string? From { get; set; }

    /// <summary>The SMTP user name, or empty to send without signing in.</summary>
    public string? UserName { get; set; }

    /// <summary>The SMTP password, read from configuration and never stored.</summary>
    public string? Password { get; set; }

    /// <summary>Whether every setting notification needs is present.</summary>
    public bool IsConfigured =>
        IntervalMinutes is not null && !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>
/// A message assigned to a case, as its agency's notification names it: the
/// case's record number and condition, and when the message was received. No
/// patient detail is carried, because an e-mail is not where it belongs.
/// </summary>
public sealed record AssignedMessage(
    int MessageId, int? AgencyId, string RecordNumber, string Condition, DateTime ReceivedAt);

/// <summary>One agency's batch: who it goes to, what it says, and the messages it names.</summary>
public sealed record AgencyNotification(
    int AgencyId, IReadOnlyList<string> To, string Subject, string Body, IReadOnlyList<int> MessageIds);

/// <summary>
/// The legacy EMSA batch notification e-mail: a background worker that, each
/// interval, sends every agency with notification addresses one e-mail listing
/// the messages newly assigned to its cases, and marks them notified.
/// </summary>
/// <remarks>
/// Off unless <see cref="MessageNotificationOptions.IsConfigured"/>. A message
/// whose case's agency has no addresses, or whose case has no agency, is marked
/// notified with nothing sent, so it is not reconsidered on every pass.
/// </remarks>
public sealed class MessageNotificationService(
    IServiceScopeFactory scopeFactory,
    IOptions<MessageNotificationOptions> options,
    ILogger<MessageNotificationService> logger) : BackgroundService
{
    private static readonly char[] Separators = [';', ','];

    /// <summary>
    /// The addresses an agency's stored notification list names, trimmed, with
    /// blanks dropped.
    /// </summary>
    public static IReadOnlyList<string> Addresses(string? notificationEmails) =>
        (notificationEmails ?? "")
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

    /// <summary>
    /// One notification per agency that has addresses and assigned messages,
    /// listing its messages in the order received. Pure, so the batching is
    /// proven without a database or a mail server.
    /// </summary>
    public static IReadOnlyList<AgencyNotification> Compose(
        IReadOnlyList<AssignedMessage> messages, IReadOnlyDictionary<int, string?> addressesByAgency)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(addressesByAgency);

        return messages
            .Where(message => message.AgencyId is not null)
            .GroupBy(message => message.AgencyId!.Value)
            .OrderBy(group => group.Key)
            .Select(group => (Group: group, To: Addresses(addressesByAgency.GetValueOrDefault(group.Key))))
            .Where(batch => batch.To.Count > 0)
            .Select(batch =>
            {
                var listed = batch.Group.OrderBy(message => message.ReceivedAt).ThenBy(message => message.MessageId).ToList();
                var body = new StringBuilder()
                    .AppendLine(CultureInfo.InvariantCulture, $"{listed.Count} laboratory message(s) were assigned to cases of your agency:")
                    .AppendLine();
                foreach (var message in listed)
                {
                    body.AppendLine(CultureInfo.InvariantCulture,
                        $"- Case {message.RecordNumber} ({message.Condition}), message received {message.ReceivedAt:yyyy-MM-dd HH:mm} UTC");
                }

                return new AgencyNotification(
                    batch.Group.Key,
                    batch.To,
                    $"{listed.Count} laboratory message(s) assigned",
                    body.ToString(),
                    listed.Select(message => message.MessageId).ToList());
            })
            .ToList();
    }

    /// <summary>
    /// Sends each agency its batch of the assigned messages not yet notified
    /// through <paramref name="send"/>, and marks every message considered
    /// notified at <paramref name="now"/>. Answers how many e-mails it sent.
    /// </summary>
    /// <remarks>
    /// An agency's messages are marked only once its e-mail is sent, so a send
    /// that fails leaves them for the next pass; the messages no e-mail names
    /// are marked last.
    /// </remarks>
    public static async Task<int> Notify(
        SurvsysDbContext database,
        string from,
        Func<MailMessage, CancellationToken, Task> send,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(send);

        var assigned = await (
                from message in database.StagedMessages
                where message.ReviewStatus == MessageReviewStatusRules.AssignedCode
                    && message.NotifiedAt == null
                    && message.PersonConditionId != null
                join report in database.PersonConditions on message.PersonConditionId equals report.Id
                join condition in database.Conditions on report.ConditionId equals condition.Id
                select new AssignedMessage(
                    message.Id, report.AgencyId, report.RecordNumber, condition.Name, message.ReceivedAt))
            .ToListAsync(cancellationToken);

        if (assigned.Count == 0)
        {
            return 0;
        }

        var agencyIds = assigned.Select(message => message.AgencyId).OfType<int>().Distinct().ToList();
        var addresses = await database.Agencies
            .Where(agency => agencyIds.Contains(agency.Id))
            .ToDictionaryAsync(agency => agency.Id, agency => agency.NotificationEmails, cancellationToken);

        var notifications = Compose(assigned, addresses);
        foreach (var notification in notifications)
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(from),
                Subject = notification.Subject,
                Body = notification.Body,
            };
            foreach (var to in notification.To)
            {
                mail.To.Add(to);
            }

            await send(mail, cancellationToken);
            await MarkNotified(database, notification.MessageIds, now, cancellationToken);
        }

        var unsent = assigned
            .Select(message => message.MessageId)
            .Except(notifications.SelectMany(notification => notification.MessageIds))
            .ToList();
        await MarkNotified(database, unsent, now, cancellationToken);

        return notifications.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            logger.LogInformation(
                "Message notification is off: {Section} has no interval, host or sender configured.",
                MessageNotificationOptions.SectionName);
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.IntervalMinutes!.Value));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<SurvsysDbContext>();
                using var smtp = new SmtpClient(settings.Host, settings.Port)
                {
                    EnableSsl = settings.EnableSsl,
                    Credentials = string.IsNullOrWhiteSpace(settings.UserName)
                        ? null
                        : new NetworkCredential(settings.UserName, settings.Password),
                };

                var sent = await Notify(
                    database, settings.From!, smtp.SendMailAsync, DateTime.UtcNow, stoppingToken);
                if (sent > 0)
                {
                    logger.LogInformation("Message notification sent {Sent} agency e-mail(s).", sent);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception failure)
            {
                logger.LogError(failure, "A message notification pass failed; the next pass will retry.");
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

    private static Task<int> MarkNotified(
        SurvsysDbContext database, IReadOnlyList<int> ids, DateTime now, CancellationToken cancellationToken) =>
        ids.Count == 0
            ? Task.FromResult(0)
            : database.StagedMessages
                .Where(message => ids.Contains(message.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(message => message.NotifiedAt, now), cancellationToken);
}
