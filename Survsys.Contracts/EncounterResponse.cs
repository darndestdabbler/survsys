namespace Survsys.Contracts;

/// <summary>
/// A healthcare encounter on a case, as
/// <c>GET /api/cases/{personConditionId}/encounters</c> lists it.
/// </summary>
/// <param name="Id">The encounter's own id.</param>
/// <param name="EncounterDate">The day of the encounter.</param>
/// <param name="EncounterType">The kind of encounter, as its code, or null.</param>
/// <param name="FacilityId">The facility the encounter was at, or null.</param>
/// <param name="FacilityName">That facility's name, for a list to show it by, or null.</param>
/// <param name="Description">What happened, or null.</param>
public record EncounterResponse(
    int Id,
    DateOnly EncounterDate,
    string? EncounterType,
    int? FacilityId,
    string? FacilityName,
    string? Description);
