using Microsoft.Extensions.Options;

namespace Survsys.Api;

/// <summary>
/// What the IBIS transmission reads from configuration: the endpoint a case is
/// POSTed to, and how long one transmission may take before it is given up on.
/// </summary>
/// <remarks>
/// Off by default exactly as <see cref="CdcTransmissionOptions"/> is: an empty
/// endpoint leaves the integration inert, and a deployment that reports to
/// IBIS points this at the receiving endpoint. The timeout is in seconds.
/// </remarks>
public sealed class IbisTransmissionOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "IbisTransmission";

    /// <summary>
    /// The endpoint a case is transmitted to, or null/empty to leave
    /// transmission off.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>How long one transmission may take before it is given up on, in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// One case morbidity report as IBIS receives it: what an epidemiology
/// reporting system counts, and nothing that identifies the person — no name,
/// birth date or address.
/// </summary>
public sealed record IbisCaseRecord(
    string RecordNumber,
    string Condition,
    string? Agency,
    string? CaseStatus,
    DateOnly? EventOnsetDate,
    int? MmwrWeek,
    int? MmwrYear);

/// <summary>Which way a transmission to IBIS ended.</summary>
public enum IbisTransmissionStatus
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
/// The outcome of one transmission to IBIS: which way it ended, what the
/// endpoint said (or why it could not be reached), and when it was attempted.
/// </summary>
/// <param name="Status">Which way the transmission ended.</param>
/// <param name="Detail">The responding status and body, or the reason it could not be reached; null when accepted cleanly.</param>
/// <param name="AttemptedAt">When the transmission was attempted.</param>
public sealed record IbisTransmissionResult(
    IbisTransmissionStatus Status,
    string? Detail,
    DateTimeOffset AttemptedAt);

/// <summary>
/// Transmits one case to IBIS, the external epidemiology reporting system, as
/// JSON over an HTTP POST under a timeout.
/// </summary>
/// <remarks>
/// Shaped as <see cref="CdcTransmitter"/> is and for the same reasons: every
/// way a transmission can end becomes an <see cref="IbisTransmissionResult"/>
/// rather than an exception, the outcome is logged before it is returned, and
/// the one thing let through is the caller's own cancellation. What a case
/// carries to IBIS is built elsewhere and handed here; this service only
/// delivers it.
/// </remarks>
public sealed class IbisTransmitter(
    HttpClient client,
    IOptions<IbisTransmissionOptions> options,
    ILogger<IbisTransmitter> logger)
{
    /// <summary>
    /// Transmits <paramref name="record"/> to the configured endpoint, under a
    /// timeout of <see cref="IbisTransmissionOptions.TimeoutSeconds"/>, and
    /// returns the outcome.
    /// </summary>
    public async Task<IbisTransmissionResult> Transmit(IbisCaseRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var settings = options.Value;
        var attemptedAt = DateTimeOffset.Now;

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            const string why = "IBIS transmission is off: no endpoint is configured.";
            logger.LogWarning("Not transmitting to IBIS: {Reason}", why);
            return new IbisTransmissionResult(IbisTransmissionStatus.Faulted, why, attemptedAt);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        try
        {
            using var response = await client.PostAsJsonAsync(settings.Endpoint, record, linked.Token);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Transmitted case {RecordNumber} to IBIS at {Endpoint}: {Status}.",
                    record.RecordNumber,
                    settings.Endpoint,
                    (int)response.StatusCode);
                return new IbisTransmissionResult(IbisTransmissionStatus.Accepted, null, attemptedAt);
            }

            var body = await response.Content.ReadAsStringAsync(linked.Token);
            var detail = $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";
            logger.LogWarning(
                "IBIS rejected case {RecordNumber} at {Endpoint}: {Detail}.",
                record.RecordNumber,
                settings.Endpoint,
                detail);
            return new IbisTransmissionResult(IbisTransmissionStatus.Rejected, detail, attemptedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = $"No response within {settings.TimeoutSeconds}s.";
            logger.LogWarning(
                "Case {RecordNumber} to IBIS at {Endpoint} timed out: {Detail}",
                record.RecordNumber,
                settings.Endpoint,
                detail);
            return new IbisTransmissionResult(IbisTransmissionStatus.TimedOut, detail, attemptedAt);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or UriFormatException)
        {
            logger.LogWarning(
                ex,
                "Case {RecordNumber} could not be transmitted to IBIS at {Endpoint}.",
                record.RecordNumber,
                settings.Endpoint);
            return new IbisTransmissionResult(IbisTransmissionStatus.Faulted, ex.Message, attemptedAt);
        }
    }
}
