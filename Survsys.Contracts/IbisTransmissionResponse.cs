namespace Survsys.Contracts;

/// <summary>
/// What sending a case to IBIS answers when IBIS accepted it: how the
/// transmission ended, and the case's IBIS export state after it.
/// </summary>
/// <param name="Status">Which way the transmission ended, as text: <c>Accepted</c>.</param>
/// <param name="Detail">What the endpoint said, where it said anything worth keeping; null when accepted cleanly.</param>
/// <param name="AttemptedAt">When the transmission was attempted.</param>
/// <param name="SentToIbis">Whether the case is now recorded as sent to IBIS.</param>
/// <param name="IbisUpdatedAt">The date IBIS last accepted the case.</param>
public sealed record IbisTransmissionResponse(
    string Status,
    string? Detail,
    DateTimeOffset AttemptedAt,
    bool? SentToIbis,
    DateOnly? IbisUpdatedAt);
