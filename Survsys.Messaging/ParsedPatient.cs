using Survsys.Contracts;

namespace Survsys.Messaging;

/// <summary>
/// One patient a message reports on (a PID and its patient-result group), with
/// the orders reported against them.
/// </summary>
/// <param name="Person">The patient's demographics (PID), as the intake record the rest of the system takes.</param>
/// <param name="Street">The patient's street address (PID-11.1).</param>
/// <param name="City">The patient's city (PID-11.3).</param>
/// <param name="State">The patient's state (PID-11.4).</param>
/// <param name="Zip">The patient's zip code (PID-11.5).</param>
/// <param name="Orders">One entry per order reported against this patient (one per OBR), in message order.</param>
/// <param name="County">The patient's county code (PID-11.9).</param>
public record ParsedPatient(
    CreatePersonRequest Person,
    string? Street,
    string? City,
    string? State,
    string? Zip,
    IReadOnlyList<ParsedOrder> Orders,
    string? County = null);
