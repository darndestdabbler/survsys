namespace Survsys.Contracts;

/// <summary>
/// One staged inbound message whole: the queue summary together with the raw
/// HL7 text the message arrived as — the shape
/// <c>GET /api/staged-messages/{id}</c> returns.
/// </summary>
/// <remarks>
/// The summary is a <see cref="StagedMessageResponse"/>, the same shape the
/// queue list returns, so one message reads the same whether it is read in the
/// list or on its own. The raw text is what the list has no use for and a
/// reviewer of one message does: the message exactly as a lab sent it, which a
/// reviewer reads to decide what to do with it.
/// </remarks>
/// <param name="Message">The message's queue summary.</param>
/// <param name="RawMessage">The message exactly as it arrived, the ER7 text a lab sent.</param>
public record StagedMessageDetailResponse(
    StagedMessageResponse Message,
    string RawMessage);
