using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// What an <see cref="IMessageParser"/> makes of one inbound message: the
/// fields intake keeps, whatever format the message arrived in.
/// </summary>
/// <param name="Format">The format the message was read as, such as <c>HL7v2</c>.</param>
/// <param name="MessageType">The message type, such as <c>ORU^R01</c> (MSH-9).</param>
/// <param name="SendingFacility">The sending facility's code (MSH-4.1).</param>
/// <param name="Person">The patient's demographics (PID), as the intake record the rest of the system takes.</param>
/// <param name="Street">The patient's street address (PID-11.1).</param>
/// <param name="City">The patient's city (PID-11.3).</param>
/// <param name="State">The patient's state (PID-11.4).</param>
/// <param name="Zip">The patient's zip code (PID-11.5).</param>
/// <param name="Orders">One entry per order the message reports (one per OBR), in message order.</param>
public record ParsedMessage(
    string Format,
    string? MessageType,
    string? SendingFacility,
    CreatePersonRequest Person,
    string? Street,
    string? City,
    string? State,
    string? Zip,
    IReadOnlyList<ParsedOrder> Orders);
