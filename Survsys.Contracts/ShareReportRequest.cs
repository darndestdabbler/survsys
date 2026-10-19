namespace Survsys.Contracts;

/// <summary>
/// The agency to share a report with: what
/// <c>POST /api/person-conditions/{id}/shares</c> takes.
/// </summary>
/// <remarks>
/// One field, where the report id is the route's. Sharing puts the report into
/// another agency's work queue to be seen, not edited: the share is read-only,
/// which the queue surface honours by offering no workflow actions on a
/// shared-in report. The agency is one <c>GET /api/agencies</c> lists; the API
/// tier refuses an identifier no agency has, a report already shared with the
/// agency, and the agency already investigating the report.
/// </remarks>
/// <param name="AgencyId">
/// The identifier of the agency to share the report read-only with, as
/// <c>GET /api/agencies</c> lists it.
/// </param>
public record ShareReportRequest(int AgencyId);
