namespace Survsys.Messaging;

/// <summary>
/// What an <see cref="IMessageParser"/> makes of one inbound message: the
/// fields intake keeps, whatever format the message arrived in.
/// </summary>
/// <param name="Format">The format the message was read as, such as <c>HL7v2</c>.</param>
/// <param name="MessageType">The message type, such as <c>ORU^R01</c> (MSH-9).</param>
/// <param name="SendingFacility">The sending facility's code (MSH-4.1).</param>
/// <param name="Patients">The patients the message reports on, in message order, each with their orders.</param>
public record ParsedMessage(
    string Format,
    string? MessageType,
    string? SendingFacility,
    IReadOnlyList<ParsedPatient> Patients)
{
    /// <summary>
    /// The message as the rows intake stages it: one per order, with the
    /// patient it was reported against, and one with no order for a patient
    /// reported with none, so no patient is lost. A row's index here is the
    /// one its staged message records.
    /// </summary>
    public IReadOnlyList<(ParsedPatient Patient, ParsedOrder? Order)> Rows =>
        Patients
            .SelectMany(patient => patient.Orders.Count == 0
                ? [(patient, (ParsedOrder?)null)]
                : patient.Orders.Select(order => (patient, (ParsedOrder?)order)))
            .ToList();
}
