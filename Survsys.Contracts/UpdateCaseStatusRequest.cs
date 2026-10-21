namespace Survsys.Contracts;

/// <summary>
/// The case status to set on a report: what
/// <c>PUT /api/person-conditions/{id}/status</c> takes.
/// </summary>
/// <remarks>
/// One writable field, where the report id is the route's. The classification
/// is set directly in this release rather than derived from a report's labs,
/// address or exposures: the disease-specific auto-classification is the
/// deferred, Utah-specific work, so a user chooses the status. The value is the
/// legacy status code — Confirmed, Probable, Suspected or Not a Case — and the
/// API tier validates it against the case status vocabulary, refusing a code it
/// does not know. Null clears the status to Unknown, which is how an
/// unclassified case is left.
/// </remarks>
/// <param name="CaseStatus">
/// The legacy status code to set, or null to clear the status to Unknown.
/// </param>
public record UpdateCaseStatusRequest(string? CaseStatus);
