namespace Survsys.Contracts;

/// <summary>
/// A vaccine record to add to a case or to save over an existing one: what
/// <c>POST /api/cases/{personConditionId}/vaccines</c> and
/// <c>PUT /api/cases/vaccines/{id}</c> take.
/// </summary>
/// <remarks>
/// The vaccine is required — a record names the vaccine administered — and must
/// be one the catalogue holds, which the endpoint refuses where it is not.
/// Everything else a record can be entered without is optional and may be left
/// null. A dose number, where given, is one or more; an expiration date, where
/// both are given, is not before the administered date.
/// </remarks>
/// <param name="VaccineId">The vaccine administered. Required.</param>
/// <param name="AdministeredDate">The day the vaccine was administered, or null for none.</param>
/// <param name="DoseNumber">Which dose this was, or null for none.</param>
/// <param name="LotNumber">The vaccine's lot number, or null for none.</param>
/// <param name="ExpirationDate">The day the lot expires, or null for none.</param>
/// <param name="Comment">A note on the record, or null for none.</param>
/// <param name="Manufacturer">The vaccine's manufacturer, as its code, or null.</param>
/// <param name="InformationSource">Where the record's information came from, as its code, or null.</param>
public record SaveVaccineRecordRequest(
    int VaccineId,
    DateOnly? AdministeredDate = null,
    int? DoseNumber = null,
    string? LotNumber = null,
    DateOnly? ExpirationDate = null,
    string? Comment = null,
    string? Manufacturer = null,
    string? InformationSource = null);
