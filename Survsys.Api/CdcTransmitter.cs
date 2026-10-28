using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace Survsys.Api;

/// <summary>How a generated notification leaves this tier.</summary>
public enum CdcTransport
{
    /// <summary>POSTed directly to <see cref="CdcTransmissionOptions.Endpoint"/>.</summary>
    Http,

    /// <summary>
    /// Written as a file into <see cref="CdcTransmissionOptions.OutboxDirectory"/>,
    /// for a PHINMS or AIMS sender to pick up.
    /// </summary>
    File,
}

/// <summary>
/// What the CDC transmission reads from configuration: the transport, the
/// endpoint a generated notification is POSTed to or the outbox it is written
/// to, the credentials the POST signs in with, and how long one transmission
/// may take before it is given up on.
/// </summary>
/// <remarks>
/// The endpoint and the outbox are empty by default, which turns transmission
/// off the way an empty <see cref="ElrIntakeOptions.DropDirectory"/> turns the
/// inbound intake off: a deployment that notifies CDC points the transport it
/// chose at the receiving endpoint or the sender's folder, and one that does
/// not is left inert. The timeout is in seconds because that is how a
/// configuration file reads it, and falls back to its default rather than to
/// zero when misconfigured.
/// </remarks>
public sealed class CdcTransmissionOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "CdcTransmission";

    /// <summary>How a notification leaves this tier; HTTP by default.</summary>
    public CdcTransport Transport { get; set; } = CdcTransport.Http;

    /// <summary>
    /// The endpoint a generated notification is transmitted to under the HTTP
    /// transport, or null/empty to leave transmission off.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// The folder a generated notification is written to under the file
    /// transport, or null/empty to leave transmission off.
    /// </summary>
    public string? OutboxDirectory { get; set; }

    /// <summary>The user name the HTTP transport signs in with, or empty to send without signing in.</summary>
    public string? UserName { get; set; }

    /// <summary>The HTTP transport's password, read from configuration and never stored.</summary>
    public string? Password { get; set; }

    /// <summary>How long one transmission may take before it is given up on, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Which way a transmission ended.</summary>
public enum CdcTransmissionStatus
{
    /// <summary>The endpoint answered with a success status.</summary>
    Accepted,

    /// <summary>The endpoint answered, but with a non-success status.</summary>
    Rejected,

    /// <summary>The endpoint did not answer within the configured timeout.</summary>
    TimedOut,

    /// <summary>The transmission could not be made at all — no endpoint, a bad endpoint or a transport error.</summary>
    Faulted,
}

/// <summary>
/// The outcome of one transmission: which way it ended, what the endpoint said
/// (or why it could not be reached), and when it was attempted.
/// </summary>
/// <param name="Status">Which way the transmission ended.</param>
/// <param name="Detail">The responding status and body, or the reason it could not be reached; null when accepted cleanly.</param>
/// <param name="AttemptedAt">When the transmission was attempted.</param>
/// <param name="BatchFile">The name of the file the message was written to under the file transport; null otherwise.</param>
public sealed record CdcTransmissionResult(
    CdcTransmissionStatus Status,
    string? Detail,
    DateTimeOffset AttemptedAt,
    string? BatchFile = null);

