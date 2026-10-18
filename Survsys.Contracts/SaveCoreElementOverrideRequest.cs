namespace Survsys.Contracts;

/// <summary>
/// An override to save: the element, the condition, the agency, or both, it
/// applies to, and whether it hides the element there. Saving a combination
/// that already has an override replaces that override's flag.
/// </summary>
public record SaveCoreElementOverrideRequest(int CoreElementId, int? ConditionId, int? AgencyId, bool Hidden);
