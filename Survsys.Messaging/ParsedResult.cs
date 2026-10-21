namespace Survsys.Messaging;

/// <summary>One result an order reports — an OBX segment.</summary>
/// <param name="ObservationCode">What was observed, as its code (OBX-3.1).</param>
/// <param name="ObservationName">What was observed, as its name (OBX-3.2).</param>
/// <param name="ResultValue">The value (OBX-5), or a coded value's code.</param>
/// <param name="CodedResult">A coded value's text (OBX-5.2), or null for a plain value.</param>
/// <param name="Units">The units (OBX-6).</param>
/// <param name="ReferenceRange">The reference range (OBX-7).</param>
/// <param name="AbnormalFlag">The first abnormal flag (OBX-8).</param>
/// <param name="ResultStatus">The result status (OBX-11).</param>
public record ParsedResult(
    string? ObservationCode,
    string? ObservationName,
    string? ResultValue,
    string? CodedResult,
    string? Units,
    string? ReferenceRange,
    string? AbnormalFlag,
    string? ResultStatus);
