namespace Survsys.Contracts;

/// <summary>
/// A treatment record to add to a case or to save over an existing one: what
/// <c>POST /api/cases/{personConditionId}/treatments</c> and
/// <c>PUT /api/cases/treatments/{id}</c> take.
/// </summary>
/// <remarks>
/// The treatment is required and must be one the catalogue holds — any
/// treatment in it, not only the ones the case's condition lists, which the
/// screen offers first. Everything else may be left null. A stop date, where
/// both are given, is not before the start date.
/// </remarks>
/// <param name="TreatmentId">The treatment given. Required.</param>
/// <param name="TreatmentDate">The day the treatment started, or null for none.</param>
/// <param name="StopTreatmentDate">The day the treatment stopped, or null for none.</param>
/// <param name="Dose">The dose given, or null for none.</param>
/// <param name="Comment">A note on the record, or null for none.</param>
public record SaveTreatmentRecordRequest(
    int TreatmentId,
    DateOnly? TreatmentDate = null,
    DateOnly? StopTreatmentDate = null,
    string? Dose = null,
    string? Comment = null);
