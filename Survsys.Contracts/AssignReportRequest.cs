namespace Survsys.Contracts;

/// <summary>
/// The agency to assign a report to: what
/// <c>PUT /api/person-conditions/{id}/assignment</c> takes.
/// </summary>
/// <remarks>
/// One field, where the report id is the route's. Assigning a report names its
/// investigating agency and returns it to the assigned-to-LHD start of the
/// workflow, as the legacy enters a case into the Local Health Department's
/// queue. The agency is one <c>GET /api/agencies</c> lists; the API tier
/// refuses an identifier no agency has.
/// </remarks>
/// <param name="AgencyId">
/// The identifier of the investigating agency to assign the report to, as
/// <c>GET /api/agencies</c> lists it.
/// </param>
public record AssignReportRequest(int AgencyId);
