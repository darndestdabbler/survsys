using System.Text;
using Microsoft.Extensions.Options;

namespace Survsys.Api;

/// <summary>
/// What the direct CDC transmission reads from configuration: the endpoint a
/// generated notification is POSTed to, and how long one transmission may take
/// before it is given up on.
/// </summary>
/// <remarks>
/// The endpoint is empty by default, which turns transmission off the way an
/// empty <see cref="ElrIntakeOptions.DropDirectory"/> turns the inbound intake
/// off: a deployment that notifies CDC points this at the receiving endpoint,
/// and one that does not is left inert. The timeout is in seconds because that
/// is how a configuration file reads it, and falls back to its default rather
/// than to zero when misconfigured.
/// </remarks>
public sealed class CdcTransmissionOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "CdcTransmission";

    /// <summary>
    /// The endpoint a generated notification is transmitted to, or null/empty
    /// to leave transmission off.
    /// </summary>
    public string? Endpoint { get; set; }

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
public sealed record CdcTransmissionResult(
    CdcTransmissionStatus Status,
    string? Detail,
    DateTimeOffset AttemptedAt);

/// <summary>
/// Transmits a generated ORU^R01 notification to CDC directly, in place of the
/// legacy Mirth export queue: no integration engine between this tier and the
/// receiving endpoint, just an HTTP POST under a timeout whose outcome is
/// recorded. The message is generated elsewhere and handed here as ER7; this
/// service only delivers it.
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
/// result. Persisting the outcome to an export log is a later session's work.
/// </remarks>
public sealed class CdcTransmitter(
    HttpClient client,
    IOptions<CdcTransmissionOptions> options,
    ILogger<CdcTransmitter> logger)
{
    private const string Hl7MediaType = "application/hl7-v2";

    /// <summary>
    /// Transmits <paramref name="message"/> to the configured endpoint, under a
    /// timeout of <see cref="CdcTransmissionOptions.TimeoutSeconds"/>, and
    /// returns the outcome.
    /// </summary>
    public async Task<CdcTransmissionResult> Transmit(string message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        var attemptedAt = DateTimeOffset.Now;

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
            using var content = new StringContent(message, Encoding.UTF8, Hl7MediaType);
            using var response = await client.PostAsync(settings.Endpoint, content, linked.Token);

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
}
