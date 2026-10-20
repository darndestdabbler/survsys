namespace Survsys.Messaging;

/// <summary>
/// One order a message reports — an OBR with its specimen and results. Intake
/// stages one message per order.
/// </summary>
/// <param name="AccessionNo">The filler order number (OBR-3.1).</param>
/// <param name="TestCode">The ordered test's code (OBR-4.1, else the first OBX-3.1).</param>
/// <param name="TestName">The ordered test's name (OBR-4.2).</param>
/// <param name="ObservedOn">The observation date (OBR-7).</param>
/// <param name="SpecimenCode">The specimen type's code (SPM-4.1).</param>
/// <param name="SpecimenText">The specimen type's text (SPM-4.2).</param>
/// <param name="CollectedOn">The collection date (SPM-17, else OBR-7).</param>
/// <param name="Results">The order's results (its OBX segments), in message order.</param>
public record ParsedOrder(
    string? AccessionNo,
    string? TestCode,
    string? TestName,
    DateOnly? ObservedOn,
    string? SpecimenCode,
    string? SpecimenText,
    DateOnly? CollectedOn,
    IReadOnlyList<ParsedResult> Results);
