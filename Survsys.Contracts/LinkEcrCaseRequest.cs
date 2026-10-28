namespace Survsys.Contracts;

/// <summary>
/// A reviewer's settling of an eICR held for review: whom its patient is, and
/// optionally the case it belongs to.
/// </summary>
/// <remarks>
/// With no person, the eICR's patient is made a new person. A case named must
/// be an open case of that person on one of the eICR's reportable
/// conditions; on any other condition the person's open case is linked, or a
/// new one opened.
/// </remarks>
/// <param name="PersonId">The person on file the patient is, or null for a new person.</param>
/// <param name="PersonConditionId">The person's case to link, or null to link or open one by condition.</param>
public record LinkEcrCaseRequest(int? PersonId, int? PersonConditionId = null);
