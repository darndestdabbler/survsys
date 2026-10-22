namespace Survsys.Contracts;

/// <summary>
/// A vaccine recorded against a case, as the case's Vaccines tab shows it: the
/// record's own fields and the vaccine named from the catalogue.
/// </summary>
/// <remarks>
/// The record carries the vaccine's description as well as its id so the tab
/// need not resolve the id against the catalogue it holds. A record is saved
/// and edited through <see cref="SaveVaccineRecordRequest"/>.
/// </remarks>
/// <param name="Id">The record's identifier.</param>
/// <param name="VaccineId">The vaccine administered.</param>
/// <param name="VaccineDescription">The vaccine's name, or null where it has none.</param>
/// <param name="AdministeredDate">The day the vaccine was administered, or null where unrecorded.</param>
/// <param name="DoseNumber">Which dose this was, or null where unrecorded.</param>
/// <param name="LotNumber">The vaccine's lot number, or null where unrecorded.</param>
/// <param name="ExpirationDate">The day the lot expires, or null where unrecorded.</param>
/// <param name="Comment">A note on the record, or null where none was written.</param>
/// <param name="Manufacturer">The vaccine's manufacturer, as its code, or null.</param>
/// <param name="InformationSource">Where the record's information came from, as its code, or null.</param>
public record VaccineRecordResponse(
    int Id,
    int VaccineId,
    string? VaccineDescription,
    DateOnly? AdministeredDate,
    int? DoseNumber,
    string? LotNumber,
    DateOnly? ExpirationDate,
    string? Comment,
    string? Manufacturer = null,
    string? InformationSource = null);
