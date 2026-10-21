namespace Survsys.Contracts;

/// <summary>
/// A vaccine in the catalogue: its code, its description, where it falls in
/// the catalogue's order and whether it may still be chosen.
/// </summary>
/// <remarks>
/// The catalogue is reference data an administrator keeps current and a
/// record's vaccine is chosen from. A retired vaccine is listed too, so a
/// record that names it still shows it; the picker offers only the active
/// ones. A case's vaccine records are <see cref="VaccineRecordResponse"/>.
/// </remarks>
/// <param name="Id">The vaccine's identifier, as a record names it.</param>
/// <param name="Code">The vaccine's CDC code.</param>
/// <param name="Description">The vaccine's name, or null where it has none.</param>
/// <param name="SortOrder">Where the vaccine falls in the catalogue's order.</param>
/// <param name="Active">Whether the vaccine may be chosen for a new record.</param>
public record VaccineResponse(int Id, string Code, string? Description, int SortOrder, bool Active);
