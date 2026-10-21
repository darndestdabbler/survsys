namespace Survsys.Contracts;

/// <summary>
/// A healthcare encounter to add to a case or to save over an existing one:
/// what <c>POST /api/cases/{personConditionId}/encounters</c> and
/// <c>PUT /api/cases/encounters/{id}</c> take.
/// </summary>
/// <remarks>
/// The date is required — an encounter is a visit on a day. A facility, where
/// named, must be one on file, which the endpoint refuses where it is not.
/// </remarks>
/// <param name="EncounterDate">The day of the encounter. Required.</param>
/// <param name="EncounterType">The kind of encounter, as its code, or null for none.</param>
/// <param name="FacilityId">The facility the encounter was at, or null for none.</param>
/// <param name="Description">What happened, or null for none.</param>
public record SaveEncounterRequest(
    DateOnly? EncounterDate,
    string? EncounterType = null,
    int? FacilityId = null,
    string? Description = null);
