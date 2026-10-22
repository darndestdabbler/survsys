namespace Survsys.Contracts;

/// <summary>
/// One row of the intake statistics: how many messages one sender had in one
/// review state among those received on one day — the shape
/// <c>GET /api/staged-messages/statistics</c> returns a list of.
/// </summary>
/// <param name="SendingFacility">The sending facility's code (MSH-4.1), or null for messages that carried none.</param>
/// <param name="ReviewStatus">The review-state code the messages are in now.</param>
/// <param name="Day">The day the messages were received, in UTC.</param>
/// <param name="Count">How many messages.</param>
public record MessageStatisticsRowResponse(
    string? SendingFacility,
    string ReviewStatus,
    DateOnly Day,
    int Count);