/// <summary>
/// Transmits a generated ORU^R01 notification to CDC, in place of the legacy
/// Mirth export queue: either an HTTP POST under a timeout, signed in with
/// Basic credentials when a user name is configured, or one file per message
/// written into the outbox a PHINMS or AIMS sender watches. The message is
/// generated elsewhere and handed here as ER7; this service only delivers it.
/// </summary>
/// <remarks>
/// Every way a transmission can end becomes a <see cref="CdcTransmissionResult"/>
/// rather than an exception, so a caller reads the outcome rather than guarding
/// a throw: a success status is <see cref="CdcTransmissionStatus.Accepted"/>, a
/// non-success status is <see cref="CdcTransmissionStatus.Rejected"/> with the
/// status and body as the detail, the timeout elapsing is
/// <see cref="CdcTransmissionStatus.TimedOut"/>, and an unreachable or
/// unconfigured endpoint or a transport error is
/// <see cref="CdcTransmissionStatus.Faulted"/>. The one thing it lets through is
/// the caller's own cancellation, which is cooperative and not a transmission
/// outcome. The outcome is logged before it is returned, so the record of a
/// transmission exists in the tier's log whether or not a caller keeps the
/// result.
///
/// Under the file transport a written file is <see cref="CdcTransmissionStatus.Accepted"/>
/// with its name as the result's batch file: the sender's delivery is its own
/// and is not watched here. The file is written under a temporary name and then
/// renamed, so a sender polling the folder never picks up half a message.
/// </remarks>
public sealed class CdcTransmitter(
    HttpClient client,
    IOptions<CdcTransmissionOptions> options,
    ILogger<CdcTransmitter> logger)
{
    private const string Hl7MediaType = "application/hl7-v2";

    /// <summary>
    /// Transmits <paramref name="message"/> by the configured transport — to
    /// the endpoint under a timeout of
    /// <see cref="CdcTransmissionOptions.TimeoutSeconds"/>, or into the outbox —
    /// and returns the outcome.
    /// </summary>
    public async Task<CdcTransmissionResult> Transmit(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        var attemptedAt = DateTimeOffset.Now;

        if (settings.Transport == CdcTransport.File)
        {
            return await WriteToOutbox(message, settings, attemptedAt, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            const string why = "CDC transmission is off: no endpoint is configured.";
            logger.LogWarning("Not transmitting to CDC: {Reason}", why);
            return new CdcTransmissionResult(CdcTransmissionStatus.Faulted, why, attemptedAt);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.Endpoint)
            {
                Content = new StringContent(message, Encoding.UTF8, Hl7MediaType),
            };
            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserName}:{settings.Password}")));
            }

            using var response = await client.SendAsync(request, linked.Token);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Transmitted a notification to CDC at {Endpoint}: {Status}.",
                    settings.Endpoint,
                    (int)response.StatusCode);
                return new CdcTransmissionResult(CdcTransmissionStatus.Accepted, null, attemptedAt);
            }

            var body = await response.Content.ReadAsStringAsync(linked.Token);
            var detail = $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";
            logger.LogWarning(
                "CDC rejected a notification at {Endpoint}: {Detail}.", settings.Endpoint, detail);
            return new CdcTransmissionResult(CdcTransmissionStatus.Rejected, detail, attemptedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = $"No response within {settings.TimeoutSeconds}s.";
            logger.LogWarning(
                "A notification to CDC at {Endpoint} timed out: {Detail}", settings.Endpoint, detail);
            return new CdcTransmissionResult(CdcTransmissionStatus.TimedOut, detail, attemptedAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            logger.LogWarning(
                ex, "A notification to CDC at {Endpoint} could not be transmitted.", settings.Endpoint);
            return new CdcTransmissionResult(CdcTransmissionStatus.Faulted, ex.Message, attemptedAt);
        }
    }

    private async Task<CdcTransmissionResult> WriteToOutbox(
        string message,
        CdcTransmissionOptions settings,
        DateTimeOffset attemptedAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.OutboxDirectory))
        {
            const string why = "CDC transmission is off: no outbox directory is configured.";
            logger.LogWarning("Not transmitting to CDC: {Reason}", why);
            return new CdcTransmissionResult(CdcTransmissionStatus.Faulted, why, attemptedAt);
        }

        var batchFile = $"survsys-{attemptedAt.UtcDateTime:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.hl7";
        var path = Path.Combine(settings.OutboxDirectory, batchFile);
        var partial = path + ".tmp";

        try
        {
            await File.WriteAllTextAsync(partial, message, cancellationToken);
            File.Move(partial, path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(
                ex, "A notification to CDC could not be written to {Outbox}.", settings.OutboxDirectory);
            if (File.Exists(partial))
            {
                File.Delete(partial);
            }

            return new CdcTransmissionResult(CdcTransmissionStatus.Faulted, ex.Message, attemptedAt);
        }

        logger.LogInformation(
            "Wrote a notification to CDC into {Outbox} as {BatchFile}.", settings.OutboxDirectory, batchFile);
        return new CdcTransmissionResult(
            CdcTransmissionStatus.Accepted, $"Written to the outbox as {batchFile}.", attemptedAt, batchFile);
    }
}
