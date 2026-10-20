namespace Survsys.Contracts;

/// <summary>
/// A vaccine in the catalogue, as the record picker shows it: the vaccine's code
/// and its description.
/// </summary>
/// <remarks>
/// The catalogue is reference data an operator chooses a record's vaccine from,
/// so the row carries what the picker shows — the code and the description — and
/// no more. A case's vaccine records are <see cref="VaccineRecordResponse"/>.
/// </remarks>
/// <param name="Id">The vaccine's identifier, as a record names it.</param>
/// <param name="Code">The vaccine's CDC code.</param>
/// <param name="Description">The vaccine's name, or null where it has none.</param>
public record VaccineResponse(int Id, string Code, string? Description);
