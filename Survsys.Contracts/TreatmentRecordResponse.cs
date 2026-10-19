namespace Survsys.Contracts;

/// <summary>
/// A treatment recorded against a case, as
/// <c>GET /api/cases/{personConditionId}/treatments</c> lists it.
/// </summary>
/// <param name="Id">The record's own id.</param>
/// <param name="TreatmentId">The treatment given.</param>
/// <param name="TreatmentName">The treatment's name, for a list to show it by.</param>
/// <param name="TreatmentDate">The day the treatment started, or null.</param>
/// <param name="StopTreatmentDate">The day the treatment stopped, or null.</param>
/// <param name="Dose">The dose given, or null.</param>
/// <param name="Comment">A note on the record, or null.</param>
public record TreatmentRecordResponse(
    int Id,
    int TreatmentId,
    string? TreatmentName,
    DateOnly? TreatmentDate,
    DateOnly? StopTreatmentDate,
    string? Dose,
    string? Comment);
